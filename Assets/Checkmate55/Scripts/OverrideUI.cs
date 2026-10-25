using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// IMGUI layer for everything the cabin itself cannot show: title / mode menu, notes, the impact clock, endings. Scales like CabinUI and
// uses large touch targets. Anything diegetic (radio, climate, dock, phone) lives on the cabin, never here.
public sealed class OverrideUI : MonoBehaviour {
 public OverrideDirector director;
 GUIStyle small,label,body,title,sub,button,big,warn;Texture2D dark,panel,white;bool styles;float scale;
 enum Screen_ {Title,Submenu,Network}
 Screen_ screen=Screen_.Title;GameMode pending;Difficulty difficulty=Difficulty.Normal;RuleSet rules=RuleSet.CyberRoadChess;CarSkill skill=CarSkill.Smart;CoopControl coop=CoopControl.TacticalAssist;
 Vector2 notesScroll;public bool NotesOpen{get;private set;}
 GameMode hover=GameMode.Story;
 public bool Blocking=>director!=null&&(director.WaitingForMode||(director.Final!=null&&director.Final.ScreenReady));
 public void OpenNotes(){NotesOpen=true;}
 public void CloseNotes(){NotesOpen=false;}
 void Setup(){
  var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  small=new GUIStyle{font=font,fontSize=12,alignment=TextAnchor.MiddleCenter,wordWrap=true,normal={textColor=new Color(.85f,.9f,.91f)}};label=new GUIStyle(small){fontSize=16};body=new GUIStyle(small){fontSize=15,alignment=TextAnchor.UpperLeft};
  sub=new GUIStyle(small){fontSize=15};title=new GUIStyle(small){fontSize=44,alignment=TextAnchor.MiddleCenter};big=new GUIStyle(small){fontSize=64,alignment=TextAnchor.MiddleCenter};warn=new GUIStyle(small){fontSize=15,normal={textColor=new Color(1f,.45f,.3f)}};
  button=new GUIStyle(GUI.skin.button){font=font,fontSize=14,wordWrap=true};
  dark=new Texture2D(1,1);dark.SetPixel(0,0,new Color(.02f,.035f,.045f,.9f));dark.Apply();panel=new Texture2D(1,1);panel.SetPixel(0,0,new Color(.04f,.075f,.09f,.82f));panel.Apply();white=Texture2D.whiteTexture;styles=true;
 }
 static string Clock(float s){int t=(int)Mathf.Max(0,s);return (t/60).ToString("00")+":"+(t%60).ToString("00");}

