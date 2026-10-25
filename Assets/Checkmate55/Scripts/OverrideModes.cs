using System;
using System.Collections.Generic;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// Every mode is assembled from the same world and the same rules: Story is the whole campaign; Calibration is the official, harmless,
// 8-10 minute training program; Passenger Chess / Road Chess are the rules without the escape pressure; AI Simulation lets two AI
// controllers play the physical tray for testing and spectating.
public sealed class OverrideModes {
 readonly OverrideDirector d;SceneManager game=>d.game;
 public float ForcedMph{get;private set;}
 public Difficulty PassengerDifficulty=Difficulty.Normal;public RuleSet PassengerRules=RuleSet.Standard;
 public bool CalibrationComplete{get;private set;}
 public string GameResult{get;private set;}="";
 // ---- calibration
 sealed class Lesson {public string Id,Title,Prompt,Voice,Target;public Func<bool> Done;public Action Start;public float Min=5;public ChessChallenge Chess;}
 readonly List<Lesson> lessons=new List<Lesson>();int index=-1;float lessonClock,focusTime,startYaw;bool chessDone,deployed;int startSeat,startUse;float startTemp;
 public string LessonTitle=>index>=0&&index<lessons.Count?lessons[index].Title:"";
 public int LessonIndex=>Mathf.Max(0,index);public int LessonCount=>lessons.Count;
 public string PromptOverride{get{
  if(d.Mode.Mode==GameMode.Calibration)return CalibrationComplete?"CALIBRATION COMPLETE.":(index>=0&&index<lessons.Count?lessons[index].Prompt:"");
  if(d.Mode.ChessOnly&&!game.Chess.Active)return "Tap AUTHENTICATE on the rear display to deploy the board. "+PassengerDifficulty.ToString()+" · "+ModeCatalog.RuleNames[(int)PassengerRules];
  return null;}}
 public string TargetOverride{get{
  if(d.Mode.Mode==GameMode.Calibration&&index>=0&&index<lessons.Count)return lessons[index].Target;
  if(d.Mode.ChessOnly&&!game.Chess.Active)return "authenticate";
  return null;}}
 // ---- ai simulation
 public OverrideModes(OverrideDirector director){d=director;}
 public object Online;                 // set when a room-server session is connected (see NetClient)
 public void Initialize(){game.Chess.Completed+=OnChessCompleted;game.Chess.Ended+=OnChessEnded;}
 ChessChallenge Make(string id,string title,string fen,ChessObjective objective,string guidance,int maxMoves){
  var c=ScriptableObject.CreateInstance<ChessChallenge>();c.id=id;c.title=title;c.FEN=fen;c.objectiveType=objective;c.guidanceText=guidance;c.maxMoves=maxMoves;c.rewardEvent="cal."+id;c.resultText=title+" · COMPLETE";
  c.laneFollowsMove=false;c.checkBrakes=false;c.opponent="easy";c.hintMove="";c.hintText=guidance;return c;
 }
 // ---------------------------------------------------------------------------------------------------------------- begin
 public void Begin(ModeConfig m){
  game.RunSeed=game.RunSeed;GameResult="";ForcedMph=0;
  switch(m.Mode){
   case GameMode.Calibration:BuildLessons();index=-1;CalibrationComplete=false;break;
   case GameMode.PassengerChess:case GameMode.RoadChess:
    PassengerRules=m.Rules;PassengerDifficulty=m.Difficulty;break;
  }
  game.Ride.LockdownAllowed=m.Hostile;
  if(m.Mode==GameMode.Story){game.Ride.LockdownNotBefore=75;game.Ride.LockdownAt=300;}
 }
 // ---------------------------------------------------------------------------------------------------------------- chess-only modes
 public ChessChallenge BuildPassengerChallenge(){
  var c=Make("passenger","PASSENGER CHESS",d.Mode.Mode==GameMode.AiSimulation?FinalPositions.Pick(game.RunSeed):"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",ChessObjective.Checkmate,"Checkmate the car. White moves first.",0);
  bool road=PassengerRules!=RuleSet.Standard,cyber=PassengerRules==RuleSet.CyberRoadChess;
  c.road=road;c.cyber=cyber;c.cards=cyber;c.hazards=road;c.carCyber=cyber;c.carCards=false;c.autoVehicle=road;c.startHand=cyber?1:0;c.startCharges=0;
  c.opponent=d.Mode.Mode==GameMode.AiSimulation?ModeConfig.SkillLevel(d.Mode.Skill):PassengerDifficulty.ToString().ToLowerInvariant();
  c.noRetry=true;c.rewardEvent="passenger.win";c.title=d.Mode.Mode==GameMode.RoadChess?"ROAD CHESS":"PASSENGER CHESS";c.seed=game.RunSeed;
  c.resultText="CHECKMATE · WHITE WINS";c.hintText="A safe developing move.";
  return c;
 }
 public bool OnAuthenticate(){
  if(game.Chess.Active)return true;
  if(d.Mode.Mode==GameMode.Calibration){
   if(index>=0&&index<lessons.Count&&lessons[index].Chess!=null){Deploy(lessons[index].Chess);}
   else d.Say("Calibration: follow the highlighted prompt first.",false);
   return true;
  }
  NewGame();return true;
 }
 public void NewGame(){
  if(game.Chess.Active)return;
  GameResult="";var c=BuildPassengerChallenge();game.Chess.challenge=c;game.Chess.AiBudgetMs=PassengerDifficulty==Difficulty.Brutal?350:180;game.Chess.Begin();
 }
 void Deploy(ChessChallenge c){
  game.Chess.challenge=c;game.Chess.Begin();
 }
 void OnChessCompleted(string id){if(d.Mode.Mode==GameMode.Calibration&&id.StartsWith("cal."))chessDone=true;}
 void OnChessEnded(string winner,string reason){
  if(!d.Mode.ChessOnly&&d.Mode.Mode!=GameMode.AiSimulation)return;
  string result=winner=="w"?"win":winner=="b"?"loss":"draw";GameResult=winner=="w"?"CHECKMATE · WHITE WINS":winner=="b"?"CHECKMATE · THE CAR WINS":"DRAW";
  if(d.Mode.ChessOnly){d.Records.RecordResult(d.Mode.Mode,result);d.SaveRecords();}
 }
 // ---------------------------------------------------------------------------------------------------------------- calibration lessons
 void BuildLessons(){
  lessons.Clear();
  var seat=game.Seats;var radio=d.Radio;var climate=d.Climate;
  lessons.Add(new Lesson{Id="look",Title="LOOKING",Voice="Passenger systems calibration initiated.",Prompt="CALIBRATION 1 · LOOK. Drag (or use WASD / arrow keys) to look around the cabin.",Min=6,Start=()=>startYaw=game.Player.yaw,Done=()=>Mathf.Abs(Mathf.DeltaAngle(game.Player.yaw,startYaw))>40});
  lessons.Add(new Lesson{Id="focus",Title="INTERACTION FOCUS",Voice="Please interact with the highlighted control.",Prompt="CALIBRATION 2 · FOCUS. Hold your view on the glowing control. The camera leans toward it and narrows.",Target="climate-plus",Min=8,Start=()=>focusTime=0,Done=()=>focusTime>1.0f});
  lessons.Add(new Lesson{Id="touch",Title="TOUCH",Voice="Excellent. Now touch it.",Prompt="CALIBRATION 3 · TOUCH. Tap the glowing + to warm the cabin. Watch the hand reach.",Target="climate-plus",Min=5,Start=()=>startTemp=d.Climate.Temp,Done=()=>d.Climate.Temp>startTemp});
  lessons.Add(new Lesson{Id="window",Title="WINDOWS",Voice="Window systems are passenger controlled on this ride.",Prompt="CALIBRATION 4 · WINDOWS. Press the window switch on the left door.",Target="left-window",Min=6,Start=()=>startUse=game.Registry.Get("left-window").UseCount,Done=()=>game.Registry.Get("left-window").UseCount>startUse});
  lessons.Add(new Lesson{Id="seat",Title="SEATS",Voice="You may change seats at any time.",Prompt="CALIBRATION 5 · SEATS. Press the seat button (bottom left) or Tab to move to another seat.",Min=6,Start=()=>startSeat=seat.Seat,Done=()=>seat.Seat!=startSeat&&!seat.Moving});
  lessons.Add(new Lesson{Id="climate",Title="CLIMATE",Voice="Cabin climate calibration.",Prompt="CALIBRATION 6 · CLIMATE. Press the FAN knob until it reads FAN 3.",Target="climate-fan",Min=8,Start=()=>{if(seat.Seat!=1)seat.Choose(1);},Done=()=>d.Climate.Fan==3});
  lessons.Add(new Lesson{Id="radio",Title="RADIO",Voice="Audio calibration. Five presets are available.",Prompt="CALIBRATION 7 · RADIO. Choose preset 2, raise the volume, then pan the sound left (BAL <).",Target="radio-p2",Min=10,Start=()=>{},Done=()=>d.Radio.Station==2&&d.Radio.Volume>=.65f&&d.Radio.Balance<=-.4f});
  lessons.Add(new Lesson{Id="compartment",Title="COMPARTMENTS",Voice="Storage compartments open on request.",Prompt="CALIBRATION 8 · COMPARTMENTS. Open the glove box in the dashboard (move to the front seat to reach it).",Target="glovebox",Min=6,Start=()=>{},Done=()=>game.Ride.glove.State==CompartmentState.OPEN});
  lessons.Add(new Lesson{Id="board",Title="THE BOARD",Voice="Chess is the vehicle's operating language. Deploying the training tray.",Prompt="CALIBRATION 9 · THE BOARD. Tap the glowing AUTHENTICATE panel on the rear display. The tray unfolds into your lap.",Target="authenticate",Min=6,Start=()=>{if(seat.Seat!=1)seat.Choose(1);},Done=()=>game.Chess.Active});
  var std=Make("std","STANDARD CHESS","6k1/5ppp/8/8/8/8/1Q6/6K1 w - - 0 1",ChessObjective.Checkmate,"Checkmate in one. Tap the queen, then the glowing square.",3);std.hintMove="b2b8";
  lessons.Add(new Lesson{Id="standard",Title="STANDARD CHESS",Voice="Standard chess rules apply at all times unless a road rule says otherwise.",Prompt="CALIBRATION 10 · STANDARD CHESS. Checkmate in one: Qb8.",Min=14,Chess=std,Done=()=>chessDone});
  var jump=Make("road","ROAD JUMP","4k3/6pp/8/2N5/8/8/8/4K3 w - - 0 1",ChessObjective.RoadMove,"At 70+ MPH a knight in the active lane may jump 3 files and 1 rank. Jump the knight to f6 to give check.",4);jump.road=true;jump.targetPiece="roadJump";jump.hintMove="c5f6";
  lessons.Add(new Lesson{Id="road",Title="ROAD CHESS · JUMP",Voice="Road Chess safety simulation beginning.",Prompt="CALIBRATION 11 · ROAD CHESS. Lane A–C is active and the car is above 70 MPH. Jump the knight c5 → f6.",Min=14,Chess=jump,Start=()=>{ForcedMph=80;game.Motion.lane=0;},Done=()=>chessDone});
  var shift=Make("shift","LANE SHIFT","4k3/8/8/8/8/8/2P5/4K3 w - - 0 1",ChessObjective.RoadMove,"A pawn in the active lane may slide exactly one file sideways instead of moving forward. Slide it.",4);shift.road=true;shift.targetPiece="laneShift";shift.hintMove="c2b2";
  lessons.Add(new Lesson{Id="shift",Title="ROAD CHESS · LANE SHIFT",Voice="Lane shift: one file left or right.",Prompt="CALIBRATION 12 · LANE SHIFT. Slide the pawn one file sideways (tap the pawn, then b2 or d2).",Min=12,Chess=shift,Start=()=>{ForcedMph=60;game.Motion.lane=0;},Done=()=>chessDone});
  var cyber=Make("cyber","CYBER ABILITIES","r3k2r/ppp2ppp/2n5/3pP3/3P4/2N5/PPP2PPP/R3K2R w KQkq - 0 1",ChessObjective.UseCyber,"Use FIREWALL, EMP SHOCK and OVERDRIVE once each. One Cyber charge arrives every turn; you start with three.",12);
  cyber.road=true;cyber.cyber=true;cyber.hazards=false;cyber.startCharges=3;cyber.opponent="easy";
  lessons.Add(new Lesson{Id="cyber",Title="CYBER CHESS",Voice="Cyber abilities. Charges are limited. One ability per turn.",Prompt="CALIBRATION 13 · CYBER. Use FIREWALL, EMP SHOCK and OVERDRIVE once each (buttons on the tray's far edge).",Min=40,Chess=cyber,Start=()=>{ForcedMph=75;game.Motion.lane=1;},Done=()=>chessDone});
  var cards=Make("cards","TACTICAL CARDS","4k3/pppp4/8/8/8/3P4/PPP5/4K3 w - - 0 1",ChessObjective.PlayCard,"Play one tactical card from the right-hand row, then make your move. A card does not use your move.",8);
  cards.cards=true;cards.cyber=true;cards.startHand=2;cards.startCharges=0;cards.opponent="easy";
  lessons.Add(new Lesson{Id="cards",Title="TACTICAL CARDS",Voice="Tactical cards. One card per turn.",Prompt="CALIBRATION 14 · CARDS. Tap a card, play it, then make your chess move.",Min=20,Chess=cards,Start=()=>{ForcedMph=0;},Done=()=>chessDone});
  var haz=Make("hazard","HAZARD WARNING","4k3/8/8/8/8/2N5/8/4K3 w - - 0 1",ChessObjective.SurviveMoves,"A tile is sparking: WARNING. At the end of your turn it becomes ACTIVE and destroys anything on it. Move the knight off c3.",2);
  haz.road=true;haz.hazards=true;haz.startHazards="c3";haz.opponent="easy";
  lessons.Add(new Lesson{Id="hazard",Title="HAZARD WARNING",Voice="Hazard warning. Squares spark before they strike.",Prompt="CALIBRATION 15 · HAZARDS. The sparking tile will destroy what stands on it. Move the knight away.",Min=14,Chess=haz,Start=()=>{ForcedMph=80;},Done=()=>chessDone});
 }
 void Advance(){
  index++;lessonClock=0;chessDone=false;deployed=false;
  if(index>=lessons.Count){Complete();return;}
  var L=lessons[index];L.Start?.Invoke();d.Say(L.Voice);
 }
 void Complete(){
  CalibrationComplete=true;ForcedMph=0;d.Records.CalibrationDone=true;d.SaveRecords();d.Say("CALIBRATION COMPLETE.");game.Audio.Chime();
 }
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void Update(float dt){
  var m=d.Mode.Mode;
  if(m==GameMode.Calibration&&game.State.Current!=RideState.BOARDING){
   if(CalibrationComplete)return;
   if(index<0&&game.Ride!=null)Advance();
   if(index<0||index>=lessons.Count)return;
   var L=lessons[index];lessonClock+=dt;
   var f=game.Interactions.Focus;if(L.Target!=null&&f!=null&&f.id==L.Target&&game.Player.FocusBlend>.6f)focusTime+=dt;
   if(L.Chess!=null&&!game.Chess.Active&&!deployed&&lessonClock>2.5f){deployed=true;Deploy(L.Chess);}
   if(L.Chess!=null&&game.Chess.Active&&game.Chess.challenge==L.Chess)deployed=true;
   if(lessonClock>=L.Min&&L.Done()&&!game.Chess.Busy)Advance();
   else if(L.Chess!=null&&deployed&&!game.Chess.Active&&lessonClock>L.Min+120&&!chessDone){deployed=false;}   // the player closed the tray: offer it again
  }
 }
 public void LateUpdate(){
  var ride=game.Ride;if(ride==null||ride.displayAction==null)return;
  switch(d.Mode.Mode){
   case GameMode.Calibration:ride.displayStatus.text="CALIBRATION RIDE\n"+(CalibrationComplete?"COMPLETE":LessonTitle);ride.displayAction.text="TRAINING PROGRAM\n"+(lessons.Count>0&&index>=0&&index<lessons.Count&&lessons[index].Chess!=null?"TAP TO DEPLOY":"STEP "+(LessonIndex+1)+" OF "+LessonCount);ride.displayStatus.color=ride.displayAction.color=new Color(.62f,.89f,.92f);break;
   case GameMode.PassengerChess:case GameMode.RoadChess:ride.displayStatus.text=(d.Mode.Mode==GameMode.RoadChess?"ROAD CHESS":"PASSENGER CHESS")+"\n"+PassengerDifficulty.ToString().ToUpper()+" · "+ModeCatalog.RuleNames[(int)PassengerRules];ride.displayAction.text=game.Chess.Active?"GAME IN PROGRESS":"TAP TO PLAY\nRELAXED DAYTIME RIDE";break;
  }
 }
}
}
