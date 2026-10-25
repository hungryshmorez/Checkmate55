using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using Checkmate55.Core;
namespace Checkmate55 {
// Radio playback (five user-supplied songs), plus the procedural sound palette of the new systems: sirens, static, zaps, impacts.
// No music is invented: a preset without a song plays only broadcast static and shows NO SIGNAL on the display.
public sealed class OverrideAudio : MonoBehaviour {
 OverrideDirector d;AudioSource music,noise,siren,fx;AudioClip noiseClip,sirenClip,zap,spark,boom,swish,beep,dial,heart,thud,whoosh,tick;
 readonly AudioClip[] songs=new AudioClip[6];readonly string[] names=new string[6],titles=new string[6];int playing=-1;float heartbeat;
 // Songs can also stream from the web (your GitHub Pages / raw.githubusercontent.com): see Assets/Resources/Radio/stations.txt.
 readonly string[] urls=new string[6];readonly string[] failed=new string[6];string baseUrl="";readonly System.Collections.Generic.List<int> pendingLoads=new System.Collections.Generic.List<int>();bool loadingNow;
 public int Pending=>pendingLoads.Count+(loadingNow?1:0);
 public int RemoteCount{get{int n=0;for(int i=1;i<=5;i++)if(!string.IsNullOrEmpty(urls[i]))n++;return n;}}
 public string LastRadioError{get{for(int i=1;i<=5;i++)if(failed[i]!=null)return "preset "+i+": "+failed[i];return "";}}
 static string Resolve(string baseUrl,string v){
  v=v.Trim();if(v.StartsWith("http://")||v.StartsWith("https://"))return v;
  var parts=v.Split('/');for(int i=0;i<parts.Length;i++)parts[i]=System.Uri.EscapeDataString(parts[i]);
  return baseUrl.TrimEnd('/')+"/"+string.Join("/",parts);
 }
 static AudioType TypeFor(string url){string u=url.ToLowerInvariant();int q=u.IndexOf('?');if(q>=0)u=u.Substring(0,q);return u.EndsWith(".ogg")?AudioType.OGGVORBIS:u.EndsWith(".wav")?AudioType.WAV:AudioType.MPEG;}
 System.Collections.IEnumerator LoadRemote(){
  loadingNow=true;
  while(pendingLoads.Count>0){
   int i=pendingLoads[0];pendingLoads.RemoveAt(0);if(songs[i]!=null||string.IsNullOrEmpty(urls[i]))continue;
   string url=Resolve(baseUrl,urls[i]);
   using(var req=UnityWebRequestMultimedia.GetAudioClip(url,TypeFor(url))){
    ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio=false;
    yield return req.SendWebRequest();
    if(req.result==UnityWebRequest.Result.Success){var clip=DownloadHandlerAudioClip.GetContent(req);if(clip!=null&&clip.length>0){clip.name="preset"+i;songs[i]=clip;failed[i]=null;if(playing==i)playing=-1;}else failed[i]="empty audio";}
    else{failed[i]=req.error;Debug.LogWarning("[radio] preset "+i+" could not load "+url+" : "+req.error);}
   }
  }
  loadingNow=false;
 }
 void Prioritise(int preset){if(preset<1||preset>5||songs[preset]!=null||string.IsNullOrEmpty(urls[preset])||failed[preset]!=null)return;int at=pendingLoads.IndexOf(preset);if(at>0){pendingLoads.RemoveAt(at);pendingLoads.Insert(0,preset);}}