 struct StyleKey:IEquatable<StyleKey>{public int b,a,s,c,f;public bool Equals(StyleKey o){return b==o.b&&a==o.a&&s==o.s&&c==o.c&&f==o.f;}public override int GetHashCode(){return (((b*31+a)*31+s)*31+c)*31+f;}}
 readonly Dictionary<StyleKey,GUIStyle> styleCache=new Dictionary<StyleKey,GUIStyle>();
 int heldCount=-1;string heldText="";
 // Styles are cached: OnGUI runs several times a frame, and a WebGL build must not allocate in it.
 GUIStyle C(GUIStyle b,int a=-1,int size=0,Color? col=null,int fs=-1){
  int packed=0;if(col.HasValue){Color32 c32=col.Value;packed=(c32.r<<24)|(c32.g<<16)|(c32.b<<8)|c32.a;}
  var key=new StyleKey{b=b.GetHashCode(),a=a,s=size,c=packed,f=fs};GUIStyle st;
  if(styleCache.TryGetValue(key,out st))return st;
  st=new GUIStyle(b);if(a>=0)st.alignment=(TextAnchor)a;if(size>0)st.fontSize=size;if(col.HasValue)st.normal.textColor=col.Value;if(fs>=0)st.fontStyle=(FontStyle)fs;
  styleCache[key]=st;return st;
 }
 void OnGUI(){
  GUI.depth=-5;
  var d=director;if(d==null||d.game==null)return;
  if(!styles)Setup();
  scale=Mathf.Max(.8f,UnityEngine.Screen.height/800f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=UnityEngine.Screen.width/scale,h=UnityEngine.Screen.height/scale;
  if(d.WaitingForMode){DrawTitle(w,h);return;}
  if(d.Final!=null&&d.Final.Fade>0){GUI.color=new Color(1,1,1,d.Final.Fade);GUI.DrawTexture(new Rect(0,0,w,h),white);GUI.color=Color.white;}
  if(d.Final!=null&&d.Final.ScreenReady){DrawEnding(w,h);return;}
  if(d.game.Paused)return;
  DrawHud(w,h);DrawRoles(w,h);
  if(d.Mode.ChessOnly)DrawChessPanel(w,h);
 }
 // ---------------------------------------------------------------------------------------------------------------- title
 void DrawTitle(float w,float h){
  var d=director;GUI.DrawTexture(new Rect(0,0,w,h),dark);
  float pw=Mathf.Min(760,w-24),x=(w-pw)/2,y=Mathf.Max(8,h*.05f);
  GUI.Label(new Rect(x,y,pw,56),"CHECKMATE 55",title);GUI.Label(new Rect(x,y+50,pw,34),"O V E R R I D E",C(sub,size:22,col:new Color(1f,.38f,.3f)));
  GUI.Label(new Rect(x,y+84,pw,24),"THE CAR THINKS IN CHESS",small);
  float top=y+120;
  if(screen==Screen_.Title){
   var modes=ModeCatalog.Menu;float bw=(pw-10)/2,bh=62;
   for(int i=0;i<modes.Length;i++){
    var cfg=ModeCatalog.For(modes[i]);var r=new Rect(x+(i%2)*(bw+10),top+(i/2)*(bh+8),bw,bh);
    bool recommended=modes[i]==GameMode.Calibration&&!d.Records.CalibrationDone;
    GUI.color=recommended?new Color(.7f,1f,.9f):Color.white;
    if(GUI.Button(r,cfg.Title+(recommended?"  ★ START HERE":"")+(cfg.Networked?"  (online)":""),button)){Choose(modes[i]);}
    GUI.color=Color.white;if(r.Contains(Event.current.mousePosition))hover=modes[i];
   }
   float by=top+4*(bh+8)+4;
   GUI.DrawTexture(new Rect(x,by,pw,64),panel);GUI.Label(new Rect(x+10,by+4,pw-20,56),ModeCatalog.For(hover).Blurb,sub);
   var rec=d.Records;by+=72;
   GUI.Label(new Rect(x,by,pw,22),"BEST RIDE "+Records.Clock(rec.BestStoryMs)+"   ·   BEST FINAL "+Records.Clock(rec.BestFinalMs)+"   ·   ENDINGS "+rec.EndingCount+"/5   ·   ESCAPES "+rec.Escapes,small);
   GUI.Label(new Rect(x,by+22,pw,22),"MUSIC: "+(d.Sound!=null&&(d.Sound.StationCount>0||d.Sound.Pending>0)?d.Sound.StationCount+" of 5 radio stations set up"+(d.Sound.Pending>0?" · streaming "+d.Sound.Pending+" more…":"")+(d.Sound.LastRadioError.Length>0?" · "+d.Sound.LastRadioError:""):"no radio songs found (see Assets/Resources/Radio/README.txt)")+"   ·   CAR VOICE "+(d.VoiceOn?"ON":"OFF"),small);
   if(GUI.Button(new Rect(x+pw-150,by+46,150,32),"VOICE: "+(d.VoiceOn?"ON":"OFF"),button)){d.VoiceOn=!d.VoiceOn;PlayerPrefs.SetInt("cm55.voice",d.VoiceOn?1:0);PlayerPrefs.Save();if(!d.VoiceOn)BrowserPlatform.SpeakStop();}
  }else if(screen==Screen_.Submenu)DrawSubmenu(x,top,pw);
  else DrawNetwork(x,top,pw);
 }
 void Choose(GameMode m){
  pending=m;
  switch(m){
   case GameMode.Calibration:Launch(m);break;
   case GameMode.CoopEscape:case GameMode.DriverlessDuel:screen=Screen_.Network;break;
   default:screen=Screen_.Submenu;break;
  }
 }
 void Launch(GameMode m){
  var d=director;d.StartMode(m,cfg=>{cfg.Difficulty=difficulty;cfg.Skill=skill;cfg.Coop=coop;if(m==GameMode.PassengerChess)cfg.Rules=rules==RuleSet.Standard?RuleSet.Standard:rules;if(m==GameMode.RoadChess&&rules==RuleSet.Standard)cfg.Rules=RuleSet.RoadChess;else if(m==GameMode.RoadChess)cfg.Rules=rules;});
  screen=Screen_.Title;
 }
 void Pick<T>(ref T value,T[] options,float x,ref float y,float pw,string heading,Func<T,string> name){
  GUI.Label(new Rect(x,y,pw,22),heading,small);y+=22;float bw=pw/options.Length;
  for(int i=0;i<options.Length;i++){GUI.color=EqualityComparer<T>.Default.Equals(value,options[i])?new Color(.7f,1f,.9f):Color.white;if(GUI.Button(new Rect(x+i*bw,y,bw-4,40),name(options[i]),button))value=options[i];}
  GUI.color=Color.white;y+=48;
 }
 void DrawSubmenu(float x,float y,float pw){
  var m=pending;var cfg=ModeCatalog.For(m);GUI.Label(new Rect(x,y,pw,30),cfg.Title,label);GUI.Label(new Rect(x,y+28,pw,44),cfg.Blurb,sub);y+=80;
  if(m==GameMode.PassengerChess||m==GameMode.RoadChess){
   Pick(ref difficulty,new[]{Difficulty.Easy,Difficulty.Normal,Difficulty.Hard,Difficulty.Brutal},x,ref y,pw,"CAR DIFFICULTY",v=>v.ToString().ToUpper());
   var ruleOptions=m==GameMode.PassengerChess?new[]{RuleSet.Standard,RuleSet.RoadChess,RuleSet.CyberRoadChess}:new[]{RuleSet.RoadChess,RuleSet.CyberRoadChess};
   if(Array.IndexOf(ruleOptions,rules)<0)rules=ruleOptions[ruleOptions.Length-1];
   Pick(ref rules,ruleOptions,x,ref y,pw,"RULES",v=>ModeCatalog.RuleNames[(int)v]);
  }else if(m==GameMode.Story){
   Pick(ref difficulty,new[]{Difficulty.Easy,Difficulty.Normal,Difficulty.Hard,Difficulty.Brutal},x,ref y,pw,"HOW WELL THE CAR PLAYS THE RIDE GAME (A FULL GAME OF CHESS, PLAYED ACROSS THE RIDE)",v=>v.ToString().ToUpper());
  }else if(m==GameMode.AiSimulation||m==GameMode.CarMaster){
   Pick(ref skill,new[]{CarSkill.Casual,CarSkill.Smart,CarSkill.Expert,CarSkill.Relentless},x,ref y,pw,m==GameMode.CarMaster?"AI PASSENGER SKILL":"AI SKILL",v=>v.ToString().ToUpper());
  }
  if(GUI.Button(new Rect(x,y+8,pw*.62f,52),"START",button))Launch(m);
  if(GUI.Button(new Rect(x+pw*.64f,y+8,pw*.36f,52),"BACK",button))screen=Screen_.Title;
 }
 string room="";string serverUrl="",playerName="";bool urlInit,hostAsCar;
 void DrawNetwork(float x,float y,float pw){
  var d=director;var m=pending;var cfg=ModeCatalog.For(m);var net=d.Roles.EnsureNet();
  if(!urlInit){serverUrl=NetClient.ServerUrl;playerName=PlayerPrefs.GetString("cm55.name","Passenger");urlInit=true;}
  GUI.Label(new Rect(x,y,pw,30),cfg.Title+"  ·  ONLINE ROOM",label);
  GUI.Label(new Rect(x,y+28,pw,50),cfg.Blurb+(m==GameMode.CoopEscape?"\nBoth passengers control ONE White side.":"\nOne plays the passenger, one plays the car."),sub);y+=90;
  switch(net.Status){
   case NetStatus.Offline:case NetStatus.Failed:{
    GUI.Label(new Rect(x,y,pw,22),"ROOM SERVER",small);y+=22;serverUrl=Field(new Rect(x,y,pw,38),serverUrl,"Room server address (ws://host:8080)",120,false);y+=44;
    GUI.Label(new Rect(x,y,pw,22),"YOUR NAME",small);y+=22;playerName=Field(new Rect(x,y,pw*.5f,38),playerName,"Your name",14,false);
    if(GUI.Button(new Rect(x+pw*.52f,y,pw*.48f,38),"CONNECT",button)){PlayerPrefs.SetString("cm55.server",serverUrl);PlayerPrefs.SetString("cm55.name",playerName);PlayerPrefs.Save();net.Connect(serverUrl,playerName);}
    y+=48;
    if(net.Status==NetStatus.Failed)GUI.Label(new Rect(x,y,pw,40),"Could not connect: "+net.Error+"\nStart the server with:  node Tools/Server/server.cjs",warn);
    y+=46;
    if(m==GameMode.CoopEscape&&GUI.Button(new Rect(x,y,pw,44),"PLAY OFFLINE · AI PARTNER (TACTICAL ASSIST)",button))Launch(m);
    if(m==GameMode.DriverlessDuel)GUI.Label(new Rect(x,y,pw,50),"A duel needs a second player online. To be the car against an AI passenger, choose CAR MASTER from the main menu.",sub);
    y+=52;
    break;}
   case NetStatus.Connecting:GUI.Label(new Rect(x,y,pw,30),"Connecting to "+serverUrl+" …",sub);y+=40;break;
   case NetStatus.Connected:{
    if(m==GameMode.CoopEscape)Pick(ref coop,new[]{CoopControl.Shared,CoopControl.Alternating,CoopControl.TacticalAssist},x,ref y,pw,"CO-OP CONTROL",v=>v==CoopControl.Shared?"SHARED":v==CoopControl.Alternating?"ALTERNATING":"TACTICAL ASSIST");
    else{GUI.Label(new Rect(x,y,pw,22),"YOU HOST AS",small);y+=22;if(GUI.Button(new Rect(x,y,pw/2-2,40),(hostAsCar?"":"• ")+"PASSENGER",button))hostAsCar=false;if(GUI.Button(new Rect(x+pw/2+2,y,pw/2-2,40),(hostAsCar?"• ":"")+"CAR",button))hostAsCar=true;y+=48;}
    Pick(ref skill,new[]{CarSkill.Casual,CarSkill.Smart,CarSkill.Expert,CarSkill.Relentless},x,ref y,pw,m==CoopEscape()?"VEHICLE AI SKILL":"AI SKILL (USED IF A PLAYER DROPS)",v=>v.ToString().ToUpper());
    GUI.Label(new Rect(x,y,pw,22),"ROOM CODE",small);y+=22;room=Field(new Rect(x,y,pw*.5f,40),room,"Room code",6,true);
    if(GUI.Button(new Rect(x+pw*.52f,y,pw*.24f,40),"HOST",button))net.CreateRoom(m==GameMode.CoopEscape?"coop":"duel",coop==CoopControl.Shared?"shared":coop==CoopControl.Alternating?"alternating":"assist",hostAsCar?"C":"P",ModeConfig.SkillLevel(skill));
    if(GUI.Button(new Rect(x+pw*.77f,y,pw*.23f,40),"JOIN",button)&&room.Length>=4)net.JoinRoom(room);
    y+=48;break;}
   case NetStatus.InRoom:{
    GUI.Label(new Rect(x,y,pw,50),"ROOM  "+net.Code,C(title,size:34));y+=56;
    GUI.Label(new Rect(x,y,pw,22),"Share this code. You are "+(net.Slot=="A"?"PASSENGER A":net.Slot=="B"?"PASSENGER B":net.Slot=="P"?"the PASSENGER":"the CAR")+".",small);y+=26;
    foreach(var r in net.Roster){GUI.Label(new Rect(x,y,pw,24),(r.host?"★ ":"   ")+r.name.ToUpper()+"   ["+(r.slot=="C"?"CAR":r.slot=="P"?"PASSENGER":"PASSENGER "+r.slot)+"]",C(sub,a:(int)TextAnchor.MiddleLeft));y+=26;}
    y+=6;
    if(net.Host){bool ready=net.Roster.Length>=2;GUI.enabled=ready;if(GUI.Button(new Rect(x,y,pw*.62f,50),ready?"START MATCH":"WAITING FOR A SECOND PLAYER…",button))net.StartMatch();GUI.enabled=true;}
    else GUI.Label(new Rect(x,y,pw*.62f,50),"Waiting for the host to start…",sub);
    y+=58;break;}
   case NetStatus.Started:GUI.Label(new Rect(x,y,pw,30),"Match starting…",sub);y+=40;break;
  }
  if(!string.IsNullOrEmpty(net.Error)&&net.Status!=NetStatus.Failed)GUI.Label(new Rect(x,y,pw,24),net.Error,warn);
  if(GUI.Button(new Rect(x,y+8,pw*.36f,44),net.Connected?"LEAVE":"BACK",button)){if(net.Connected)net.Disconnect();screen=Screen_.Title;}
 }
 static GameMode CoopEscape()=>GameMode.CoopEscape;
 string Field(Rect r,string value,string label,int max,bool upper){
  string v;
  if(BrowserPlatform.Mobile){if(GUI.Button(r,string.IsNullOrEmpty(value)?"(tap to type)":value,button))value=BrowserPlatform.Prompt(label,value)??value;v=value;}
  else v=GUI.TextField(r,value,max,button);
  if(v.Length>max)v=v.Substring(0,max);
  return upper?v.ToUpperInvariant():v;
 }
 // ---------------------------------------------------------------------------------------------------------------- hud
 void DrawHud(float w,float h){
  var d=director;var g=d.game;bool carView=d.Roles!=null&&d.Roles.LocalIsCar;       // the car sees sensors and alerts, never the passenger's notes, items or objectives
  float top=70;
  if(d.Mode.Mode==GameMode.Calibration&&!d.Modes.CalibrationComplete){
   float bw=Mathf.Min(520,w-150);GUI.DrawTexture(new Rect((w-bw)/2,10,bw,34),panel);GUI.Label(new Rect((w-bw)/2,10,bw,34),"CALIBRATION RIDE · "+(d.Modes.LessonIndex+1)+" / "+d.Modes.LessonCount+" · "+d.Modes.LessonTitle,label);
   GUI.color=new Color(.4f,1f,.8f);GUI.DrawTexture(new Rect((w-bw)/2,44,bw*(d.Modes.LessonIndex/(float)Mathf.Max(1,d.Modes.LessonCount)),3),white);GUI.color=Color.white;top=56;
  }
  if(d.Modes.CalibrationComplete&&d.Mode.Mode==GameMode.Calibration)DrawCalibrationDone(w,h);
  // run clock (Story) and impact clock (final)
  if(d.Mode.Mode==GameMode.Story||d.Mode.Mode==GameMode.CoopEscape){
   GUI.Label(new Rect(8,8,200,22),"RUN "+Clock(d.StoryClock)+(d.Records.BestStoryMs>0?"  ·  BEST "+Records.Clock(d.Records.BestStoryMs):""),C(small,a:(int)TextAnchor.MiddleLeft));
  }
  if(d.RideGame!=null&&d.RideGame.Enabled&&!d.Final.Ended){
   float chipW=Mathf.Min(360,w-190);var chip=new Rect(8,32,chipW,28);
   if(carView)GUI.Label(chip,d.RideGame.Status,C(small,a:(int)TextAnchor.MiddleLeft));
   else if(GUI.Button(chip,d.RideGame.Status+"   [G]",button))d.RideGame.Open();
  }
  if(d.Final!=null&&d.Final.Active&&!d.Final.Ended){
   float left=d.Final.ImpactLeft;bool urgent=left<30;
   var st=C(big,size:44,col:urgent?new Color(1f,.2f,.15f):new Color(1f,.55f,.4f));
   GUI.Label(new Rect((w-300)/2,6,300,26),"COLLISION COURSE",small);GUI.Label(new Rect((w-300)/2,24,300,54),"IMPACT: "+Clock(left),st);top=84;
   if(!carView)DrawEscape(8,100);
  }
  if(g.Ride!=null&&d.Police.Active&&d.Police.Visible){GUI.Label(new Rect(w-250,52,238,22),"POLICE "+Mathf.RoundToInt(d.Police.Distance)+" m  ·  PRESSURE "+Mathf.RoundToInt(d.Police.Pressure*100)+"%",C(small,a:(int)TextAnchor.MiddleRight,col:new Color(.6f,.8f,1f)));}
  // contests: what the car is holding and how close the passenger is to winning it back
  float cy=h-118;foreach(var s in d.Contests.OpenSystems){GUI.Label(new Rect(8,cy,260,20),"CAR HOLDS · "+s.ToUpper()+"  "+d.Contests.Progress(s)+"/3",C(small,a:(int)TextAnchor.MiddleLeft,col:new Color(1f,.6f,.35f)));cy-=20;}
  // items in hand
  if(!carView&&g.Inventory.Items.Count>0){if(g.Inventory.Items.Count!=heldCount){heldCount=g.Inventory.Items.Count;heldText="HELD: "+string.Join(" · ",g.Inventory.Items.Select(i=>i.Replace('-',' ').ToUpper()));}GUI.Label(new Rect(8,h-98,320,20),heldText,C(small,a:(int)TextAnchor.MiddleLeft));}
  if(!carView&&GUI.Button(new Rect(8,h-72,92,26),"NOTES ("+d.Clues.All.Count+")",button)){g.TogglePause();OpenNotes();}
  // car mood tag: the vehicle's personality is part of the fiction, shown quietly
  if(d.Hostile||d.Mode.Mode==GameMode.Story&&g.State.Has("lockdown"))GUI.Label(new Rect(w-250,8,238,22),"VEHICLE · "+MoodModel.Name(d.Mood),C(small,a:(int)TextAnchor.MiddleRight,col:Color.Lerp(new Color(.6f,.9f,.9f),new Color(1f,.4f,.3f),(int)d.Mood/5f)));
 }
 void DrawEscape(float x,float y){
  var d=director;if(!d.Escape.Active)return;
  GUI.DrawTexture(new Rect(x,y,300,100),panel);GUI.Label(new Rect(x+8,y+2,284,20),"PASSENGER 04 · UNFINISHED SEQUENCE",C(small,a:(int)TextAnchor.MiddleLeft));
  if(d.Has("clue.finalnote"))GUI.Label(new Rect(x+8,y+22,284,78),d.Escape.Checklist(),C(small,a:(int)TextAnchor.UpperLeft,size:12));
  else GUI.Label(new Rect(x+8,y+22,284,70),"No notes found. Is there something on the phone?",C(small,a:(int)TextAnchor.UpperLeft,size:12));
 }
 void DrawCalibrationDone(float w,float h){
  var d=director;float pw=Mathf.Min(520,w-30),x=(w-pw)/2,y=h*.32f;GUI.DrawTexture(new Rect(x,y,pw,230),dark);
  GUI.Label(new Rect(x,y+8,pw,46),"CALIBRATION COMPLETE.",C(title,size:30));GUI.Label(new Rect(x+16,y+56,pw-32,60),"You can now use every system in the car: focus, hands, seats, climate, radio, compartments, the board, Road Chess, Cyber abilities, cards and hazards.",sub);
  if(GUI.Button(new Rect(x+16,y+130,pw*.5f-24,48),"PLAY STORY",button)){PlayerPrefs.SetInt("cm55.autostart",(int)GameMode.Story+1);director.game.Restart();}
  if(GUI.Button(new Rect(x+pw*.5f+8,y+130,pw*.5f-24,48),"MAIN MENU",button))director.game.Restart();
 }
 // ---------------------------------------------------------------------------------------------------------------- Passenger / Road chess panel
 void DrawChessPanel(float w,float h){
  var d=director;var g=d.game;float bw=170,x=w-bw-8,y=88;
  GUI.DrawTexture(new Rect(x-6,y-6,bw+12,g.Chess.Active?188:222),panel);
  GUI.Label(new Rect(x,y,bw,22),d.Mode.Title,label);y+=24;
  if(!g.Chess.Active||g.Chess.Snapshot!=null&&g.Chess.Snapshot.over){
   var dm=d.Modes;
   if(GUI.Button(new Rect(x,y,bw,32),"DIFFICULTY: "+dm.PassengerDifficulty.ToString().ToUpper(),button))dm.PassengerDifficulty=(Difficulty)(((int)dm.PassengerDifficulty+1)%4);y+=36;
   if(d.Mode.Mode==GameMode.PassengerChess&&GUI.Button(new Rect(x,y,bw,32),ModeCatalog.RuleNames[(int)dm.PassengerRules],button))dm.PassengerRules=(RuleSet)(((int)dm.PassengerRules+1)%3);
   else if(d.Mode.Mode==GameMode.RoadChess&&GUI.Button(new Rect(x,y,bw,32),ModeCatalog.RuleNames[(int)dm.PassengerRules],button))dm.PassengerRules=dm.PassengerRules==RuleSet.RoadChess?RuleSet.CyberRoadChess:RuleSet.RoadChess;
   y+=36;
   if(g.Chess.Active&&g.Chess.Snapshot!=null&&g.Chess.Snapshot.over){GUI.Label(new Rect(x,y,bw,40),g.Chess.ResultText(),warn);y+=44;if(GUI.Button(new Rect(x,y,bw,36),"NEW GAME",button)){g.Chess.Cancel();d.StartCoroutine(Later(.2f+1.6f,()=>d.Modes.NewGame()));}}
   else if(GUI.Button(new Rect(x,y,bw,36),"NEW GAME",button))d.Modes.NewGame();
  }
 }
 System.Collections.IEnumerator Later(float s,Action a){yield return new WaitForSeconds(s);a();}
 // ---------------------------------------------------------------------------------------------------------------- notes
 public void DrawNotes(float w,float h){
  var d=director;float pw=Mathf.Min(560,w-24),x=(w-pw)/2;
  GUI.Label(new Rect(x,16,pw,40),"NOTES & RECORDS",C(title,size:28));
  var view=new Rect(x,64,pw,h-170);GUI.DrawTexture(view,panel);
  var rect=new Rect(0,0,pw-26,Mathf.Max(h,60+d.Clues.All.Count*130));notesScroll=GUI.BeginScrollView(view,notesScroll,rect);
  float y=6;
  if(d.Clues.All.Count==0){GUI.Label(new Rect(8,y,pw-40,40),"Nothing yet. Clues you read are kept here.",body);y+=44;}
  foreach(var c in d.Clues.All){GUI.Label(new Rect(8,y,pw-40,22),c.Title,C(label,a:(int)TextAnchor.MiddleLeft,col:new Color(.6f,1f,.9f)));float th=body.CalcHeight(new GUIContent(c.Text),pw-44);GUI.Label(new Rect(8,y+22,pw-44,th),c.Text,body);y+=th+34;}
  var r=d.Records;y+=10;GUI.Label(new Rect(8,y,pw-40,22),"RECORDS",C(label,a:(int)TextAnchor.MiddleLeft,col:new Color(1f,.8f,.4f)));y+=24;
  GUI.Label(new Rect(8,y,pw-44,120),"Best ride "+Records.Clock(r.BestStoryMs)+"\nBest final confrontation "+Records.Clock(r.BestFinalMs)+"\nEndings seen "+r.EndingCount+" / 5\nEscapes "+r.Escapes+"\nCalibration "+(r.CalibrationDone?"complete":"not yet")+"\nPreferred radio preset "+r.PreferredPreset,body);
  GUI.EndScrollView();
  if(GUI.Button(new Rect(x,h-90,pw,48),"BACK",button)){CloseNotes();}
 }
 // ---------------------------------------------------------------------------------------------------------------- ending
 void DrawEnding(float w,float h){
  var d=director;var f=d.Final;GUI.DrawTexture(new Rect(0,0,w,h),f.Ending==Ending.Destination?white:dark);
  bool lose=f.Ending==Ending.Destination;var ink=lose?new Color(.05f,.05f,.06f):new Color(.85f,.95f,.95f);
  float pw=Mathf.Min(680,w-24),x=(w-pw)/2,y=h*.18f;
  GUI.Label(new Rect(x,y,pw,70),f.Headline,C(title,size:40,col:ink));
  GUI.Label(new Rect(x,y+76,pw,60),f.Epilogue,C(sub,col:ink));
  string ending=f.Ending==Ending.Checkmate?"ENDING · CHECKMATE":f.Ending==Ending.Cheater?"ENDING · CHEATER":f.Ending==Ending.PreviousPassenger?"ENDING · PREVIOUS PASSENGER":f.Ending==Ending.SystemEscape?"ENDING · SYSTEM ESCAPE":"ENDING · DESTINATION";
  GUI.Label(new Rect(x,y+140,pw,26),ending,C(label,col:ink));
  GUI.Label(new Rect(x,y+170,pw,60),"RIDE "+Records.Clock(f.StoryMs)+(f.FinalMs>0?"  ·  FINAL "+Records.Clock(f.FinalMs):"")+"   ·   ENDINGS FOUND "+d.Records.EndingCount+" / 5"+(Ending_IsBest(f)?"   ·   NEW BEST":""),C(small,col:ink));
  if(GUI.Button(new Rect(x,y+250,pw*.48f,56),"RIDE AGAIN",button)){PlayerPrefs.SetInt("cm55.autostart",(int)d.Mode.Mode+1);d.game.Restart();}
  if(GUI.Button(new Rect(x+pw*.52f,y+250,pw*.48f,56),"MAIN MENU",button)){PlayerPrefs.DeleteKey("cm55.autostart");d.game.Restart();}
 }
 static bool Ending_IsBest(OverrideFinal f)=>f.Ending!=Ending.Destination&&f.StoryMs>0;

 // ---------------------------------------------------------------------------------------------------------------- roles
 CarAction? carTool;string carToolName="";string carSel="";string carMsg="";float carMsgUntil;
 static readonly string[] CarNames={"RADIO HIJACK","WINDOW CLOSE","TINT UP","CLIMATE CUT","LOCK CYCLE","SEAT ADJUST","FORCED LANE CHANGE","HAZARD GENERATE","ROUTE MANIPULATE","EXTREME ACCELERATION"};
 void DrawRoles(float w,float h){
  var d=director;var r=d.Roles;if(r==null||!r.Active)return;
  var centre=C(small,size:14);
  if(!string.IsNullOrEmpty(r.Banner)&&d.Mode.Mode==GameMode.CoopEscape){float bw=Mathf.Min(560,w-170);GUI.DrawTexture(new Rect((w-bw)/2,50,bw,26),panel);GUI.Label(new Rect((w-bw)/2,50,bw,26),r.Banner,centre);}
  string note=r.NoticeLine;if(note.Length>0){float bw=Mathf.Min(520,w-40);GUI.DrawTexture(new Rect((w-bw)/2,h-124,bw,30),panel);GUI.Label(new Rect((w-bw)/2,h-124,bw,30),note,centre);}
  if(d.Mode.Mode==GameMode.AiSimulation)DrawSim(w,h);
  if(d.Mode.Mode==GameMode.CarMaster||r.LocalIsCar)DrawCarConsole(w,h);
 }
 void DrawSim(float w,float h){
  var r=director.Roles;float x=8,y=66;
  GUI.DrawTexture(new Rect(x,y,250,26),panel);GUI.Label(new Rect(x+6,y,160,26),"AI SIMULATION  ×"+r.SimSpeed,C(small,a:(int)TextAnchor.MiddleLeft));
  float[] speeds={1,2,4,8};for(int i=0;i<4;i++)if(GUI.Button(new Rect(x+130+i*30,y+2,28,22),"×"+speeds[i],button))r.SetSimSpeed(speeds[i]);
  int n=Mathf.Min(r.SimLog.Count,7);for(int i=0;i<n;i++)GUI.Label(new Rect(x,y+30+i*18,330,18),r.SimLog[r.SimLog.Count-n+i],C(small,a:(int)TextAnchor.MiddleLeft,size:11));
 }
 void DrawCarConsole(float w,float h){
  var d=director;var r=d.Roles;float pw=Mathf.Min(310,w*.44f),x=w-pw-8,y=60;
  GUI.DrawTexture(new Rect(x,y,pw,h-190),panel);
  GUI.Label(new Rect(x,y+2,pw,22),"YOU ARE THE CAR",label);y+=28;
  float auth=r.AuthorityNow;GUI.color=new Color(.1f,.2f,.25f);GUI.DrawTexture(new Rect(x+10,y,pw-20,14),white);GUI.color=Color.Lerp(new Color(1f,.4f,.25f),new Color(.4f,1f,.8f),auth/SystemAuthority.Max);GUI.DrawTexture(new Rect(x+10,y,(pw-20)*auth/SystemAuthority.Max,14),white);GUI.color=Color.white;
  GUI.Label(new Rect(x,y-1,pw,16),"SYSTEM AUTHORITY "+Mathf.FloorToInt(auth),C(small,size:11));y+=22;
  float bw=(pw-26)/2,bh=44;var actions=(CarAction[])Enum.GetValues(typeof(CarAction));
  for(int i=0;i<actions.Length;i++){
   var a=actions[i];string why=r.CarWhy(a);var rect=new Rect(x+10+(i%2)*(bw+6),y+(i/2)*(bh+4),bw,bh);
   GUI.enabled=why.Length==0;
   if(GUI.Button(rect,CarNames[i]+"  ·  "+SystemAuthority.Cost(a)+(why.Length>0?"\n"+why:""),C(button,size:11))){string msg=r.TryCar(a);if(msg.Length>0){carMsg=msg;carMsgUntil=Time.unscaledTime+2.5f;}}
   GUI.enabled=true;
  }
  y+=5*(bh+4)+6;
  if(Time.unscaledTime<carMsgUntil)GUI.Label(new Rect(x,y,pw,20),"REFUSED: "+carMsg,warn);y+=22;
  GUI.Label(new Rect(x+10,y,pw-20,18),"SENSORS",C(small,a:(int)TextAnchor.MiddleLeft));y+=18;
  for(int i=Mathf.Max(0,r.Alerts.Count-4);i<r.Alerts.Count;i++){GUI.Label(new Rect(x+10,y,pw-20,34),r.Alerts[i],C(small,a:(int)TextAnchor.UpperLeft,size:11,col:new Color(1f,.6f,.35f)));y+=30;}
  if(r.Alerts.Count==0)GUI.Label(new Rect(x+10,y,pw-20,20),"No unauthorised activity.",C(small,a:(int)TextAnchor.MiddleLeft,size:11));
  DrawCarBoard(w,h);
 }
 // The car's board: it plays BLACK. It can only choose among the legal actions the engine offers.
 void DrawCarBoard(float w,float h){
  var g=director.game;var chess=g.Chess;if(!chess.Active||chess.Snapshot==null)return;
  float cell=Mathf.Min(34,(h-260)/8f),bx=8,by=(h-cell*8)/2-10;
  bool mine=chess.AwaitingCar;var moves=chess.CarMoves;
  GUI.DrawTexture(new Rect(bx-4,by-26,cell*8+8,cell*8+54),panel);
  GUI.Label(new Rect(bx,by-24,cell*8,20),mine?"YOUR MOVE · BLACK":"WHITE IS THINKING…",C(small,size:12));
  var targets=carSel!=""?moves.Where(m=>m.from==carSel).Select(m=>m.to).Distinct().ToArray():new string[0];
  var pieces=new Dictionary<string,ChessPiece>();if(chess.Snapshot.pieces!=null)foreach(var p in chess.Snapshot.pieces)pieces[p.square]=p;
  for(int rank=0;rank<8;rank++)for(int file=0;file<8;file++){
   string sq=""+(char)('a'+file)+(char)('8'-rank);var rect=new Rect(bx+file*cell,by+rank*cell,cell,cell);
   bool dark=(file+rank)%2==1;GUI.color=sq==carSel?new Color(.9f,.75f,.2f):Array.IndexOf(targets,sq)>=0?new Color(.3f,.9f,.6f):dark?new Color(.32f,.4f,.44f):new Color(.62f,.7f,.72f);GUI.DrawTexture(rect,white);GUI.color=Color.white;
   ChessPiece pc;if(pieces.TryGetValue(sq,out pc)){string t=pc.type.ToUpper();GUI.Label(rect,t+(pc.shield?"*":"")+(pc.emp?"!":""),C(label,size:(int)(cell*.55f),fs:(int)FontStyle.Bold,col:pc.color=="w"?new Color(.95f,.97f,1f):new Color(.12f,.1f,.1f)));}
   if(mine&&GUI.Button(rect,"",GUIStyle.none))CarTap(sq,moves,chess);
  }
  float ty=by+cell*8+4;
  if(mine&&chess.challenge.carCyber){
   if(GUI.Button(new Rect(bx,ty,cell*4-2,22),"FIREWALL",button)){carToolName="firewall";carSel="";}
   if(GUI.Button(new Rect(bx+cell*4,ty,cell*4-2,22),"EMP SHOCK",button)){carToolName="emp";carSel="";}
   if(carToolName!="")GUI.Label(new Rect(bx,ty+22,cell*8,16),"tap a square for "+carToolName.ToUpper(),C(small,size:11));
  }
 }
 void CarTap(string sq,ChessMove[] moves,ChessController chess){
  if(carToolName!=""){chess.SubmitCarCyber(carToolName,sq);carToolName="";return;}
  if(carSel!=""&&moves.Any(m=>m.from==carSel&&m.to==sq)){var mv=moves.Where(m=>m.from==carSel&&m.to==sq&&m.kind!="overdrive").OrderBy(m=>m.promotion=="q"?0:1).FirstOrDefault();carSel="";if(mv!=null)chess.SubmitCarMove(mv.kind,mv.from,mv.to,mv.promotion);return;}
  carSel=moves.Any(m=>m.from==sq)?sq:"";
 }
}
}
