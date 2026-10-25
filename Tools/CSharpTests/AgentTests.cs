using System;
using System.Collections.Generic;
using Checkmate55.Core;
namespace Checkmate55.Tests {
// The AI passenger's micro-protocols are run against the REAL puzzle models (ClimateModel, RhythmLock, RadioModel), not mocks.
[TestFixture] public sealed class AgentTests {
 static readonly string[] Card={"SERVICE ACCESS · UNIT 55\nMAINTENANCE ONLY\n\n"+StoryText.MaintenanceCardBack};
 [Test] public void ParsesPatternsFromTheCardText(){
  int fan;var c=StoryAgent.ParseClimate(Card,out fan);Assert.True(c!=null&&c.Length==3);Assert.Eq(19,c[0]);Assert.Eq(23,c[1]);Assert.Eq(17,c[2]);Assert.Eq(3,fan);
  var l=StoryAgent.ParseLight(Card);Assert.True(l!=null&&l.Length==2);Assert.Eq(2,l[0]);Assert.Eq(3,l[1]);
  Assert.Eq(4,StoryAgent.ParsePreset("Preset 4, balance fully left, fader fully to the rear."));Assert.Eq(0,StoryAgent.ParsePreset("nothing"));
 }
 [Test] public void AgentSolvesTheClimateServiceTest(){
  var m=new ClimateModel();var agent=new StoryAgent();float t=0;int presses=0;
  for(int i=0;i<4000&&!m.Solved;i++){
   t+=.1f;m.Tick(.1f);
   var v=new PassengerView{BeatId="climate",Time=t,Temp=m.Temp,Fan=m.Fan,PatternReady=m.PatternReady};v.Notes.AddRange(Card);
   var step=agent.Next(v,0);if(step==null||step.Kind!=IntentKind.Interact)continue;presses++;
   switch(step.ControlId){case "climate-plus":m.Adjust(1);break;case "climate-minus":m.Adjust(-1);break;case "climate-fan":m.CycleFan();break;default:throw new Exception("unexpected control "+step.ControlId);}
  }
  Assert.True(m.Solved,"climate pattern solved by the agent");Assert.True(presses<40,"efficient: "+presses+" presses");
 }
 [Test] public void AgentSolvesTheDomeLightRhythm(){
  var lockp=new RhythmLock(StoryText.LightPattern);var agent=new StoryAgent();float t=0;
  for(int i=0;i<4000&&!lockp.Solved;i++){
   t+=.05f;lockp.Tick(t);
   var v=new PassengerView{BeatId="light",Time=t};v.Notes.AddRange(Card);
   var step=agent.Next(v,0);if(step!=null&&step.Kind==IntentKind.Interact){Assert.Eq("overhead-light",step.ControlId);lockp.Press(t);}
  }
  Assert.True(lockp.Solved,"rhythm solved by the agent");
 }
 [Test] public void AgentFindsThePreset4Transmission(){
  var r=new RadioModel();var agent=new StoryAgent();float t=0;
  for(int i=0;i<4000&&!r.RecordingFound;i++){
   t+=.1f;r.Tick(.1f);
   var v=new PassengerView{BeatId="radio",Time=t,Hint="Preset 4, balance fully left, fader fully to the rear.",Station=r.Station,Balance=r.Balance,Fader=r.Fader,Volume=r.Volume,Muted=r.Muted};
   var step=agent.Next(v,0);if(step==null||step.Kind!=IntentKind.Interact)continue;
   switch(step.ControlId){case "radio-bal-l":r.BalanceStep(-1);break;case "radio-fade-r":r.FaderStep(-1);break;case "radio-vol-up":r.VolumeStep(1);break;case "radio-mute":r.ToggleMute();break;default:if(step.ControlId.StartsWith("radio-p"))r.Select(step.ControlId[7]-'0');else throw new Exception("unexpected "+step.ControlId);break;}
  }
  Assert.True(r.RecordingFound,"the agent isolated the rear-left speaker on preset 4");
 }
 [Test] public void AgentRecoversWhenTheCarMutesTheRadio(){
  var r=new RadioModel();r.CarMute();r.CarVolume(0);r.CarMute();var agent=new StoryAgent();float t=0;
  for(int i=0;i<4000&&!r.RecordingFound;i++){
   t+=.1f;r.Tick(.1f);
   var v=new PassengerView{BeatId="radio",Time=t,Hint="Preset 4, balance fully left, fader fully to the rear.",Station=r.Station,Balance=r.Balance,Fader=r.Fader,Volume=r.Volume,Muted=r.Muted};
   var step=agent.Next(v,0);if(step==null||step.Kind!=IntentKind.Interact)continue;
   switch(step.ControlId){case "radio-bal-l":r.BalanceStep(-1);break;case "radio-fade-r":r.FaderStep(-1);break;case "radio-vol-up":r.VolumeStep(1);break;case "radio-mute":r.ToggleMute();break;default:r.Select(step.ControlId[7]-'0');break;}
  }
  Assert.True(r.RecordingFound,"works from a muted, silent start");
 }
 [Test] public void AgentRideBeatExploresThenRequestsStop(){
  var agent=new StoryAgent();float t=0;var touched=new HashSet<string>();bool stop=false;
  for(int i=0;i<3000&&!stop;i++){t+=.1f;var step=agent.Next(new PassengerView{BeatId="ride",Time=t},0);if(step!=null&&step.Kind==IntentKind.Interact){if(step.ControlId=="request-stop"){stop=true;Assert.True(touched.Count>=5,"explored "+touched.Count+" before stopping");Assert.True(t>=75,"waited for the ride clock");}else touched.Add(step.ControlId);}}
  Assert.True(stop,"asked to stop");
 }
 [Test] public void UnknownBeatsFallBackToTheGuideRing(){
  var agent=new StoryAgent();Assert.True(agent.Next(new PassengerView{BeatId="trim",Time=5},0)==null);Assert.True(agent.Next(new PassengerView{BeatId="",Time=5},0)==null);
  var ai=new AIPassengerController(0,CarSkill.Relentless,null,1);var v=new PassengerView{BeatId="trim",TargetControl="security-trim",Time=10};
  Intent first=null;for(int i=0;i<40&&(first==null||first.Kind==IntentKind.Idle);i++)first=ai.Decide(v,.5f);
  Assert.Eq("security-trim",first.ControlId);
 }
}
}