 // ---- playlist stations: a whole folder of the manifest (minus excluded albums) played in shuffled order, and YouTube playlists
 [Serializable] class ManifestTrack {public string src="",title="",album="";}
 [Serializable] class Manifest {public ManifestTrack[] tracks;}
 sealed class Playlist {public readonly List<string> urls=new List<string>(),titles=new List<string>();public int index=-1;public AudioClip next;public int nextIndex=-1;public bool busy;public string spec="";public float startedAt;public int skips;}
 readonly Playlist[] lists=new Playlist[6];readonly string[] ytIds=new string[6];string manifestError="";bool manifestLoaded,manifestLoading;
 Manifest manifest;string ytCurrent="";float ytPoll,ytVolumeSent=-1;int ytStatusCache;string ytTitleCache="";
#if UNITY_WEBGL && !UNITY_EDITOR
 [DllImport("__Internal")] static extern void CM55_YtPlay(string id);
 [DllImport("__Internal")] static extern void CM55_YtPause();
 [DllImport("__Internal")] static extern void CM55_YtVolume(int v);
 [DllImport("__Internal")] static extern int CM55_YtStatus();
 [DllImport("__Internal")] static extern IntPtr CM55_YtTitle();
 [DllImport("__Internal")] static extern void CM55_YtFree(IntPtr p);
 static bool YtAvailable=>true;
#else
 static void CM55_YtPlay(string id){}static void CM55_YtPause(){}static void CM55_YtVolume(int v){}static int CM55_YtStatus(){return 0;}static string YtTitleNative(){return "";}
 static bool YtAvailable=>false;
#endif
 static string YtTitle(){
#if UNITY_WEBGL && !UNITY_EDITOR
  IntPtr p=CM55_YtTitle();if(p==IntPtr.Zero)return "";try{return Marshal.PtrToStringAnsi(p);}finally{CM55_YtFree(p);}
#else
  return "";
#endif
 }
 // "manifest:music/driftwave/ ; !music/tanky/homer-simpson-last-call-lullabies/"  -> include prefixes, and excluded prefixes marked with "!"
 static bool Matches(string spec,string src){
  bool any=false,included=false;
  foreach(var raw in spec.Split(';')){
   string t=raw.Trim();if(t.Length==0)continue;
   if(t[0]=='!'){if(src.StartsWith(t.Substring(1).Trim(),StringComparison.OrdinalIgnoreCase))return false;}
   else{any=true;if(src.StartsWith(t,StringComparison.OrdinalIgnoreCase))included=true;}
  }
  return any&&included;
 }
 IEnumerator LoadManifest(){
  manifestLoading=true;
  string url=baseUrl.TrimEnd('/')+"/manifest.json";
  using(var req=UnityWebRequest.Get(url)){
   yield return req.SendWebRequest();
   if(req.result==UnityWebRequest.Result.Success){
    try{manifest=JsonUtility.FromJson<Manifest>(req.downloadHandler.text);}catch(Exception e){manifestError=e.Message;}
   }else manifestError=req.error;
  }
  if(manifest!=null&&manifest.tracks!=null){
   var rng=new System.Random(Environment.TickCount);
   for(int i=1;i<=5;i++){
    var l=lists[i];if(l==null)continue;
    foreach(var t in manifest.tracks){
     if(string.IsNullOrEmpty(t.src)||!Matches(l.spec,t.src))continue;
     l.urls.Add(Resolve(baseUrl,t.src));
     string title=string.IsNullOrEmpty(t.title)?System.IO.Path.GetFileNameWithoutExtension(t.src):t.title;
     int dash=title.IndexOf(" — ",StringComparison.Ordinal);l.titles.Add(dash>=0&&dash<24?title.Substring(dash+3):title);
    }
    for(int k=l.urls.Count-1;k>0;k--){int j=rng.Next(k+1);string u=l.urls[k];l.urls[k]=l.urls[j];l.urls[j]=u;string tt=l.titles[k];l.titles[k]=l.titles[j];l.titles[j]=tt;}
    if(l.urls.Count==0)failed[i]="no tracks matched "+l.spec;
   }
  }else if(manifestError.Length==0)manifestError="empty manifest";
  if(manifestError.Length>0)for(int i=1;i<=5;i++)if(lists[i]!=null)failed[i]=manifestError;
  manifestLoaded=true;manifestLoading=false;
 }
 IEnumerator FetchClip(string url,Action<AudioClip,string> done){
  using(var req=UnityWebRequestMultimedia.GetAudioClip(url,TypeFor(url))){
   ((DownloadHandlerAudioClip)req.downloadHandler).streamAudio=false;
   yield return req.SendWebRequest();
   if(req.result==UnityWebRequest.Result.Success){var c=DownloadHandlerAudioClip.GetContent(req);if(c!=null&&c.length>0)done(c,null);else done(null,"empty audio");}
   else done(null,req.error);
  }
 }
 // Load the track at l.index for station i (the one that should be heard now).
 IEnumerator LoadCurrent(int i){
  var l=lists[i];l.busy=true;
  for(int attempt=0;attempt<4&&l.urls.Count>0;attempt++){
   int idx=((l.index%l.urls.Count)+l.urls.Count)%l.urls.Count;AudioClip got=null;string err=null;
   if(l.next!=null&&l.nextIndex==idx){got=l.next;l.next=null;l.nextIndex=-1;}
   else yield return FetchClip(l.urls[idx],(c,e)=>{got=c;err=e;});
   if(got!=null){
    var old=songs[i];songs[i]=got;titles[i]=l.titles[idx];failed[i]=null;l.startedAt=Time.unscaledTime;if(old!=null)Destroy(old);
    if(playing==i)playing=-1;                     // restart the music source on the new clip
    break;
   }
   failed[i]=err;Debug.LogWarning("[radio] skipping track: "+l.urls[idx]+" : "+err);l.index++;
  }
  l.busy=false;
 }
 IEnumerator Preload(int i){
  var l=lists[i];if(l.urls.Count<2||l.next!=null)yield break;
  int idx=(l.index+1)%l.urls.Count;AudioClip got=null;
  l.busy=true;yield return FetchClip(l.urls[idx],(c,e)=>{got=c;});
  l.busy=false;
  if(got!=null){if(playing==i||songs[i]!=null){l.next=got;l.nextIndex=idx;}else Destroy(got);}
 }
 void Advance(int i){var l=lists[i];if(l.busy)return;l.index++;var old=songs[i];songs[i]=null;if(old!=null&&music.clip==old){music.Stop();music.clip=null;}StartCoroutine(LoadCurrent(i));}
 // Keep exactly one clip (plus the next one) in memory: a station that is no longer tuned releases its audio.
 void ReleaseOthers(int keep){
  for(int i=1;i<=5;i++){
   var l=lists[i];if(l==null||i==keep||l.busy)continue;
   if(songs[i]!=null){Destroy(songs[i]);songs[i]=null;}if(l.next!=null){Destroy(l.next);l.next=null;l.nextIndex=-1;}
  }
 }
 void TuneStations(int want){
  for(int i=1;i<=5;i++){
   var l=lists[i];
   if(l!=null&&i==want&&manifestLoaded&&l.urls.Count>0){
    if(songs[i]==null&&!l.busy){if(l.index<0)l.index=0;StartCoroutine(LoadCurrent(i));}
    else if(songs[i]!=null&&!l.busy&&l.next==null&&Time.unscaledTime-l.startedAt>3f&&music.isPlaying)StartCoroutine(Preload(i));
    // the track ended: on to the next one
    if(songs[i]!=null&&playing==i&&music.clip==songs[i]&&!music.isPlaying&&Time.unscaledTime-l.startedAt>2f&&!l.busy)Advance(i);
   }
  }
  ReleaseOthers(want);
 }
 void TuneYouTube(int want,float vol){
  string id=want>=1?ytIds[want]:null;
  if(!string.IsNullOrEmpty(id)){
   if(ytCurrent!=id){ytCurrent=id;var choices=id.Split(',');CM55_YtPlay(choices[UnityEngine.Random.Range(0,choices.Length)].Trim());}
   int v=Mathf.RoundToInt(Mathf.Clamp01(vol)*100);if(v!=(int)ytVolumeSent){ytVolumeSent=v;CM55_YtVolume(v);}
   if(Time.unscaledTime>ytPoll){ytPoll=Time.unscaledTime+1.5f;ytStatusCache=CM55_YtStatus();ytTitleCache=YtTitle();}
  }else if(ytCurrent!=""){ytCurrent="";CM55_YtPause();}
 }
 bool IsYouTubePlaying(int want)=>IsYouTube(want)&&YtAvailable&&ytStatusCache==1;
 public bool IsYouTube(int preset)=>preset>=1&&preset<=5&&!string.IsNullOrEmpty(ytIds[preset]);
 public int StationCount{get{int n=0;for(int i=1;i<=5;i++)if(songs[i]!=null||lists[i]!=null||!string.IsNullOrEmpty(ytIds[i])||!string.IsNullOrEmpty(urls[i]))n++;return n;}}
 public int SongCount{get{int n=0;for(int i=1;i<=5;i++)if(songs[i]!=null||IsYouTube(i)&&YtAvailable)n++;return n;}}
 public bool HasSong(int preset)=>preset>=1&&preset<=5&&(songs[preset]!=null||IsYouTube(preset)&&YtAvailable);
 public string StationLabel(int preset){
  if(preset<1||preset>5)return "";
  string n=string.IsNullOrEmpty(names[preset])?"PRESET "+preset:names[preset];
  if(IsYouTube(preset)){
   if(!YtAvailable)return n+"  ·  NO SIGNAL";
   if(preset==playing||ytCurrent==ytIds[preset]){if(ytStatusCache==3)return n+"  ·  NO SIGNAL";return n+(ytTitleCache.Length>0?"  ·  "+Trim(ytTitleCache):"  ·  TUNING…");}
   return n;
  }
  if(songs[preset]!=null)return n+(string.IsNullOrEmpty(titles[preset])?"":"  ·  "+Trim(titles[preset]));
  bool trying=(lists[preset]!=null||!string.IsNullOrEmpty(urls[preset]))&&(failed[preset]==null);
  return n+(trying?"  ·  TUNING…":"  ·  NO SIGNAL");
 }
 // accepts a bare playlist id (PL...) or any YouTube link containing list=ID
 static string ParseYouTubeId(string v){
  if(v.IndexOf(',')>=0){var all=new List<string>();foreach(var part in v.Split(','))if(part.Trim().Length>0)all.Add(ParseYouTubeId(part.Trim()));return string.Join(",",all);}   // several playlists on one station: one is picked each time you tune in
  int i=v.IndexOf("list=",StringComparison.Ordinal);if(i>=0){string r=v.Substring(i+5);int e=r.IndexOfAny(new[]{'&','#'});return e>=0?r.Substring(0,e):r;}return v;}
 static string Trim(string t)=>t.Length>26?t.Substring(0,25)+"…":t;
 AudioSource Src(bool loop,float vol){var a=gameObject.AddComponent<AudioSource>();a.loop=loop;a.volume=vol;a.playOnAwake=false;a.spatialBlend=0;return a;}
 public void Initialize(OverrideDirector director){
  d=director;music=Src(true,0);noise=Src(true,0);siren=Src(true,0);fx=Src(false,.8f);
  noiseClip=Make("Broadcast static",2,0);sirenClip=Make("Police wail",3,1);zap=Make("EMP zap",.4f,2);spark=Make("Spark",.25f,3);boom=Make("Impact",2.2f,4);swish=Make("Card",.3f,5);beep=Make("Beep",.12f,6);dial=Make("Dial",1.2f,7);heart=Make("Heartbeat",1f,8);thud=Make("Thud",.3f,9);whoosh=Make("Whoosh",.5f,10);tick=Make("Tick",.05f,11);
  noise.clip=noiseClip;siren.clip=sirenClip;
  var meta=Resources.Load<TextAsset>("Radio/stations");
  if(meta!=null)foreach(var line in meta.text.Split('\n')){
   var p=line.Trim().Split('|');int n;
   if(p.Length>=2&&p[0].Trim().ToLowerInvariant()=="base"){baseUrl=p[1].Trim();continue;}
   if(p.Length>=2&&int.TryParse(p[0].Trim(),out n)&&n>=1&&n<=5){names[n]=p[1].Trim();if(p.Length>=3)titles[n]=p[2].Trim();if(p.Length>=4&&p[3].Trim().Length>0){string src=p[3].Trim();
    if(src.StartsWith("youtube:",StringComparison.OrdinalIgnoreCase))ytIds[n]=ParseYouTubeId(src.Substring(8).Trim());
    else if(src.StartsWith("manifest:",StringComparison.OrdinalIgnoreCase))lists[n]=new Playlist{spec=src.Substring(9).Trim()};
    else urls[n]=src;}}
  }
  for(int i=1;i<=5;i++)songs[i]=Resources.Load<AudioClip>("Radio/preset"+i);       // a file in the project always wins over a web link
  foreach(int i in new[]{4,1,2,3,5})if(songs[i]==null&&!string.IsNullOrEmpty(urls[i])&&(baseUrl.Length>0||urls[i].StartsWith("http")))pendingLoads.Add(i);
  if(pendingLoads.Count>0)StartCoroutine(LoadRemote());
  bool anyList=false;for(int i=1;i<=5;i++)if(lists[i]!=null)anyList=true;
  if(anyList&&baseUrl.Length>0)StartCoroutine(LoadManifest());
  noise.Play();siren.Play();
 }
 AudioClip Make(string name,float seconds,int kind){
  int rate=22050;int n=(int)(rate*seconds);var s=new float[n];var rng=new System.Random(77+kind);float low=0,phase=0;
  for(int i=0;i<n;i++){
   float t=(float)i/rate;float white=(float)rng.NextDouble()*2-1;low=Mathf.Lerp(low,white,.25f);float env=Mathf.Min(1,t*40)*Mathf.Min(1,(seconds-t)*30);float v=0;
   switch(kind){
    case 0:v=(white*.5f+low*.5f)*.6f;break;                                                       // static
    case 1:{float f=880+260*Mathf.Sin(t*2*Mathf.PI/1.5f);phase+=2*Mathf.PI*f/rate;v=(Mathf.Sin(phase)+.35f*Mathf.Sin(phase*2))*.35f;break;}   // wail
    case 2:v=(Mathf.Sin(t*(1500-3200*t)*2*Mathf.PI)*.5f+white*.3f)*Mathf.Exp(-t*7)*env;break;       // EMP
    case 3:v=(rng.NextDouble()<.02?white:0)*.9f*Mathf.Exp(-t*6);break;                           // spark crackle
    case 4:v=(Mathf.Sin(t*(70-30*t)*2*Mathf.PI)*.9f+low*.5f)*Mathf.Exp(-t*2.2f)*env;break;           // impact
    case 5:v=low*Mathf.Sin(t*Mathf.PI/seconds)*.7f*env;break;                                    // card swish
    case 6:v=Mathf.Sin(t*880*2*Mathf.PI)*.4f*env;break;
    case 7:{float[] f1={697,770,852,941},f2={1209,1336,1477};int k=(int)(t/.2f)%4;v=(Mathf.Sin(t*f1[k]*2*Mathf.PI)+Mathf.Sin(t*f2[k%3]*2*Mathf.PI))*.22f*(t%.2f<.15f?1:0);break;}   // dialling
    case 8:{float a=t%1f;v=(Mathf.Sin(a*60*2*Mathf.PI)*Mathf.Exp(-a*18)+Mathf.Sin((a-.28f)*55*2*Mathf.PI)*(a>.28f?Mathf.Exp(-(a-.28f)*18):0))*.9f;break;}   // heartbeat
    case 9:v=(Mathf.Sin(t*110*2*Mathf.PI)+low)*Mathf.Exp(-t*18);break;                            // thud
    case 10:v=low*Mathf.Sin(t*Mathf.PI/seconds)*Mathf.Lerp(.4f,1f,t/seconds)*env;break;         // whoosh
    default:v=Mathf.Sin(t*1800*2*Mathf.PI)*Mathf.Exp(-t*90);break;
   }
   s[i]=Mathf.Clamp(v,-1,1);
  }
  var c=AudioClip.Create(name,n,1,rate,false);c.SetData(s,0);return c;
 }
 public void Zap(){fx.pitch=1;fx.PlayOneShot(zap,.7f);}
 public void Spark(){fx.pitch=UnityEngine.Random.Range(.9f,1.2f);fx.PlayOneShot(spark,.8f);}
 public void Boom(){fx.pitch=1;fx.PlayOneShot(boom,1f);}
 public void Swish(){fx.pitch=UnityEngine.Random.Range(.95f,1.1f);fx.PlayOneShot(swish,.6f);}
 public void Beep(float pitch=1){fx.pitch=pitch;fx.PlayOneShot(beep,.7f);}
 public void Dial(){fx.pitch=1;fx.PlayOneShot(dial,.6f);}
 public void Thud(){fx.pitch=UnityEngine.Random.Range(.9f,1.05f);fx.PlayOneShot(thud,.9f);}
 public void Whoosh(){fx.pitch=1;fx.PlayOneShot(whoosh,.8f);}
 public void Tick(){fx.pitch=1;fx.PlayOneShot(tick,.5f);}
 public void SetSiren(float volume,float pan){siren.volume=Mathf.Clamp01(volume)*.55f;siren.panStereo=pan;}
 public void Heartbeat(bool on){heartbeat=on?1:0;}
 void Update(){
  if(d==null||d.game==null)return;
  var r=d.Radio;bool jam=d.AudioJam;
  // station switching: a preset with a user-supplied song plays it; otherwise only static
  int want=jam?-1:r.Station;
  if(want>=1)Prioritise(want);
  TuneStations(want);
  if(want!=playing){playing=want;if(want>=1&&songs[want]!=null){music.clip=songs[want];music.loop=lists[want]==null;music.Play();}else{music.Stop();music.clip=null;}}
  float vol=r.Muted||jam?0:r.Volume;
  TuneYouTube(want,vol*.8f*Mathf.Lerp(1f,.55f,Mathf.InverseLerp(0,-1,r.Fader))*(r.RecordingGain>0?Mathf.Lerp(1f,.25f,r.RecordingGain):1f));
  float rear=Mathf.Lerp(1f,.55f,Mathf.InverseLerp(0,-1,r.Fader));          // the rear speakers are quieter
  float duck=r.RecordingGain>0?Mathf.Lerp(1f,.25f,r.RecordingGain):1f;    // the hidden transmission rides on top of the music
  music.volume=Mathf.MoveTowards(music.volume,vol*.8f*rear*duck,Time.deltaTime*2);music.panStereo=Mathf.Clamp(r.Balance,-1,1);
  float staticVol=jam?.45f:(want>=1&&songs[want]==null&&!IsYouTubePlaying(want)&&!r.Muted?.05f*r.Volume+.02f:0)+r.RecordingGain*.28f;
  noise.volume=Mathf.MoveTowards(noise.volume,staticVol,Time.deltaTime*1.5f);noise.panStereo=jam?0:Mathf.Clamp(r.Balance,-1,1);
  if(heartbeat>0&&Time.time>0){if(!fx.isPlaying)fx.PlayOneShot(heart,.5f);}
 }
}
}
