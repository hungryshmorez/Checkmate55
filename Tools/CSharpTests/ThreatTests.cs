using System;
using System.Collections.Generic;
using System.Linq;
using Checkmate55.Core;
namespace Checkmate55.Tests {
[TestFixture] public class ThreatTests {
 static ThreatContext Ctx(StoryPhase p,bool hostile=true,CarMood m=CarMood.Sarcastic)=>new ThreatContext{Phase=p,Hostile=hostile,Mood=m};
 static List<(float t,ThreatAction a)> Run(int seed,StoryPhase p,float seconds,Func<float,ThreatContext> ctx=null){
  var d=new ThreatDirector(seed);var log=new List<(float,ThreatAction)>();float t=0;
  while(t<seconds){t+=.25f;var a=d.Tick(.25f,ctx!=null?ctx(t):Ctx(p));if(a!=null)log.Add((t,a));}
  return log;
 }
 [Test] public void NoThreatsDuringTheNormalRide(){Assert.Eq(0,Run(1,StoryPhase.Ride,600,t=>Ctx(StoryPhase.Ride,false,CarMood.Helpful)).Count);}
 [Test] public void NothingHappensWhenNotHostile(){Assert.Eq(0,Run(1,StoryPhase.Exploration,300,t=>Ctx(StoryPhase.Exploration,false)).Count);}
 [Test] public void ActionsAreNeverSpammed(){
  foreach(var seed in new[]{1,2,3,4,5}){
   var log=Run(seed,StoryPhase.Pursuit,600);
   for(int i=1;i<log.Count;i++)Assert.True(log[i].t-log[i-1].t>=8f,"seed "+seed+": two actions only "+(log[i].t-log[i-1].t)+"s apart");
   for(float w=0;w<600-60;w+=15)Assert.True(log.Count(e=>e.t>=w&&e.t<w+60)<=5,"seed "+seed+": too many actions in one minute near "+w);
  }
 }
 [Test] public void EveryKindRespectsItsCooldown(){
  var log=Run(7,StoryPhase.Pursuit,1200);
  foreach(var g in log.GroupBy(e=>e.a.Kind)){var times=g.Select(e=>e.t).OrderBy(x=>x).ToList();for(int i=1;i<times.Count;i++)Assert.True(times[i]-times[i-1]>=14f,g.Key+" repeated after "+(times[i]-times[i-1]));}
 }
 [Test] public void NeverTheSameKindTwiceInARowProactively(){
  var log=Run(3,StoryPhase.Exploration,900).Where(e=>!e.a.Reaction).ToList();
  for(int i=1;i<log.Count;i++)Assert.True(log[i].a.Kind!=log[i-1].a.Kind,"repeat "+log[i].a.Kind);
 }
 [Test] public void IntensityRisesWithPhaseAndFallsBackAfterwards(){
  var d=new ThreatDirector(1);float t=0;
  for(;t<120;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.Exploration));float explore=d.Intensity;
  for(;t<300;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.Pursuit));float pursuit=d.Intensity;
  for(;t<600;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.Ended));float ended=d.Intensity;
  Assert.True(pursuit>explore+.2f,"pursuit "+pursuit+" vs explore "+explore);Assert.True(ended<.1f,"intensity falls: "+ended);
 }
 [Test] public void IntensityBreathes(){
  var d=new ThreatDirector(1);var vals=new List<float>();
  for(float t=0;t<400;t+=.25f){d.Tick(.25f,Ctx(StoryPhase.Pursuit));if(t>150)vals.Add(d.Intensity);}
  Assert.True(vals.Max()-vals.Min()>.08f,"range "+(vals.Max()-vals.Min()));
 }
 [Test] public void PlayerOpeningAWindowGetsItClosed(){
  var d=new ThreatDirector(5);ThreatAction hit=null;
  for(float t=0;t<30;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.Exploration));
  d.OnPlayer("window.open",true,CarMood.Sarcastic);
  for(float u=0;u<12&&hit==null;u+=.25f){var a=d.Tick(.25f,Ctx(StoryPhase.Exploration));if(a!=null&&a.Kind==ThreatKind.WindowClose)hit=a;}
  Assert.True(hit!=null&&hit.Reaction,"window closed in reaction");Assert.True(hit.Line.Length>0,"the car says something");
 }
 [Test] public void ReactionsHaveARealDelay(){
  var d=new ThreatDirector(5);for(float t=0;t<30;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.Exploration));
  d.OnPlayer("climate.change",true,CarMood.Sarcastic);float elapsed=0;bool found=false;
  while(!found&&elapsed<20){elapsed+=.25f;var a=d.Tick(.25f,Ctx(StoryPhase.Exploration));if(a!=null&&a.Kind==ThreatKind.ClimateReset)found=true;}
  Assert.True(found&&elapsed>=3.5f&&elapsed<=8f,"climate reset after "+elapsed);
 }
 [Test] public void ThreeDoorAttemptsCycleTheLocks(){
  var d=new ThreatDirector(9);for(float t=0;t<20;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.Exploration));
  d.OnPlayer("door.try",true,CarMood.Sarcastic);d.OnPlayer("door.try",true,CarMood.Sarcastic);
  bool early=false;for(int i=0;i<8;i++){var a=d.Tick(.25f,Ctx(StoryPhase.Exploration));if(a!=null&&a.Kind==ThreatKind.LockCycle)early=true;}
  Assert.False(early,"two attempts are not enough");
  d.OnPlayer("door.try",true,CarMood.Sarcastic);bool cycled=false;for(int i=0;i<12;i++){var a=d.Tick(.25f,Ctx(StoryPhase.Exploration));if(a!=null&&a.Kind==ThreatKind.LockCycle)cycled=true;}
  Assert.True(cycled,"third attempt cycles the locks (CLICK CLICK CLICK)");
 }
 [Test] public void DiscoveriesMakeTheCarAccelerateAndTint(){
  var d=new ThreatDirector(2);for(float t=0;t<40;t+=.25f)d.Tick(.25f,Ctx(StoryPhase.RoadChess));
  d.OnDiscovery(true);var kinds=new HashSet<ThreatKind>();
  for(float t=0;t<15;t+=.25f){var a=d.Tick(.25f,Ctx(StoryPhase.RoadChess));if(a!=null)kinds.Add(a.Kind);}
  Assert.True(kinds.Contains(ThreatKind.Accelerate)&&kinds.Contains(ThreatKind.TintUp),"car is afraid of what you learned: "+string.Join(",",kinds));
 }
 [Test] public void NoPhysicalInterferenceWhileASmallChessPuzzleIsOpen(){
  var log=Run(4,StoryPhase.Exploration,300,t=>new ThreatContext{Phase=StoryPhase.Exploration,Hostile=true,Mood=CarMood.Sarcastic,ChessActive=true,PuzzleBusy=true});
  Assert.Eq(0,log.Count);
 }
 [Test] public void FinalChessAllowsPuzzleInterference(){
  var log=Run(4,StoryPhase.Final,400,t=>new ThreatContext{Phase=StoryPhase.Final,Hostile=true,Mood=CarMood.Nervous,ChessActive=true});
  Assert.True(log.Any(e=>e.a.Kind==ThreatKind.PuzzleInterference),"the car interferes with the final board");
 }
 [Test] public void ContestsAreCappedAtTwo(){
  var d=new ThreatDirector(6);int takeaways=0;
  for(float t=0;t<600;t+=.25f){var a=d.Tick(.25f,new ThreatContext{Phase=StoryPhase.Pursuit,Hostile=true,Mood=CarMood.Angry,OpenContests=2});if(a!=null&&!a.Reaction&&ThreatDirector.ContestSystem(a.Kind)!="")takeaways++;}
  Assert.Eq(0,takeaways,"no new takeaway while two contests are already open");
 }
 [Test] public void DeterministicForASeed(){
  string Sig(int seed)=>string.Join(";",Run(seed,StoryPhase.Pursuit,300).Select(e=>e.t+":"+e.a));
  Assert.Eq(Sig(11),Sig(11));Assert.True(Sig(11)!=Sig(12),"different seeds differ");
 }
 [Test] public void EveryLineContextHasLinesForEveryMood(){
  foreach(var c in CarVoice.Contexts)foreach(CarMood m in Enum.GetValues(typeof(CarMood)))Assert.True(CarVoice.Count(c,m)>0,c+"/"+m);
 }
 [Test] public void MoodFollowsWhatThePlayerHasLearned(){
  Func<HashSet<string>,Func<string,bool>> has=s=>f=>s.Contains(f);
  Assert.Eq(CarMood.Helpful,MoodModel.For(has(new HashSet<string>())));
  Assert.Eq(CarMood.Controlling,MoodModel.For(has(new HashSet<string>{"lockdown"})));
  Assert.Eq(CarMood.Sarcastic,MoodModel.For(has(new HashSet<string>{"lockdown","glovebox-authenticated"})));
  Assert.Eq(CarMood.Angry,MoodModel.For(has(new HashSet<string>{"lockdown","glovebox-authenticated","outside.contact"})));
  Assert.Eq(CarMood.Nervous,MoodModel.For(has(new HashSet<string>{"collision.started"})));
  Assert.Eq(CarMood.Afraid,MoodModel.For(has(new HashSet<string>{"collision.started","rule.used-hidden"})));
 }
 [Test] public void ContestWonByRepeatedPresses(){
  var c=new ContestBoard();c.Begin("window",0);Assert.False(c.Press("window",1));Assert.False(c.Press("window",2));Assert.True(c.Press("window",3));Assert.True(c.Freed.Contains("window"));Assert.False(c.IsOpen("window"));
  var d=new ContestBoard();d.Begin("seat",0);d.Press("seat",1);d.Press("seat",2);Assert.False(d.Press("seat",20),"slow presses do not accumulate");
 }
}
}
