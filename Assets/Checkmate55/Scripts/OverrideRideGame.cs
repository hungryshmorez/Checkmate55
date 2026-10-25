using System;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// The RIDE GAME (see Core/RideGame.cs): one full game of chess against the car, spread across the whole ride. This module owns the persistent
// challenge, the car's move clock (it plays a weak move for you if you leave it), the quota that gates the end of the route, and what a win or
// a loss of the game means.
public sealed class OverrideRideGame {
 readonly OverrideDirector d;SceneManager game=>d.game;
 public readonly RideGameModel Model=new RideGameModel();
 public ChessChallenge challenge;
 public bool Enabled{get;private set;}
 public Transform ControlRoot;public TextMesh Label;public Renderer Led;       // set by the builder
 int openedAtMoves;float labelClock,lastAiPlay=-999,forcedRetry;bool forcePending;
 public int MovesThisOpen=>Model.PlayerMoves-openedAtMoves;
 public OverrideRideGame(OverrideDirector director){d=director;}
 public void Initialize(){game.Chess.WhiteMoved+=OnWhite;game.Chess.Ended+=OnEnded;}
 public void Begin(ModeConfig m){
  Enabled=m.Hostile&&(m.Mode==GameMode.Story||m.Mode==GameMode.CoopEscape||m.Mode==GameMode.CarMaster||m.Mode==GameMode.AiSimulation);
  Model.Reset();forcePending=false;lastAiPlay=-999;openedAtMoves=0;
  if(ControlRoot!=null)ControlRoot.gameObject.SetActive(Enabled);
  challenge=Enabled?Build(m):null;
 }
 ChessChallenge Build(ModeConfig m){
  var c=ScriptableObject.CreateInstance<ChessChallenge>();
  c.id="ridegame";c.title="RIDE GAME";c.FEN=RideGameModel.StartFen;c.objectiveType=ChessObjective.Checkmate;c.maxMoves=0;
  c.guidanceText="A full game against the car, played across the whole ride. Make a move, retract the tray, solve something, come back.";
  c.road=true;c.cyber=true;c.cards=true;c.hazards=true;c.carCyber=true;c.carCards=false;c.startHand=1;c.startCharges=0;c.seed=game.RunSeed;
  c.opponent=m.Mode==GameMode.AiSimulation?ModeConfig.SkillLevel(m.Skill):m.Difficulty.ToString().ToLowerInvariant();
  c.noRetry=true;c.persistent=true;c.laneFollowsMove=false;c.checkBrakes=false;c.rewardEvent="ridegame.win";c.hintMove="";c.hintText="Develop your pieces, castle, and watch the warning tiles.";
  c.resultText="CHECKMATE · YOU WIN THE RIDE GAME";
  return c;
 }
 // ---------------------------------------------------------------------------------------------------------------- opening the tray
 public bool Open(){
  if(!Enabled||challenge==null)return false;
  if(Model.Over){d.Say("The ride game is over.",false);return false;}
  if(d.Final!=null&&d.Final.Active){d.Say("There is no time for the ride game now.",false);return false;}
  if(game.State.Current==RideState.BOARDING){d.Say("Boarding in progress.",false);return false;}
  if(game.Chess.Active){if(game.Chess.challenge==challenge)return true;d.Say("Retract the tray first, then open the ride game.",false);return false;}
  openedAtMoves=Model.PlayerMoves;game.Chess.challenge=challenge;game.Chess.AiBudgetMs=d.Mode.Difficulty==Difficulty.Brutal?300:180;game.Chess.Begin();
  return game.Chess.Active||game.Chess.challenge==challenge;
 }
 public bool OnButton(){Open();return true;}
 // ---------------------------------------------------------------------------------------------------------------- events
 void OnWhite(ChessChallenge c){
  if(c!=challenge||challenge==null)return;
  Model.OnPlayerMove();
  if(Model.PlayerMoves==RideGameModel.Quota)d.Say("You have played enough of the game. I will let the route continue.",false);
 }
 void OnEnded(string winner,string reason){
  if(challenge==null||game.Chess.challenge!=challenge)return;
  Model.OnOver(winner);EndOfGame(winner);
 }
 void EndOfGame(string winner){
  if(d.Final==null||d.Final.Ended)return;
  if(winner=="w")d.Final.Finish(d.Final.UsedHiddenRule?Ending.Cheater:Ending.Checkmate);
  else if(winner=="b")d.Final.Finish(Ending.Destination);
  else d.Say("A draw. The game is over, but the route is not.",false);
 }
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void Update(float dt){
  if(!Enabled||challenge==null)return;
  if(Model.QuotaMet&&!d.Has("ridegame.quota"))d.Flag("ridegame.quota");
  if(d.Has("lockdown")&&!d.Has("ridegame.offered")){d.Flag("ridegame.offered");d.Say("While we drive, we will play. Press RIDE GAME on the console, or G, when you are ready. I will not wait forever.");d.Puzzles.ShowWarning("RIDE GAME · A FULL GAME OF CHESS AGAINST THE CAR",6f);}
  bool boardOpenOnRide=game.Chess.Active&&game.Chess.challenge==challenge;
  bool online=d.Roles!=null&&d.Roles.Online;
  bool clockRuns=d.Has("lockdown")&&!boardOpenOnRide&&!(d.Final!=null&&(d.Final.Active||d.Final.Ended))&&game.State.Current!=RideState.BOARDING;
  var nag=Model.Tick(dt,d.Phase,clockRuns);
  switch(nag){
   case RideGameModel.Nag.Reminder:d.Say("Your move.");d.Puzzles.ShowWarning("RIDE GAME · YOUR MOVE",4f);break;
   case RideGameModel.Nag.Warning:d.Say("I will not wait much longer.");d.Puzzles.ShowWarning("RIDE GAME · THE CAR WILL MOVE FOR YOU",5f);game.Audio.Chime();break;
   case RideGameModel.Nag.Forced:if(online){Model.OnAutoMove();}else forcePending=true;break;
  }
  if(forcePending&&!game.Chess.Active&&!(d.Final!=null&&d.Final.Active)){forcedRetry-=dt;if(forcedRetry<=0){forcedRetry=2f;if(PlayForPassenger())forcePending=false;}}
  labelClock-=dt;if(labelClock<=0){labelClock=.4f;RefreshLabel();}
 }
 public string Status{get{
  if(!Enabled)return "";
  if(Model.Over)return "RIDE GAME · OVER";
  string s="RIDE GAME · "+Math.Min(Model.PlayerMoves,RideGameModel.Quota)+"/"+RideGameModel.Quota+" MOVES";
  float left=Model.TimeLeft(d.Phase);if(left>0&&d.Has("lockdown"))s+=" · YOUR MOVE "+RideGameModel.Clock(left);
  return s;}}
 void RefreshLabel(){
  if(Label!=null)Label.text=Model.Over?"RIDE GAME\nOVER":"RIDE GAME\n"+Math.Min(Model.PlayerMoves,RideGameModel.Quota)+"/"+RideGameModel.Quota+(Model.TimeLeft(d.Phase)>0&&d.Has("lockdown")?"  "+RideGameModel.Clock(Model.TimeLeft(d.Phase)):"");
  if(Led!=null){
   float iv=RideGameModel.Interval(d.Phase);float frac=iv>0&&d.Has("lockdown")?Model.TimeLeft(d.Phase)/iv:1f;
   Color c=Model.Over?new Color(.4f,.4f,.45f):frac<.15f?new Color(1f,.15f,.08f):frac<.4f?new Color(1f,.7f,.15f):Model.QuotaMet?new Color(.3f,1f,.55f):new Color(.25f,.9f,.95f);
   Mats.SetGlow(Led.sharedMaterial,c,frac<.4f?1.8f:1f);
  }
 }
 // ---------------------------------------------------------------------------------------------------------------- the car plays for you
 // Runs the engine directly on the saved position (no tray needed): a weak White move, then the car's real reply.
 bool PlayForPassenger(){
  try{
   var rules=game.Chess.RoadRules;rules.Enabled=true;rules.AllRulesKnown=game.AllRulesKnown;rules.Sync(game);
   string fen=challenge.savedFen,st=challenge.savedState;
   ChessSnapshot S(string op,Action<ChessRequest> fill=null){
    var q=new ChessRequest{op=op,fen=fen,stateJson=st};rules.Configure(q);fill?.Invoke(q);
    var r=ChessEngine.Send(q);if(!string.IsNullOrEmpty(r.stateJson))st=r.stateJson;if(!string.IsNullOrEmpty(r.fen)&&op!="ai"&&op!="probe")fen=r.fen;return r;
   }
   if(string.IsNullOrEmpty(st)){                       // the game was never opened: the car starts it for you
    var begin=new ChessRequest{op="begin",fen=challenge.FenFor(game.RunSeed),seed=challenge.seed+game.RunSeed,cfgJson=challenge.ConfigJson()};rules.Configure(begin);
    var b=ChessEngine.Send(begin);if(!b.ok)return false;st=b.stateJson;fen=b.fen;S("startTurn");
   }
   var snap=S("query");if(snap.over)return true;
   var plan=S("ai",q=>{q.difficulty="easy";q.budgetMs=60;q.noAbilities=true;});
   AiAction a=plan.ok&&plan.plan!=null?plan.plan.action:null;
   if(a==null||string.IsNullOrEmpty(a.from)){var first=snap.moves!=null?Array.Find(snap.moves,m=>m.kind=="move"||m.kind=="castle"):null;if(first==null)return true;a=new AiAction{kind=first.kind,from=first.from,to=first.to,promotion=first.promotion,steps=new ChessStep[0]};}
   var mine=S("act",q=>{q.kind=a.kind;q.from=a.from;q.to=a.to;q.promotion=a.promotion??"";q.steps=a.steps;});
   if(!mine.accepted){var any=snap.moves!=null?Array.Find(snap.moves,m=>m.kind=="move"):null;if(any==null)return true;mine=S("act",q=>{q.kind=any.kind;q.from=any.from;q.to=any.to;q.promotion=any.promotion??"";});}
   if(!mine.over){
    S("startTurn");
    var reply=S("ai",q=>{q.difficulty=challenge.opponent=="scripted"?"normal":challenge.opponent;q.budgetMs=120;});
    if(reply.ok&&reply.plan!=null&&reply.plan.action!=null&&!string.IsNullOrEmpty(reply.plan.action.from)){
     var p=reply.plan;
     if(p.card!=null&&!string.IsNullOrEmpty(p.card.card))S("card",q=>{q.card=p.card.card;q.target=p.card.target??"";q.target2=p.card.target2??"";});
     if(p.cyber!=null&&!string.IsNullOrEmpty(p.cyber.ability))S("cyber",q=>{q.ability=p.cyber.ability;q.target=p.cyber.target??"";});
     S("act",q=>{q.kind=p.action.kind;q.from=p.action.from;q.to=p.action.to;q.promotion=p.action.promotion??"";q.steps=p.action.steps;});
    }
    var after=S("query");if(!after.over)S("startTurn");
   }
   var end=S("query");
   challenge.savedFen=fen;challenge.savedState=st;
   Model.OnAutoMove();
   d.Say("You were taking too long. I moved for you.");d.Puzzles.ShowWarning("THE CAR MOVED FOR YOU · THAT MOVE DOES NOT COUNT",5f);game.Audio.LockClick();
   if(end.over){Model.OnOver(end.winner);challenge.savedFen="";challenge.savedState="";EndOfGame(end.winner);}
   return true;
  }catch(Exception e){Debug.LogException(e);Model.OnAutoMove();return true;}
 }
 // ---------------------------------------------------------------------------------------------------------------- finale reward
 public int BonusCharges(){
  if(!Enabled||challenge==null)return 0;
  string fen=game.Chess.Active&&game.Chess.challenge==challenge&&game.Chess.Snapshot!=null?game.Chess.Snapshot.fen:challenge.savedFen;
  return RideGameModel.BonusCharges(RideGameModel.MaterialDiff(fen));
 }
 // ---------------------------------------------------------------------------------------------------------------- AI passenger
 public bool AiShouldPlay(float now,CarSkill skill){
  if(!Enabled||Model.Over||!d.Has("lockdown"))return false;
  var beat=d.Story.Current(d.Has);bool urgent=beat!=null&&beat.Id=="ridegame";
  float gap=urgent?6f:skill==CarSkill.Casual?90f:skill==CarSkill.Smart?60f:skill==CarSkill.Expert?40f:28f;
  return now-lastAiPlay>gap;
 }
 public void NoteAiPlay(float now){lastAiPlay=now;}
}
}
