using System;
using System.Collections.Generic;
using System.Linq;
using Checkmate55.Core;
namespace Checkmate55.Tests {
[TestFixture] public class RadioTests {
 [Test] public void FivePresetsVolumeMuteBalanceFader(){
  var r=new RadioModel();
  for(int p=1;p<=5;p++){Assert.True(r.Select(p));Assert.Eq(p,r.Station);}
  Assert.False(r.Select(0));Assert.False(r.Select(6));
  r.VolumeStep(1);Assert.InRange(r.Volume,.59f,.61f);r.ToggleMute();Assert.True(r.Muted);r.ToggleMute();Assert.False(r.Muted);
  for(int i=0;i<10;i++)r.BalanceStep(-1);Assert.Eq(-1f,r.Balance);for(int i=0;i<10;i++)r.FaderStep(-1);Assert.Eq(-1f,r.Fader);
 }
 [Test] public void HiddenRecordingNeedsThePresetTheBalanceAndTheFader(){
  var r=new RadioModel();bool heard=false;r.RecordingHeard+=()=>heard=true;
  r.Select(4);for(int i=0;i<5;i++)r.BalanceStep(-1);
  for(int i=0;i<40;i++)r.Tick(.1f);Assert.False(heard,"fader still centred: music only");
  for(int i=0;i<5;i++)r.FaderStep(-1);Assert.True(r.RecordingAudible);
  for(int i=0;i<20;i++)r.Tick(.1f);Assert.True(heard&&r.RecordingFound);
  var wrong=new RadioModel();wrong.Select(2);for(int i=0;i<5;i++){wrong.BalanceStep(-1);wrong.FaderStep(-1);}
  for(int i=0;i<40;i++)wrong.Tick(.1f);Assert.False(wrong.RecordingFound,"wrong station");
  var muted=new RadioModel();muted.Select(4);for(int i=0;i<5;i++){muted.BalanceStep(-1);muted.FaderStep(-1);}muted.ToggleMute();for(int i=0;i<40;i++)muted.Tick(.1f);Assert.False(muted.RecordingFound,"muted radio plays nothing");
 }
 [Test] public void CarCanTakeTheRadioAndPassengerMustWinItBack(){
  var r=new RadioModel();r.Select(3);r.CarSelect(1);Assert.Eq(1,r.Station);r.Locked=true;Assert.False(r.Select(3));Assert.False(r.ToggleMute(),"cannot be silenced while the car holds it");
  r.CarMute();Assert.True(r.Muted);r.CarVolume(1f);Assert.False(r.Muted);r.CarRoute(1,1);Assert.Eq(1f,r.Balance);
  var c=new ContestBoard();c.Begin("radio",0);c.Press("radio",1);c.Press("radio",1.5f);Assert.True(c.Press("radio",2));r.Locked=false;Assert.True(r.Select(3));
 }
 [Test] public void CarRemembersAndDeliberatelyRemovesTheFavouriteStation(){
  var r=new RadioModel();r.Select(2);for(int i=0;i<300;i++)r.Tick(.1f);Assert.Eq(2,r.Favorite);
  Assert.True(r.RemoveFavorite());Assert.Eq(0,r.Favorite);Assert.True(r.Station!=2);Assert.True(r.FavoriteLost);
  r.Select(2);for(int i=0;i<600;i++)r.Tick(.1f);Assert.Eq(0,r.Favorite,"the car only does this once");
 }
 [Test] public void DisplayShowsJamming(){var r=new RadioModel();Assert.True(r.Display(true).Contains("SIGNAL LOST"));Assert.True(r.Display().Contains("PRESET 1"));}
}
[TestFixture] public class PuzzleTests {
 [Test] public void ClimateSequenceSolvesOnlyWithThePatternThenFanThree(){
  var c=new ClimateModel();bool solved=false;c.SolvedEvent+=()=>solved=true;
  void Set(int t){while(c.Temp<t)c.Adjust(1);while(c.Temp>t)c.Adjust(-1);for(int i=0;i<15;i++)c.Tick(.1f);}
  Set(19);Set(23);Set(17);Assert.True(c.PatternReady,"pattern recognised after the last value settles");
  c.SetFan(2);Assert.False(solved);c.SetFan(3);Assert.True(solved&&c.Solved);
  var d=new ClimateModel();bool s2=false;d.SolvedEvent+=()=>s2=true;
  void Set2(int t){while(d.Temp<t)d.Adjust(1);while(d.Temp>t)d.Adjust(-1);for(int i=0;i<15;i++)d.Tick(.1f);}
  Set2(23);Set2(19);Set2(17);d.SetFan(3);Assert.False(s2,"wrong order");
  var e=new ClimateModel();e.SetFan(3);bool s3=false;e.SolvedEvent+=()=>s3=true;
  void Set3(int t){while(e.Temp<t)e.Adjust(1);while(e.Temp>t)e.Adjust(-1);for(int i=0;i<15;i++)e.Tick(.1f);}
  Set3(19);Set3(23);Set3(17);Assert.True(s3,"fan already at 3 also solves when the pattern completes");
 }
 [Test] public void ClimateBounceThroughValuesDoesNotCount(){
  var c=new ClimateModel();for(int t=21;t<=25;t++){c.Adjust(1);c.Tick(.2f);}for(int i=0;i<6;i++){c.Adjust(-1);c.Tick(.2f);}
  Assert.True(c.History.Count<=1,"values that were not left alone are not recorded: "+c.History.Count);
 }
 [Test] public void FrozenClimateIgnoresThePassenger(){var c=new ClimateModel{Frozen=true};Assert.False(c.Adjust(1));Assert.False(c.CycleFan());Assert.False(c.Toggle());c.Frozen=false;Assert.True(c.Adjust(1));}
 [Test] public void LegacyClimateToggleStillWorks(){var c=new ClimateModel();Assert.Eq(21,c.Temp);c.Toggle();Assert.Eq(19,c.Temp);c.Toggle();Assert.Eq(21,c.Temp);}
 [Test] public void OverheadRhythmTwoPauseThree(){
  var l=new RhythmLock(StoryText.LightPattern);bool solved=false;l.SolvedEvent+=()=>solved=true;
  l.Press(0);l.Press(.5f);l.Press(3f);l.Press(3.5f);Assert.False(solved);l.Press(4f);Assert.True(solved);
  var bad=new RhythmLock(StoryText.LightPattern);bool failed=false;bad.Failed+=()=>failed=true;bad.Press(0);bad.Press(.4f);bad.Press(.8f);Assert.True(failed&&!bad.Solved,"three presses in the first burst");
  var slow=new RhythmLock(StoryText.LightPattern);slow.Press(0);slow.Press(.5f);slow.Press(9f);slow.Press(9.5f);slow.Press(10f);Assert.False(slow.Solved,"a long wait resets the attempt");
  var fast=new RhythmLock(StoryText.LightPattern);fast.Press(0);fast.Press(.3f);fast.Press(.6f);Assert.False(fast.Solved);
 }
 [Test] public void PhoneNeedsCharginMaintenanceModeAndALocation(){
  var p=new PhoneModel();p.Tick(1);Assert.Eq(PhoneState.Missing,p.State);p.Held=true;p.Tick(1);Assert.Eq(PhoneState.Dead,p.State);
  p.Plugged=true;for(int i=0;i<60;i++)p.Tick(.1f);Assert.Eq(PhoneState.Jammed,p.State);Assert.False(p.Call(),"jammed");
  p.JammerOff=true;p.Tick(.1f);Assert.Eq(PhoneState.Ready,p.State);Assert.False(p.Call(),"no location yet");
  p.HasLocation=true;Assert.True(p.Call());Assert.Eq(PhoneState.Connected,p.State);Assert.True(p.Display(31.4f).Contains("31.4"));
 }
}
[TestFixture] public class PoliceTests {
 static List<(float t,PoliceEvent e)> Run(int seed,Func<float,PoliceInputs> inp,float seconds){
  var p=new PoliceModel(seed);p.Begin();var log=new List<(float,PoliceEvent)>();
  for(float t=0;t<seconds;t+=.25f)foreach(var e in p.Tick(.25f,inp(t)))log.Add((t,e));return log;
 }
 [Test] public void SirensThenPoliceAppearBehindTheVehicle(){
  var log=Run(1,t=>new PoliceInputs{CarMph=90},60);
  var faint=log.First(e=>e.e.Type==PoliceEventType.SirenFaint).t;var vis=log.First(e=>e.e.Type==PoliceEventType.Visible).t;
  Assert.True(faint>=5.5f&&faint<vis&&vis<=14.5f,"faint siren "+faint+", visible "+vis);Assert.True(log.Any(e=>e.e.Type==PoliceEventType.SirenLoud));
 }
 [Test] public void PoliceActionsMateriallyAffectTheCar(){
  var log=Run(3,t=>new PoliceInputs{CarMph=90},240);
  Assert.True(log.Any(e=>e.e.Type==PoliceEventType.ForceBrake),"police force braking");Assert.True(log.Any(e=>e.e.Type==PoliceEventType.ForceLane),"police force a lane change");
  foreach(var e in log.Where(x=>x.e.Type==PoliceEventType.ForceLane))Assert.True(e.e.Lane>=0&&e.e.Lane<=2);
 }
 [Test] public void ActionsAreSpacedOut(){
  var log=Run(5,t=>new PoliceInputs{CarMph=90},300).Where(e=>e.e.Type==PoliceEventType.ForceBrake||e.e.Type==PoliceEventType.ForceLane||e.e.Type==PoliceEventType.RadioLine||e.e.Type==PoliceEventType.RadioGarbled||e.e.Type==PoliceEventType.PitAttempt||e.e.Type==PoliceEventType.Closing).ToList();
  for(int i=1;i<log.Count;i++)Assert.True(log[i].t-log[i-1].t>=14.5f,"police acted twice within "+(log[i].t-log[i-1].t));
 }
 [Test] public void JammingGarblesTheirRadioAndSignallingBuildsPressure(){
  var jam=Run(7,t=>new PoliceInputs{CarMph=90,Jamming=true},300);
  Assert.False(jam.Any(e=>e.e.Type==PoliceEventType.RadioLine),"jammed: nothing intelligible");Assert.True(jam.Any(e=>e.e.Type==PoliceEventType.RadioGarbled));
  var free=new PoliceModel(7);free.Begin();for(float t=0;t<120;t+=.25f)free.Tick(.25f,new PoliceInputs{CarMph=90,Signalled=true,Transmitted=true});
  var jammed=new PoliceModel(7);jammed.Begin();for(float t=0;t<120;t+=.25f)jammed.Tick(.25f,new PoliceInputs{CarMph=90,Jamming=true});
  Assert.True(free.Pressure>.6f&&jammed.Pressure<.15f,"signalled "+free.Pressure+" vs jammed "+jammed.Pressure);
 }
 [Test] public void AcceleratingOpensTheGapAndPressureClosesIt(){
  var fast=new PoliceModel(2);fast.Begin();for(float t=0;t<60;t+=.25f)fast.Tick(.25f,new PoliceInputs{CarMph=125,Jamming=true});
  var pressed=new PoliceModel(2);pressed.Begin();for(float t=0;t<60;t+=.25f)pressed.Tick(.25f,new PoliceInputs{CarMph=70,Signalled=true,Transmitted=true});
  Assert.True(fast.Distance>pressed.Distance,"fast "+fast.Distance+" vs pressed "+pressed.Distance);
 }
 [Test] public void DeterministicPerSeed(){
  string Sig(int s)=>string.Join(";",Run(s,t=>new PoliceInputs{CarMph=90},120).Select(e=>e.t+e.e.ToString()));
  Assert.Eq(Sig(4),Sig(4));Assert.True(Sig(4)!=Sig(5));
 }
}
[TestFixture] public class VehicleAndEndingTests {
 [Test] public void SpeedPolicyCombinesStoryThreatAndPolice(){
  var v=new VehicleInputs{Hostile=true,Cruise=75};
  Assert.Eq(75f,VehiclePolicy.TargetMph(v));v.ThreatBoost=15;Assert.Eq(90f,VehiclePolicy.TargetMph(v));
  v.PoliceBrake=true;v.PoliceBrakeMph=50;Assert.Eq(50f,VehiclePolicy.TargetMph(v));
  Assert.Eq(VehiclePolicy.NormalRideMph,VehiclePolicy.TargetMph(new VehicleInputs{Hostile=false,Cruise=120}));
  Assert.Eq(42f,VehiclePolicy.TargetMph(new VehicleInputs{Hostile=true,Cruise=95,RouteOverride=true}));
  Assert.Eq(0f,VehiclePolicy.TargetMph(new VehicleInputs{Hostile=true,Cruise=95,Stopped=true}));
  Assert.True(VehiclePolicy.TargetMph(new VehicleInputs{Hostile=true,Cruise=200,ThreatBoost=200})<=140f);
  Assert.Eq(3,VehiclePolicy.Tier(115));Assert.Eq(0,VehiclePolicy.Tier(69));
 }
 [Test] public void PoliceBrakeChangesTheSpeedTier(){
  var v=new VehicleInputs{Hostile=true,Cruise=115};Assert.Eq(3,VehiclePolicy.Tier(VehiclePolicy.TargetMph(v)));
  v.PoliceBrake=true;v.PoliceBrakeMph=85;Assert.Eq(1,VehiclePolicy.Tier(VehiclePolicy.TargetMph(v)),"police braking drops Road Chess to the 70+ tier");
 }
 [Test] public void EndingsResolveByPriority(){
  Assert.Eq(Ending.Checkmate,EndingRules.Resolve(new EndingFacts{ChessWinner="w"}));
  Assert.Eq(Ending.Cheater,EndingRules.Resolve(new EndingFacts{ChessWinner="w",UsedHiddenRule=true}));
  Assert.Eq(Ending.Cheater,EndingRules.Resolve(new EndingFacts{CarCollapsed=true}));
  Assert.Eq(Ending.PreviousPassenger,EndingRules.Resolve(new EndingFacts{EscapeSequenceComplete=true,ChessWinner="b"}));
  Assert.Eq(Ending.SystemEscape,EndingRules.Resolve(new EndingFacts{PoliceForcedStop=true,FreedSystems=4,ImpactReached=true}));
  Assert.Eq(Ending.Destination,EndingRules.Resolve(new EndingFacts{PoliceForcedStop=true,FreedSystems=2,ImpactReached=true}));
  Assert.Eq(Ending.Destination,EndingRules.Resolve(new EndingFacts{ChessWinner="b",ChessReason="checkmate"}));
  Assert.Eq(Ending.Destination,EndingRules.Resolve(new EndingFacts{ImpactReached=true}));
  Assert.Eq(Ending.None,EndingRules.Resolve(new EndingFacts()));
  foreach(Ending e in Enum.GetValues(typeof(Ending)))if(e!=Ending.None){Assert.True(EndingRules.Title(e).Length>0&&EndingRules.Epilogue(e).Length>0);}
  Assert.Eq("DESTINATION REACHED.",EndingRules.Title(Ending.Destination));
 }
 [Test] public void RecordsPersistAndTrackBestTimesAndEndings(){
  var r=new Records();r.CalibrationDone=true;r.PreferredPreset=4;r.SecretRulesMask=RoadChessRules_Reversal;
  r.RecordEnding(Ending.Checkmate,2100000,150000);Assert.Eq(2100000,r.BestStoryMs);r.RecordEnding(Ending.Cheater,1500000,120000);Assert.Eq(1500000,r.BestStoryMs);Assert.Eq(120000,r.BestFinalMs);
  r.RecordEnding(Ending.Destination,3000000,10);Assert.Eq(1500000,r.BestStoryMs,"a loss never sets a record");Assert.Eq(2,r.Escapes);
  r.RecordResult(GameMode.PassengerChess,"win");r.RecordResult(GameMode.PassengerChess,"loss");r.RecordResult(GameMode.PassengerChess,"draw");
  var s=r.ToString();var back=Records.Parse(s);
  Assert.Eq(s,back.ToString());Assert.True(back.Seen(Ending.Cheater)&&back.Seen(Ending.Destination)&&!back.Seen(Ending.SystemEscape));Assert.Eq(3,back.EndingCount);
  Assert.Eq(1,back.ResultsFor(GameMode.PassengerChess)[0]);Assert.True(back.CalibrationDone);Assert.Eq(4,back.PreferredPreset);
  Assert.Eq(0,Records.Parse("garbage;;=;x=1").Escapes);Assert.Eq("--:--",Records.Clock(-1));Assert.Eq("12:05",Records.Clock(725000));
 }
 const int RoadChessRules_Reversal=64;
 [Test] public void ModesAreAllDefinedAndAssembledFromTheSameParts(){
  foreach(GameMode m in Enum.GetValues(typeof(GameMode))){var c=ModeCatalog.For(m);Assert.True(c.Title.Length>0&&c.Blurb.Length>0,m.ToString());}
  Assert.False(ModeCatalog.For(GameMode.PassengerChess).Hostile);Assert.False(ModeCatalog.For(GameMode.PassengerChess).ImpactTimer);Assert.False(ModeCatalog.For(GameMode.Calibration).Hostile);
  Assert.True(ModeCatalog.For(GameMode.Story).ImpactTimer);Assert.Eq(2,ModeCatalog.For(GameMode.CoopEscape).Humans);
  Assert.Eq(ControllerKind.Human,ModeCatalog.For(GameMode.DriverlessDuel).Car);Assert.Eq(ControllerKind.Ai,ModeCatalog.For(GameMode.CarMaster).Passenger);
  var sim=ModeCatalog.For(GameMode.AiSimulation);Assert.True(sim.Passenger==ControllerKind.Ai&&sim.Car==ControllerKind.Ai&&sim.Humans==0);
  Assert.True(ModeCatalog.For(GameMode.RoadChess).Road&&ModeCatalog.For(GameMode.RoadChess).Cyber);Assert.False(ModeCatalog.For(GameMode.PassengerChess).Road);
 }
}
[TestFixture] public class RoleTests {
 [Test] public void CoopNeverGivesTwoChessActionsPerTurn(){
  foreach(CoopControl mode in Enum.GetValues(typeof(CoopControl))){
   var t=new CoopTurn(mode,2);
   for(int turn=0;turn<6;turn++){
    int accepted=0;
    for(int tries=0;tries<10;tries++){if(t.TryConfirmChess(0))accepted++;if(t.TryConfirmChess(1))accepted++;}
    Assert.True(accepted<=1,mode+": "+accepted+" chess actions in one team turn");
    t.NextTurn();
   }
  }
 }
 [Test] public void SharedControlFirstConfirmedMoveLocksTheTurn(){
  var t=new CoopTurn(CoopControl.Shared,2);Assert.True(t.CanPreview(0)&&t.CanPreview(1));
  Assert.True(t.TryConfirmChess(1));Assert.False(t.TryConfirmChess(0));Assert.Eq(1,t.LockedBy);t.NextTurn();Assert.True(t.TryConfirmChess(0));
 }
 [Test] public void AlternatingOperatorSwapsEachTurn(){
  var t=new CoopTurn(CoopControl.Alternating,2);
  Assert.False(t.TryConfirmChess(1));Assert.True(t.TryConfirmChess(0));t.NextTurn();Assert.False(t.TryConfirmChess(0));Assert.True(t.TryConfirmChess(1));t.NextTurn();Assert.True(t.TryConfirmChess(0));
 }
 [Test] public void TacticalAssistSplitsResponsibilitiesAndSharesGlobalLimits(){
  var t=new CoopTurn(CoopControl.TacticalAssist,2);
  Assert.False(t.TryConfirmChess(1),"B never makes the chess move");Assert.False(t.TrySupport(0,SupportKind.Card),"A does not use support");
  Assert.True(t.TrySupport(1,SupportKind.Card));Assert.False(t.TrySupport(1,SupportKind.Card),"max one card");Assert.False(t.TrySupport(1,SupportKind.Cyber),"B has one support action");
  Assert.True(t.TryConfirmChess(0));Assert.False(t.TryConfirmChess(0));
  t.NextTurn();Assert.True(t.TrySupport(1,SupportKind.Cyber));Assert.False(t.TrySupport(1,SupportKind.Cyber));
  Assert.True(t.Banner.Contains("PASSENGER A"));
 }
 [Test] public void SoloPlayerKeepsEveryPrivilege(){
  var t=new CoopTurn(CoopControl.TacticalAssist,1);Assert.True(t.TrySupport(0,SupportKind.Card));Assert.True(t.TrySupport(0,SupportKind.Cyber));Assert.True(t.TryConfirmChess(0));Assert.False(t.TryConfirmChess(0));
 }
 [Test] public void SystemAuthorityLimitsTheHumanCar(){
  var a=new SystemAuthority();
  Assert.True(a.Try(CarAction.RadioHijack));Assert.False(a.Try(CarAction.RadioHijack),"cooldown");Assert.True(a.Why(CarAction.RadioHijack).Contains("cooling"));
  Assert.True(a.Try(CarAction.RouteManipulate)==(a.Value>=0));
  var poor=new SystemAuthority();poor.Try(CarAction.RouteManipulate);Assert.False(poor.Try(CarAction.ExtremeAcceleration),"cannot afford everything at once");
  for(int i=0;i<100;i++)poor.Tick(1f);Assert.Eq(SystemAuthority.Max,poor.Value);Assert.True(poor.Try(CarAction.ExtremeAcceleration));
  Assert.True(SystemAuthority.Cost(CarAction.RadioHijack)<SystemAuthority.Cost(CarAction.SeatAdjust)&&SystemAuthority.Cost(CarAction.SeatAdjust)<SystemAuthority.Cost(CarAction.RouteManipulate),"low < medium < high");
  Assert.True(SystemAuthority.Cost(CarAction.ExtremeAcceleration)>=40);
  foreach(var bad in SystemAuthority.Forbidden)Assert.False(SystemAuthority.IsAllowedName(bad),bad);
  Assert.True(SystemAuthority.IsAllowedName("WindowClose"));
 }
 sealed class FakeOracle:IChessOracle{public int Calls;public Intent BestChessIntent(int actor,string fen,string level){Calls++;return new Intent{Kind=IntentKind.ChessAction,Actor=actor,From="e2",To="e4"};}}
 [Test] public void HumanAndAiPassengersProduceTheSameKindOfIntent(){
  var human=new HumanPassengerController(0);human.Enqueue(Intent.Touch(99,"glovebox"));
  var i1=human.Decide(new PassengerView(),0.1f);Assert.Eq("glovebox",i1.ControlId);Assert.Eq(0,i1.Actor);Assert.Eq(IntentKind.Idle,human.Decide(new PassengerView(),.1f).Kind);
  var ai=new AIPassengerController(1,CarSkill.Relentless,new FakeOracle(),3);
  var view=new PassengerView{TargetControl="glovebox"};var seen=ai.Decide(view,1f);
  Assert.Eq(IntentKind.Interact,seen.Kind);Assert.Eq("glovebox",seen.ControlId);
 }
 [Test] public void AiPassengerOnlyUsesWhatTheViewShows(){
  var oracle=new FakeOracle();var ai=new AIPassengerController(1,CarSkill.Expert,oracle,3);
  var idle=ai.Decide(new PassengerView(),5f);Assert.Eq(IntentKind.Idle,idle.Kind,"no guide target: it does not invent one");
  var chess=new PassengerView{ChessActive=true,MyChessTurn=false,Fen="x"};Assert.Eq(IntentKind.Idle,ai.Decide(chess,5f).Kind);Assert.Eq(0,oracle.Calls,"waits for its turn");
  chess.MyChessTurn=true;var move=ai.Decide(chess,5f);Assert.Eq(IntentKind.ChessAction,move.Kind);Assert.Eq(1,oracle.Calls);
 }
 [Test] public void AiSkillChangesPaceNotInformation(){
  int Touches(CarSkill s){var ai=new AIPassengerController(1,s,null,5);int n=0;var v=new PassengerView{TargetControl="glovebox"};for(int i=0;i<200;i++)if(ai.Decide(v,.25f).Kind==IntentKind.Interact)n++;return n;}
  Assert.True(Touches(CarSkill.Relentless)>Touches(CarSkill.Casual),"stronger AI acts more often");
 }
 [Test] public void AiCarPaysTheSameAuthorityCostsAsAHumanCar(){
  var auth=new SystemAuthority();var ai=new AICarController(CarSkill.Relentless,auth,2);int actions=0;
  for(int i=0;i<3000;i++){auth.Tick(.1f);var intent=ai.Decide(new CarView(),.1f);if(intent.Kind==IntentKind.CarAction){Assert.True(auth.Try(intent.Car),"AI car proposed an action it could not afford: "+intent.Car);actions++;}}
  Assert.True(actions>10);
 }
}
}
