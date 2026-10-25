using System;
using System.Collections;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// The end of the route: Cyber Chess escalation during the pursuit, then the 3:00 COLLISION COURSE on an authored midgame position.
// Five ways out: CHECKMATE, CHEATER (a forbidden rule breaks the car), PREVIOUS PASSENGER (their unfinished sequence), SYSTEM ESCAPE
// (the systems the car took, taken back, plus the police) and DESTINATION (impact or checkmate against you).
public sealed class OverrideFinal {
 readonly OverrideDirector d;SceneManager game=>d.game;
 ChessChallenge cyber,final;
 public bool Active{get;private set;}                // the collision course is running
 public bool Ended{get;private set;}
 public bool UsedHiddenRule=>hidden;
 public bool Stopped{get;private set;}
 public Ending Ending{get;private set;}
 public float CruiseMph{get;private set;}
 public float Fade{get;private set;}                 // white-out used by DESTINATION
 public bool ScreenReady{get;private set;}
 public float FinalClock{get;private set;}
 bool boardStarted;
 public float ImpactLeft=>game.Chess.Active&&final!=null&&game.Chess.challenge==final?game.Chess.TimeLeft:Mathf.Max(0,180f-FinalClock);
 float recoverAt,startDelay,boardTimer;bool hidden,collapse,wantsCollision,panicStarted;int panicTurns;float stopPending;int storyMs,finalMs;
 public OverrideFinal(OverrideDirector director){d=director;}
 public void Initialize(){
  cyber=Make("cyber","CYBER ESCALATION","r3k2r/ppp2ppp/2n5/3pP3/3P4/2N5/PPP2PPP/R3K2R w KQkq - 0 1",ChessObjective.UseCyber,
   "Break the car's route security. Use FIREWALL, EMP SHOCK and OVERDRIVE once each. One charge arrives every turn.",12);
  cyber.road=true;cyber.cyber=true;cyber.cards=false;cyber.hazards=true;cyber.opponent="normal";cyber.startCharges=3;cyber.rewardEvent="security.cyber";cyber.resultText="ROUTE SECURITY COMPROMISED";cyber.laneFollowsMove=false;cyber.checkBrakes=false;cyber.hintMove="";cyber.hintText="Firewall your knight, EMP the pawn on d5, then Overdrive a rook.";
  d.Escape.Complete+=()=>{if(Active&&!Ended)Finish(Ending.PreviousPassenger);};
 }
 ChessChallenge Make(string id,string title,string fen,ChessObjective objective,string guidance,int maxMoves){
  var c=ScriptableObject.CreateInstance<ChessChallenge>();c.id=id;c.title=title;c.FEN=fen;c.objectiveType=objective;c.guidanceText=guidance;c.maxMoves=maxMoves;c.resultText=title+" · COMPLETE";return c;
 }
 // ---------------------------------------------------------------------------------------------------------------- Cyber escalation
 public void StartCyberEscalation(){
  if(game.Chess.Active||Ended)return;
  if(d.Roles!=null&&d.Roles.Online&&!d.Roles.IsHost)return;
  d.Say(CarVoice.Line("chess-start",d.Mood,new Rng(game.RunSeed)));
  game.Chess.challenge=cyber;game.Chess.Begin();
 }
 public void OnChessCompleted(string id){
  if(id=="security.cyber"){wantsCollision=true;startDelay=5.5f;d.Say("ROUTE SECURITY COMPROMISED.");}
 }
 // ---------------------------------------------------------------------------------------------------------------- collision course
 void StartCollisionCourse(){
  if(Active||Ended)return;
  Active=true;FinalClock=0;CruiseMph=112;game.State.Flag("collision.started");game.State.Enter(RideState.ENDGAME);
  game.Ride.DistanceMiles=4.9f;d.Escape.Active=true;d.Say("COLLISION COURSE CONFIRMED.");game.Audio.Chime();d.Puzzles.ShowWarning("COLLISION COURSE CONFIRMED · IMPACT 03:00",8f);
  d.Sound.Heartbeat(true);
  startDelay=3.2f;boardTimer=0;
 }
 public ChessChallenge Lookup(string id){if(id=="cyber")return cyber;if(id=="final"){BuildFinal();return final;}return null;}
 public void EnsureBoard(){
  if(Ended)return;
  if(d.Roles!=null&&d.Roles.Online&&!d.Roles.IsHost)return;               // in a room the host opens shared boards
  // the ride game tray (if open) is put away first; its position is kept
  if(game.Chess.Active){if(final!=null&&game.Chess.challenge!=final&&!game.Chess.Busy)game.Chess.Abort();return;}
  BuildFinal();
  // a good ride game pays off here: material lead becomes Cyber charges and a second card
  int bonus=d.RideGame!=null?d.RideGame.BonusCharges():0;final.startCharges=1+bonus;final.startHand=1+(bonus>=2?1:0);
  if(bonus>0)d.Puzzles.ShowWarning("RIDE GAME ADVANTAGE · +"+bonus+" CYBER CHARGE"+(bonus>1?"S":""),5f);
  game.Chess.challenge=final;game.Chess.AiBudgetMs=260;game.Chess.Begin();
 }
 void BuildFinal(){
  if(final==null){
   final=Make("final","COLLISION COURSE",FinalPositions.Pick(game.RunSeed),ChessObjective.Checkmate,"CHECKMATE THE CAR BEFORE IMPACT. Lane and speed give you Road moves; every rule you found is legal here.",0);
   final.road=true;final.cyber=true;final.cards=true;final.hazards=true;final.carCyber=true;final.carCards=false;final.opponent="hard";final.startHand=1;final.startCharges=1;final.timeLimitSeconds=180;
   final.laneFollowsMove=false;final.checkBrakes=false;final.noRetry=true;final.rewardEvent="ending.checkmate";final.seed=game.RunSeed;final.hintText="Check at 90+ MPH makes the car brake.";final.hintMove="";
   final.pressureJson="[{\"on\":\"check\",\"do\":\"slow\",\"mph\":15},{\"on\":\"sacrifice\",\"piece\":\"r\",\"do\":\"brake\",\"mph\":25},{\"on\":\"sacrifice\",\"piece\":\"n\",\"do\":\"laneChange\"},{\"on\":\"sacrifice\",\"piece\":\"q\",\"do\":\"reroute\"}]";
  }
 }
 // ---------------------------------------------------------------------------------------------------------------- chess events
 public void OnChessEvent(ChessEvent e){
  if(!Active||Ended)return;
  switch(e.t){
   case "hardBrake":case "pressure":
    if(e.n>0){CruiseMph=Mathf.Clamp(e.n,30,140);recoverAt=Time.time+14f;game.Player.FovShake=2.5f;d.Sound.Thud();if(e.t=="hardBrake")d.Puzzles.ShowWarning("HARD BRAKE · "+e.n+" MPH",3f);}
    if(e.t=="pressure"&&e.s=="laneChange"){int idx=Array.IndexOf(RoadChessRules.LaneNames,e.a);if(idx>=0)game.Motion.lane=idx;}
    if(e.t=="pressure"&&e.s=="reroute"){game.Ride.DistanceMiles+=1.4f;d.Puzzles.ShowWarning("ROUTE RECALCULATED · +1.4 MI",3f);}
    break;
   case "move":
    if(e.side=="w"&&(e.s=="kingReversal"||e.s=="ghostPawn")){OnHiddenRule(e.s);}
    else if(e.side=="b"&&panicStarted){panicTurns++;if(panicTurns>=3&&!collapse){collapse=true;Finish(Ending.Cheater);}}
    break;
   case "check":if(e.side=="b"&&((int)(Time.time*10)%3==0))d.Say(CarVoice.Line("final",CarMood.Nervous,new Rng(game.RunSeed+(int)Time.time)),true);break;
  }
 }
 void OnHiddenRule(string rule){
  if(!hidden){hidden=true;game.State.Flag("rule.used-hidden");game.State.Flag("car.afraid");}
  if(!panicStarted){panicStarted=true;panicTurns=0;d.Say("No.");d.StartCoroutine(PanicLines());}
 }
 IEnumerator PanicLines(){
  string[] lines={"That rule does not exist.","Do not make that move.","Illegal. ILLEGAL.","System integrity — system integrity —"};
  for(int i=0;i<lines.Length&&!Ended;i++){yield return new WaitForSeconds(2.1f);d.Say(lines[i]);game.Audio.LockClick();game.Player.FovShake=1.5f;}
 }
 public void OnChessEnded(string winner,string reason){
  if(!Active||Ended||game.Chess.challenge!=final)return;
  if(winner=="w")Finish(hidden?Ending.Cheater:Ending.Checkmate);
  else if(winner=="b"||reason=="timeout")Finish(Ending.Destination);
  else Finish(Ending.Destination);          // a stalemate or draw is not an escape either
 }
 public void OnStopTap(float now){stopPending=0;}
 public void OnStopTriple(float now){stopPending=now;}
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void Update(float dt){
  if(!d.Mode.Hostile)return;
  if(Active&&!Ended){
   FinalClock+=dt;
   if(Time.time>recoverAt&&CruiseMph<112)CruiseMph=Mathf.MoveTowards(CruiseMph,112,dt*8);
   if(stopPending>0&&Time.time-stopPending>=2.5f){stopPending=0;d.Escape.Mark("escape.stop");}
   if(startDelay>0){startDelay-=dt;if(startDelay<=0){EnsureBoard();boardTimer=2.5f;}}
   else if(!game.Chess.Active&&!boardStarted){boardTimer-=dt;if(boardTimer<=0){boardTimer=2.5f;EnsureBoard();}}
   if(game.Chess.Active&&game.Chess.challenge==final)boardStarted=true;
   // impact without a board (the passenger retracted the tray): the clock still runs
   if(!game.Chess.Active&&FinalClock>=185f&&!Ended)Finish(Ending.Destination);
  }else if(wantsCollision){
   if(!game.Chess.Active){startDelay-=dt;if(startDelay<=0){wantsCollision=false;StartCollisionCourse();}}
  }
  if(Ended)UpdateEnding(dt);
 }
 // ---------------------------------------------------------------------------------------------------------------- endings
 public void Finish(Ending e){
  if(Ended||e==Ending.None)return;
  Ended=true;Ending=e;Active=false;d.Sound.Heartbeat(false);
  storyMs=(int)(d.StoryClock*1000);finalMs=(int)(FinalClock*1000);
  d.Records.RecordEnding(e,storyMs,finalMs);d.SaveRecords();d.Roles?.OnEnding(e);
  game.State.Flag("ending.any");game.State.Flag("ending."+e.ToString().ToLowerInvariant());
  if(game.Chess.Active)game.Chess.Abort();
  d.StartCoroutine(Play(e));
 }
 IEnumerator Play(Ending e){
  if(e==Ending.Destination){
   CruiseMph=140;d.Boost(30,5);d.Say(CarVoice.Line("final",CarMood.Afraid,new Rng(1)),false);
   for(float t=0;t<2.2f;t+=Time.deltaTime){game.Player.FovShake=Mathf.Lerp(0,9,t/2.2f);yield return null;}
   d.Sound.Boom();BrowserPlatform.Vibrate(400);
   for(float t=0;t<.6f;t+=Time.deltaTime){Fade=Mathf.Clamp01(t/.6f);yield return null;}Fade=1;d.Sound.Heartbeat(false);
   Stopped=true;yield return new WaitForSeconds(1.1f);
  }else{
   // the car gives up: emergency braking, CLICK, doors and windows free
   d.Puzzles.TintCar=0;d.Puzzles.TintPlayer=0;d.AudioJam=false;d.Radio.Locked=false;d.Climate.Frozen=false;
   if(e==Ending.Cheater){d.Say("No. No. No.",true);yield return new WaitForSeconds(1.2f);d.Say("System—",true);}
   else if(e==Ending.PreviousPassenger){game.Motion.lane=2;d.Say("Reroute accepted. Service lane.",false);yield return new WaitForSeconds(1.0f);}
   Stopped=true;game.Audio.Chime();
   yield return new WaitForSeconds(2.2f);
   for(int i=0;i<4;i++){game.Audio.LockClick();yield return new WaitForSeconds(.14f);}
   foreach(var pin in game.Ride.lockPins)pin.localPosition+=Vector3.up*.03f;
   game.Security.GrantWindowLease(600f);yield return new WaitForSeconds(1.0f);
   // the car has stopped: step out and look back at it
   for(float wait=0;game.Motion.speed>.8f&&wait<18f;wait+=Time.deltaTime)yield return null;
   if(d.Exterior!=null&&d.Exterior.Built)yield return d.Exterior.PlayExit();
  }
  yield return new WaitForSeconds(.4f);game.State.Enter(RideState.ENDING);ScreenReady=true;
 }
 void UpdateEnding(float dt){}
 public string Headline=>EndingRules.Title(Ending);
 public string Epilogue=>EndingRules.Epilogue(Ending);
 public string Time_=>Records.Clock(storyMs);
 public int StoryMs=>storyMs;public int FinalMs=>finalMs;
}
}
