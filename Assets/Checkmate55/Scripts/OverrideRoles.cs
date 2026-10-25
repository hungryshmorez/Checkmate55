using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// Who is playing which side. The world only ever sees intents, so a human, an AI and a networked partner are interchangeable:
//   * Car Master        human CAR (Authority-limited console) vs AI passenger driving the physical cabin
//   * AI Simulation     AI passenger vs AI car: the whole story plays itself (QA / spectate, with a speed control)
//   * Co-op Escape      two human passengers on ONE White side online; offline, a human plus an AI partner who supports
//   * Driverless Duel   human passenger vs human car online; the server charges the car's SYSTEM AUTHORITY
public sealed class OverrideRoles {
 readonly OverrideDirector d;SceneManager game=>d.game;
 public NetClient Net;
 public SystemAuthority Authority=new SystemAuthority();
 public bool Active{get;private set;}
 public bool PassengerIsAi{get;private set;}
 public bool LocalIsCar{get;private set;}
 public bool Spectator=>LocalIsCar||PassengerIsAi;
 public bool Online=>Net!=null&&Net.Status==NetStatus.Started;
 public bool IsHost=>!Online||Net.Host;
 public string Banner{get;private set;}="";
 public string Notice{get;private set;}="";float noticeUntil;
 public float SimSpeed{get;private set;}=1;
 public readonly List<string> Alerts=new List<string>();readonly List<float> alertAt=new List<float>();
 public readonly List<string> SimLog=new List<string>();
 public int PassengerActivity{get;private set;}
 AIPassengerController ai;Intent pending;float hold;string lastBeat="";float beatSince;float stuckWarn;
 readonly HashSet<string> fromNet=new HashSet<string>();
 float ctlAt,authBase=60,authStamp;readonly Dictionary<CarAction,float> usedAt=new Dictionary<CarAction,float>();
 NetTeam team;bool hooked;
 public OverrideRoles(OverrideDirector director){d=director;}
 public string NoticeLine=>Time.unscaledTime<noticeUntil?Notice:"";
 void Tell(string line,float seconds=5f){Notice=line;noticeUntil=Time.unscaledTime+seconds;}
 public NetClient EnsureNet(){if(Net==null)Net=NetClient.Create(d.gameObject);return Net;}
 // ---------------------------------------------------------------------------------------------------------------- begin
 public void Initialize(){
  if(hooked)return;hooked=true;
  d.Clues.Added+=OnClue;
 }
 public void Begin(ModeConfig m){
  var chess=game.Chess;
  Active=m.Mode==GameMode.CoopEscape||m.Mode==GameMode.DriverlessDuel||m.Mode==GameMode.CarMaster||m.Mode==GameMode.AiSimulation;
  PassengerIsAi=m.Passenger==ControllerKind.Ai;LocalIsCar=false;ai=null;pending=null;team=null;Banner="";SimSpeed=1;Time.timeScale=1;Alerts.Clear();alertAt.Clear();SimLog.Clear();PassengerActivity=0;lastBeat="";beatSince=0;
  chess.ChallengeLookup=d.FindChallenge;chess.Net=Online?Net:null;chess.HumanCar=false;chess.AiWhite=false;chess.AiAssist=false;
  game.Player.InputLocked=false;d.ThreatsEnabled=m.Hostile;
  Authority=new SystemAuthority();authBase=Authority.Value;authStamp=Time.time;usedAt.Clear();
  switch(m.Mode){
   case GameMode.AiSimulation:UseAiPassenger(m.Skill);break;
   case GameMode.CarMaster:LocalIsCar=true;d.ThreatsEnabled=false;chess.HumanCar=true;UseAiPassenger(m.Skill);break;
   case GameMode.CoopEscape:
    if(Online){d.ThreatsEnabled=Net.Host;Banner="WHITE TEAM · WAITING FOR THE BOARD";}
    else{chess.AiAssist=true;Banner="PASSENGER B (AI) SUPPORTS YOUR WHITE MOVES";}
    break;
   case GameMode.DriverlessDuel:
    d.ThreatsEnabled=false;
    if(Online&&Net.IsCar){LocalIsCar=true;chess.HumanCar=true;}
    break;
  }
  if(Spectator)game.Player.InputLocked=true;
  if(m.Mode==GameMode.AiSimulation){int sp=PlayerPrefs.GetInt("cm55.simspeed",1);if(sp>1)SetSimSpeed(sp);}
 }
 void UseAiPassenger(CarSkill skill){
  ai=new AIPassengerController(0,skill,null,game.RunSeed+77);
  game.Chess.AiWhite=true;game.Chess.AiWhiteLevel=skill==CarSkill.Casual?"easy":skill==CarSkill.Smart?"normal":skill==CarSkill.Expert?"hard":"brutal";
 }
 public void SetSimSpeed(float s){SimSpeed=Mathf.Clamp(s,1,8);Time.timeScale=game.Paused?0:SimSpeed;}
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void PumpLobby(){if(Net!=null)PumpNet();}
 public void Update(float dt){
  if(Net!=null)PumpNet();
  if(!Active)return;
  Authority.Tick(dt);
  for(int i=Alerts.Count-1;i>=0;i--)if(Time.time-alertAt[i]>14f){Alerts.RemoveAt(i);alertAt.RemoveAt(i);}
  if(PassengerIsAi)AiUpdate(dt);
  if(Online&&team!=null)Banner=TeamBanner(team);
 }
 // ---------------------------------------------------------------------------------------------------------------- AI passenger
 PassengerView BuildView(){
  var v=new PassengerView{Time=d.StoryClock};
  var beat=d.Mode.Hostile?d.Story.Current(d.Has):null;
  if(beat!=null){v.BeatId=beat.Id;v.Objective=beat.Objective;v.Hint=beat.Hint;}
  v.TargetControl=d.Mode.Hostile?(d.Story.Target(d.Has)??""):"";
  v.Dialogue=game.Dialogue.Visible;
  foreach(var c in d.Clues.All)v.Notes.Add(c.Title+"\n"+c.Text);
  foreach(var it in game.Inventory.Items)v.Held.Add(it);
  v.ChessActive=game.Chess.Active;v.MyChessTurn=game.Chess.Active&&game.Chess.Snapshot!=null&&game.Chess.Snapshot.turn==game.Chess.challenge.playerColor;
  v.Temp=d.Climate.Temp;v.Fan=d.Climate.Fan;v.PatternReady=d.Climate.PatternReady;
  v.Station=d.Radio.Station;v.Balance=d.Radio.Balance;v.Fader=d.Radio.Fader;v.Volume=d.Radio.Volume;v.Muted=d.Radio.Muted;
  v.Seat=game.Seats.Seat;v.TintAlpha=d.Puzzles.WindshieldAlpha;v.Yaw=game.Player.yaw;v.Pitch=game.Player.pitch;
  v.ControlsInReach=new[]{"climate","armrest","right-window","guidance-1"};
  return v;
 }
 void AiUpdate(float dt){
  if(ai==null||game.Paused||d.Final.Ended)return;
  var beat=d.Mode.Hostile?d.Story.Current(d.Has):null;string id=beat!=null?beat.Id:"";
  if(id!=lastBeat){if(lastBeat!=""||id!=""){string line=Mathf.RoundToInt(d.StoryClock)+"s  "+(id==""?"(end)":id);SimLog.Add(line);Debug.Log("[AI passenger] beat → "+line);}lastBeat=id;beatSince=0;stuckWarn=0;}
  beatSince+=dt;
  if(beatSince>stuckWarn+90f&&!game.Chess.Active){stuckWarn=beatSince;string line="STUCK "+Mathf.RoundToInt(beatSince)+"s on "+(id==""?"-":id)+" · ring "+(d.Story.Target(d.Has)??"-");SimLog.Add(line);Debug.LogWarning("[AI passenger] "+line);}
  if(pending!=null){hold-=dt;if(hold<=0&&!game.Seats.Moving){var p=pending;pending=null;Execute(p);}return;}
  // the ride game: the AI passenger opens the tray now and then, makes one move, and puts it away again
  var rg=d.RideGame;
  if(rg!=null&&rg.Enabled&&rg.challenge!=null){
   if(game.Chess.Active&&game.Chess.challenge==rg.challenge){if(!game.Chess.Busy&&rg.MovesThisOpen>=1)game.Chess.Cancel();return;}
   if(!game.Chess.Active&&!game.Seats.Moving&&rg.AiShouldPlay(Time.time,d.Mode.Skill)){
    if(game.Seats.Seat==3){game.Seats.Choose(1);return;}
    if(rg.Open())rg.NoteAiPlay(Time.time);return;
   }
  }
  if(game.Chess.Active||game.Seats.Moving)return;
  var intent=ai.Decide(BuildView(),dt);
  if(intent.Kind==IntentKind.Idle)return;
  if(intent.Kind==IntentKind.Interact){
   var c=game.Registry.Get(intent.ControlId);if(c==null)return;
   int seat=SeatFor(c);
   if(seat!=game.Seats.Seat){game.Seats.Choose(seat);pending=intent;hold=1.6f;return;}
  }
  Execute(intent);
 }
 void Execute(Intent i){
  switch(i.Kind){
   case IntentKind.Interact:{
    var c=game.Registry.Get(i.ControlId);if(c==null||!c.isActiveAndEnabled||!c.available)return;
    game.Player.FocusControl(c.transform);game.Interactions.UseControl(c);break;}
   case IntentKind.Look:game.Player.FocusAngles(i.Yaw,i.Pitch);break;
  }
 }
 // The seat from which a control is easiest to reach (rear row or the front passenger seat).
 int SeatFor(PhysicalControl c){
  var parent=game.Player.transform.parent;if(parent==null)return game.Seats.Seat;
  Vector3 head=game.Player.transform.localPosition-game.Seats.Offset;Vector3 target=parent.InverseTransformPoint(c.transform.position);
  int best=game.Seats.Seat;float bestD=float.MaxValue;
  for(int s=0;s<4;s++){
   Vector3 at=head+(s==3?new Vector3(.63f,0,1.69f):new Vector3((s-1)*.61f,0,0));float dist=Vector3.Distance(new Vector3(at.x,0,at.z),new Vector3(target.x,0,target.z))+(s==game.Seats.Seat?-.25f:0);
   if(dist<bestD){bestD=dist;best=s;}
  }
  return best;
 }
 // ---------------------------------------------------------------------------------------------------------------- the human car
 public float AuthorityNow=>Online?Mathf.Min(SystemAuthority.Max,authBase+(Time.time-authStamp)*SystemAuthority.RegenPerSecond):Authority.Value;
 public float CooldownLeft(CarAction a){
  if(!Online)return Authority.CooldownLeft(a);
  float t;return usedAt.TryGetValue(a,out t)?Mathf.Max(0,SystemAuthority.Cooldown(a)-(Time.time-t)):0;
 }
 public string CarWhy(CarAction a){
  if(CooldownLeft(a)>0)return "cooling "+Mathf.CeilToInt(CooldownLeft(a))+"s";
  if(AuthorityNow<SystemAuthority.Cost(a))return "needs "+SystemAuthority.Cost(a);
  if(a==CarAction.HazardGenerate&&(!game.Chess.Active||game.Chess.Snapshot==null||game.Chess.Snapshot.over||game.Chess.Snapshot.turn!=game.Chess.challenge.playerColor||!game.Chess.challenge.hazards))return "needs an open board";
  return "";
 }
 public string TryCar(CarAction a){
  if(!LocalIsCar)return "you are not the car";
  string why=CarWhy(a);if(why.Length>0)return why;
  if(Online){Net.SendCarAction(a.ToString());return "";}
  if(!Authority.Try(a))return Authority.Why(a);
  ApplyCar(a);return "";
 }
 // Every client (and the car itself) runs the same effect when the server announces it. Deterministic where it changes gameplay.
 public void ApplyCar(CarAction a){
  var p=d.Presenter;p.ForceHold=true;
  try{
   switch(a){
    case CarAction.RadioHijack:p.Execute(new ThreatAction{Kind=ThreatKind.RadioChange,Strength=2});break;
    case CarAction.WindowClose:p.Execute(new ThreatAction{Kind=ThreatKind.WindowClose,Strength=2});break;
    case CarAction.TintUp:p.Execute(new ThreatAction{Kind=ThreatKind.TintUp,Strength=2});break;
    case CarAction.ClimateCut:p.Execute(new ThreatAction{Kind=ThreatKind.ClimateShutdown,Strength=2});break;
    case CarAction.LockCycle:p.Execute(new ThreatAction{Kind=ThreatKind.LockCycle,Strength=1});break;
    case CarAction.SeatAdjust:p.Execute(new ThreatAction{Kind=ThreatKind.SeatCreep,Strength=2});break;
    case CarAction.ForcedLaneChange:game.Motion.lane=game.Motion.lane==1?0:1;d.Puzzles.ShowWarning("LANE CHANGE",3f);break;
    case CarAction.HazardGenerate:if(!Online){game.Chess.ExternalHazard(1);d.Puzzles.ShowWarning("ROAD EVENT · HAZARD WARNING ON THE TRAY",4f);}else d.Puzzles.ShowWarning("ROAD EVENT · HAZARD WARNING ON THE TRAY",4f);break;
    case CarAction.RouteManipulate:game.Ride.DistanceMiles+=1.8f;d.Puzzles.ShowWarning("ROUTE RECALCULATED",4f);break;
    case CarAction.ExtremeAcceleration:d.Boost(22f,12f);game.Player.FovShake=3f;break;
   }
  }finally{p.ForceHold=false;}
 }
 // The car never sees what the passenger found - only that something unauthorised happened, and roughly where.
 public void OnPassengerTouched(string controlId,Vector3 worldPos){
  PassengerActivity++;
  if(!LocalIsCar)return;
  if(!Sensitive(controlId))return;
  var parent=game.Player.transform.parent;Vector3 l=parent!=null?parent.InverseTransformPoint(worldPos):worldPos;
  string zone=(l.z>1.0f?"FRONT ":"REAR ")+(l.x<-.3f?"LEFT":l.x>.3f?"RIGHT":"CENTER");
  string line="UNAUTHORIZED ACTIVITY DETECTED: "+zone;
  if(Alerts.Count>0&&Alerts[Alerts.Count-1]==line)return;
  Alerts.Add(line);alertAt.Add(Time.time);if(Alerts.Count>5){Alerts.RemoveAt(0);alertAt.RemoveAt(0);}game.Audio.Chime();
 }
 static readonly string[] Ordinary={"climate","climate-plus","climate-minus","climate-fan","armrest","guidance-0","guidance-1","guidance-2","left-window","right-window","request-stop","authenticate"};
 static bool Sensitive(string id)=>!id.StartsWith("chess-")&&!id.StartsWith("radio-")&&Array.IndexOf(Ordinary,id)<0;
 // ---------------------------------------------------------------------------------------------------------------- hooks from the world
 public void OnTouched(PhysicalControl c){
  if(!Active)return;
  if(PassengerIsAi&&LocalIsCar)OnPassengerTouched(c.id,c.transform.position);
  if(Online&&!LocalIsCar&&Time.time-ctlAt>.4f){ctlAt=Time.time;Net.SendControl(c.id);}
 }
 public void OnFlag(string flag){
  if(!Online||LocalIsCar)return;
  if(fromNet.Remove(flag))return;
  Net.SendFlag(flag);
 }
 void OnClue(ClueBook.Clue clue){
  if(!Online||LocalIsCar)return;
  if(fromNet.Remove("clue.share."+clue.Id))return;
  Net.SendFlag(clue.Id=="mile"?"clue.share.mile."+d.Puzzles.LastMile.ToString("0.0"):"clue.share."+clue.Id);
 }
 public void OnThreat(ThreatAction a){
  if(!Online||!Net.Host||a==null)return;
  Net.SendThreat(new NetThreat{kind=a.Kind.ToString(),arg=a.Arg??"",strength=a.Strength,line=a.Line??""});
 }
 public void OnEnding(Ending e){
  if(Online&&Net.Host)Net.SendEnding(e.ToString());
 }
 // ---------------------------------------------------------------------------------------------------------------- network
 void PumpNet(){
  int guard=0;
  while(Net.Inbox.Count>0&&guard++<64){
   var m=Net.Inbox.Dequeue();
   try{Handle(m);}catch(Exception e){Debug.LogException(e);}
  }
 }
 static string TeamBanner(NetTeam t){
  if(t==null)return "";
  if(t.players<2)return "WHITE TURN";
  string who=t.@operator==-1?"BOTH PASSENGERS":"PASSENGER "+(char)('A'+t.@operator);
  string line="WHITE TEAM TURN  ·  "+who+(t.locked?" MOVED":" MOVES");
  if(t.mode=="assist")line+="  ·  B: SUPPORT"+(t.card||t.cyber?" USED":"");
  else line+="  ·  CARD "+(t.card?"USED":"READY")+"  CYBER "+(t.cyber?"USED":"READY");
  return line;
 }
 void Handle(NetMsg m){
  switch(m.t){
   case "start":StartFromNet(m);break;
   case "flag":ApplyRemoteFlag(m.flag);break;
   case "threat":if(m.threat!=null&&d.ThreatsEnabled==false){ThreatKind k;if(Enum.TryParse(m.threat.kind,out k))d.Presenter.Execute(new ThreatAction{Kind=k,Arg=m.threat.arg,Line=m.threat.line,Strength=m.threat.strength});}break;
   case "carAction":{
    CarAction a;if(Enum.TryParse(m.action,out a)){usedAt[a]=Time.time;authBase=m.authority;authStamp=Time.time;ApplyCar(a);if(!LocalIsCar)Alerts.Clear();}
    break;}
   case "ctl":if(!LocalIsCar&&m.by!=Net.Slot&&!string.IsNullOrEmpty(m.id)){var c=game.Registry.Get(m.id);Tell("PASSENGER "+m.by+" · "+(c!=null?c.label:m.id).ToUpper(),2.5f);}
    else if(LocalIsCar){var c2=game.Registry.Get(m.id);if(c2!=null)OnPassengerTouched(m.id,c2.transform.position);}break;
   case "ops":
    if(m.team!=null){team=m.team;}
    game.Chess.OnNetMessage(m);break;
   case "resync":case "chessEnd":game.Chess.OnNetMessage(m);break;
   case "deny":
    if(m.@ref=="chess"||m.@ref=="chessBegin")game.Chess.OnNetMessage(m);
    else if(m.@ref=="carAction"){authBase=m.authority>0?m.authority:authBase;authStamp=Time.time;Tell("REFUSED: "+(m.reason=="authority"?"not enough SYSTEM AUTHORITY":m.reason=="cooldown"?"still cooling down":m.reason),3f);}
    break;
   case "ending":{Ending e;if(Enum.TryParse(m.ending,out e))d.Final.Finish(e);break;}
   case "left":OnLeft(m);break;
  }
 }
 void StartFromNet(NetMsg m){
  var mode=m.mode=="coop"?GameMode.CoopEscape:GameMode.DriverlessDuel;
  game.RunSeed=(int)(m.seed&0x7fffffff);
  d.StartMode(mode,cfg=>{cfg.Coop=m.coop=="shared"?CoopControl.Shared:m.coop=="alternating"?CoopControl.Alternating:CoopControl.TacticalAssist;cfg.Humans=2;
   cfg.Skill=m.skill=="casual"?CarSkill.Casual:m.skill=="expert"?CarSkill.Expert:m.skill=="relentless"?CarSkill.Relentless:CarSkill.Smart;
   cfg.Difficulty=cfg.Skill==CarSkill.Casual?Difficulty.Easy:cfg.Skill==CarSkill.Expert?Difficulty.Hard:cfg.Skill==CarSkill.Relentless?Difficulty.Brutal:Difficulty.Normal;});
 }
 void ApplyRemoteFlag(string flag){
  if(string.IsNullOrEmpty(flag))return;
  if(flag.StartsWith("clue.share.")){AddSharedClue(flag.Substring(11));return;}
  if(d.Has(flag))return;
  fromNet.Add(flag);game.State.Flag(flag);
 }
 void AddSharedClue(string id){
  fromNet.Add("clue.share."+(id.StartsWith("mile.")?"mile":id));
  switch(id){
   case "maint":d.Clues.Add("maint","MAINTENANCE CARD",StoryText.MaintenanceCardFront+"\n\n"+StoryText.MaintenanceCardBack);break;
   case "route":d.Clues.Add("route","ROUTE CARD",StoryText.RouteCard);break;
   case "usb":d.Clues.Add("usb","USB STICK","A USB stick. A spanner is scratched into the casing.");break;
   case "phone":d.Clues.Add("phone","PASSENGER 04'S PHONE",StoryText.PhoneNote);break;
   case "scratch":d.Clues.Add("scratch","SCRATCHED NOTATION","70+  N : 3 × 1\nc2 → f3\nIT LIES.");break;
   case "recording1":d.Clues.Add("recording1","RECORDING · RIGHT SPEAKER","At 90+, a rook can cross one friendly piece into another lane. At 110+, the road itself changes the rules.");break;
   case "recording4":d.Clues.Add("recording4","HIDDEN TRANSMISSION · PRESET 4",StoryText.RadioRecording);break;
   case "final":d.Clues.Add("final","PASSENGER 04 / UNFINISHED",StoryText.FinalNote);break;
   default:if(id.StartsWith("mile.")){d.Clues.Add("mile","MILE MARKER","A55 · MILE "+id.Substring(5));}break;
  }
 }
 void OnLeft(NetMsg m){
  Tell(m.takeover=="partner"?"PASSENGER "+m.slot+" LEFT · AN AI PARTNER TAKES OVER":m.takeover=="car"?"THE CAR PLAYER LEFT · THE VEHICLE AI TAKES OVER":"THE OTHER PLAYER LEFT",8f);
  if(m.takeover=="partner"&&game.Chess!=null)game.Chess.AiAssist=true;
  if(m.takeover=="car"){game.Chess.HumanCar=false;d.ThreatsEnabled=Net.Host;}
  if(m.host&&Net.Mode=="coop")d.ThreatsEnabled=true;
  if(m.closed){Tell("MATCH CLOSED · "+(m.slot=="P"?"THE PASSENGER":"A PLAYER")+" LEFT",8f);}
 }
}
}
