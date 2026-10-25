using System;
using System.Collections.Generic;
namespace Checkmate55.Core {
// Car radio state: five physical presets, volume, mute, speaker balance and fader. The car can reach in and change any of it.
// A hidden transmission only becomes audible when the right preset is selected AND the sound is isolated to the rear-left speaker.
public sealed class RadioModel {
 public const int Presets=5;
 public const float Step=.2f;
 public int Station{get;private set;}=1;
 public float Volume{get;private set;}=.5f;
 public bool Muted{get;private set;}
 public float Balance{get;private set;}            // -1 left ... +1 right
 public float Fader{get;private set;}              // -1 rear ... +1 front
 public bool Locked;                               // the car holds the controls: the passenger must win them back
 public int TargetPreset=StoryText.RadioPreset;
 public bool RecordingFound{get;private set;}
 public int Favorite{get;private set;}             // 0 = none yet
 public bool FavoriteLost{get;private set;}
 public readonly float[] Listened=new float[Presets+1];
 public readonly string[] StationNames={"","STATION 1","STATION 2","STATION 3","STATION 4","STATION 5"};
 float audibleFor;
 public event Action<string> Changed;
 public event Action RecordingHeard;
 void Note(string what)=>Changed?.Invoke(what);
 static float Snap(float v)=>(float)Math.Round(Math.Max(-1,Math.Min(1,v))/Step)*Step;
 public void PreferredStart(int preset){if(preset>=1&&preset<=Presets)Station=preset;}
 // ---- passenger controls (refused while the car holds the radio) ----
 public bool Select(int preset){if(Locked||preset<1||preset>Presets)return false;if(Station!=preset){Station=preset;Note("station");}return true;}
 public bool VolumeStep(int dir){if(Locked)return false;float v=Math.Max(0,Math.Min(1,Volume+dir*.1f));if(Math.Abs(v-Volume)>1e-4f){Volume=v;if(dir<0)Note("volume.down");else Note("volume.up");}if(v>0)Muted=false;return true;}
 public bool ToggleMute(){if(Locked)return false;Muted=!Muted;Note(Muted?"mute":"unmute");return true;}
 public bool BalanceStep(int dir){if(Locked)return false;float b=Snap(Balance+dir*Step);if(Math.Abs(b-Balance)>1e-4f){Balance=b;Note("balance");}return true;}
 public bool FaderStep(int dir){if(Locked)return false;float f=Snap(Fader+dir*Step);if(Math.Abs(f-Fader)>1e-4f){Fader=f;Note("fader");}return true;}
 // ---- the car's controls (never refused) ----
 public void CarSelect(int preset){if(preset>=1&&preset<=Presets&&preset!=Station){Station=preset;Note("car.station");}}
 public void CarVolume(float v){Volume=Math.Max(0,Math.Min(1,v));if(v>0)Muted=false;Note("car.volume");}
 public void CarMute(){Muted=true;Note("car.mute");}
 public void CarRoute(float balance,float fader){Balance=Snap(balance);Fader=Snap(fader);Note("car.route");}
 // The car deliberately removes the passenger's favourite station.
 public bool RemoveFavorite(){if(Favorite==0)return false;int old=Favorite;FavoriteLost=true;Favorite=0;Array.Clear(Listened,0,Listened.Length);CarSelect(old%Presets+1);Note("favorite.removed");return true;}
 // ---- hidden transmission ----
 public float Distance=>(float)Math.Sqrt((Balance+1)*(Balance+1)+(Fader+1)*(Fader+1));
 public bool RecordingAudible=>Station==TargetPreset&&Balance<=-.6f-1e-4f&&Fader<=-.6f-1e-4f&&Volume>.05f&&!Muted;
 // How loud the recording is relative to the music (0 = not at all).
 public float RecordingGain=>Station!=TargetPreset||Muted?0:Math.Max(0,Math.Min(1,1.15f-Distance/1.1f));
 public void Tick(float dt){
  if(!Muted&&Volume>.05f&&!Locked)Listened[Station]+=dt;
  if(!FavoriteLost&&Favorite==0){for(int i=1;i<=Presets;i++)if(Listened[i]>=25f){Favorite=i;}}
  if(RecordingAudible){audibleFor+=dt;if(!RecordingFound&&audibleFor>=1.2f){RecordingFound=true;RecordingHeard?.Invoke();}}else audibleFor=0;
 }
 public string Display(bool jammed=false){
  if(jammed)return "§§ SIGNAL LOST §§";
  string bars=new string('▮',(int)Math.Round(Volume*5))+new string('▯',5-(int)Math.Round(Volume*5));
  return (Muted?"MUTED":"PRESET "+Station)+"  "+bars+"\nBAL "+Dot(Balance,"L","R")+"  FADE "+Dot(Fader,"R","F");
 }
 static string Dot(float v,string lo,string hi){int p=(int)Math.Round((v+1)/Step/2);var s=new char[6];for(int i=0;i<6;i++)s[i]='·';s[Math.Max(0,Math.Min(5,p))]='●';return lo+new string(s)+hi;}
}
// Rear climate: temperature, fan, and a settled-value history used by the Passenger-04 service test.
public sealed class ClimateModel {
 public int Temp{get;private set;}=21;
 public int Fan{get;private set;}=1;
 public bool Frozen;                       // the car has cut the climate system
 public bool Solved{get;private set;}
 readonly List<int> settled=new List<int>();
 float sinceChange=99,sincePattern=99;int lastSettled=21;bool patternReady;
 public event Action SolvedEvent;
 public event Action<string> Changed;
 public IReadOnlyList<int> History=>settled;
 public bool Adjust(int delta){if(Frozen)return false;int t=Math.Max(16,Math.Min(28,Temp+delta));if(t!=Temp){Temp=t;sinceChange=0;Changed?.Invoke("temp");}return true;}
 public bool SetFan(int level){if(Frozen)return false;Fan=Math.Max(0,Math.Min(3,level));Changed?.Invoke("fan");TrySolve();return true;}
 void TrySolve(){if(patternReady&&Fan==StoryText.ClimateFan&&!Solved){Solved=true;SolvedEvent?.Invoke();}}
 public bool CycleFan(){return SetFan((Fan+1)%4);}
 // legacy single control: the foundation's climate toggle between 19 and 21 degrees
 public bool Toggle(){if(Frozen)return false;Temp=Temp==21?19:21;sinceChange=0;Changed?.Invoke("temp");return true;}
 public void Reset(){Temp=21;Fan=1;settled.Clear();patternReady=false;sinceChange=99;lastSettled=21;}
 public void Tick(float dt){
  if(sinceChange<10)sinceChange+=dt;sincePattern+=dt;
  // a temperature only "counts" once it has been left alone for a moment
  if(sinceChange>=1.2f&&sinceChange<10&&Temp!=lastSettled){settled.Add(Temp);lastSettled=Temp;if(settled.Count>6)settled.RemoveAt(0);CheckPattern();}
  if(patternReady&&sincePattern>9f){patternReady=false;}
 }
 void CheckPattern(){
  var p=StoryText.ClimatePattern;patternReady=false;
  if(settled.Count>=p.Length){bool ok=true;for(int i=0;i<p.Length;i++)if(settled[settled.Count-p.Length+i]!=p[i])ok=false;if(ok){patternReady=true;sincePattern=0;Changed?.Invoke("pattern");TrySolve();}}
 }
 public bool PatternReady=>patternReady;
}
// A rhythm lock on the overhead light: N presses, a pause, M presses ...
public sealed class RhythmLock {
 readonly int[] pattern;readonly List<int> groups=new List<int>();int current;float last=-99;
 public bool Solved{get;private set;}
 public event Action SolvedEvent;
 public event Action Failed;
 public float PauseGap=1.4f,BurstGap=1.2f,Timeout=6f;
 public RhythmLock(int[] pattern){this.pattern=pattern;}
 public IReadOnlyList<int> Groups=>groups;
 public void Press(float now){
  if(Solved)return;
  float gap=now-last;
  if(current>0&&gap>Timeout){Reset();}
  else if(current>0&&gap>=PauseGap){groups.Add(current);current=0;if(!Prefix()){Reset();Failed?.Invoke();}}
  current++;last=now;
  if(groups.Count==pattern.Length-1&&current==pattern[pattern.Length-1]){Solved=true;SolvedEvent?.Invoke();return;}
  if(groups.Count<pattern.Length&&current>pattern[groups.Count]){Reset();Failed?.Invoke();}
 }
 // a group is accepted once the pause that closes it has elapsed
 public void Tick(float now){if(!Solved&&current>0&&now-last>Timeout){Reset();}}
 bool Prefix(){for(int i=0;i<groups.Count;i++)if(i>=pattern.Length||groups[i]!=pattern[i])return false;return true;}
 public void Reset(){groups.Clear();current=0;last=-99;}
}
// The previous passenger's phone: dead until charged in the correct port, jammed until maintenance mode, then able to call out.
public enum PhoneState { Missing, Dead, Charging, Jammed, Ready, Calling, Connected }
public sealed class PhoneModel {
 public bool Held,Plugged,JammerOff,HasLocation;
 public float Battery;
 public PhoneState State{get;private set;}=PhoneState.Missing;
 public event Action<PhoneState> StateChanged;
 void Set(PhoneState s){if(s!=State){State=s;StateChanged?.Invoke(s);}}
 public void Tick(float dt){
  if(State==PhoneState.Missing){if(Held)Set(PhoneState.Dead);return;}
  if(Plugged&&Battery<100)Battery=Math.Min(100,Battery+dt*14f);
  if(State==PhoneState.Dead&&Plugged)Set(PhoneState.Charging);
  if(State==PhoneState.Charging&&Battery>=35)Set(PhoneState.Jammed);
  if(State==PhoneState.Jammed&&JammerOff)Set(PhoneState.Ready);
  if(State==PhoneState.Ready&&!JammerOff)Set(PhoneState.Jammed);
 }
 public bool PowerOk=>Battery>=35;
 // Returns true when the call goes through.
 public bool Call(){if(State!=PhoneState.Ready||!HasLocation)return false;Set(PhoneState.Calling);Set(PhoneState.Connected);return true;}
 public string Display(float mile){switch(State){
  case PhoneState.Dead:return "";case PhoneState.Charging:return "CHARGING "+(int)Battery+"%";case PhoneState.Jammed:return (int)Battery+"%\nNO SERVICE\nSIGNAL JAMMED";
  case PhoneState.Ready:return (int)Battery+"%  ▂▄\nTAP TO CALL EMERGENCY\n"+(HasLocation?"LOCATION  A55 MI "+mile.ToString("0.0"):"LOCATION UNKNOWN");
  case PhoneState.Connected:case PhoneState.Calling:return "CALL CONNECTED\nA55 MI "+mile.ToString("0.0");default:return "";}}
}
}
