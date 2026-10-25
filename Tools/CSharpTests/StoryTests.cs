using System;
using System.Collections.Generic;
using System.Linq;
using Checkmate55.Core;
namespace Checkmate55.Tests {
[TestFixture] public class StoryTests {
 static Func<string,bool> Has(HashSet<string> s)=>f=>s.Contains(f);
 [Test] public void PacingTargetsMatchTheDesignDocument(){
  var g=new StoryGraph();
  Assert.InRange(g.ParTotal,420,620,"speedrun route (planned) must be about 8-10 minutes of play");
  Assert.InRange(g.FirstRunTotal,1700,2100,"first run (planned) is 28-35 minutes including reading time");
  Assert.InRange(g.FirstRunTotal/2f,850,1200,"experienced replay (planned at half the first run) is 15-20 minutes");
 }
 [Test] public void BeatsFormAConnectedCampaignFromBoardingToTheEnding(){
  var g=new StoryGraph();var flags=new HashSet<string>();int guard=0;var order=new List<string>();
  while(true){
   var b=g.Current(Has(flags));if(b==null)break;if(guard++>200)throw new Exception("campaign does not terminate");
   order.Add(b.Id);foreach(var f in b.Done)flags.Add(f);
  }
  Assert.True(flags.Contains("ending.any"),"campaign reaches an ending; stopped after "+string.Join(",",order));
  Assert.Eq(g.Beats.Count,order.Count,"every beat is reachable exactly once: "+string.Join(",",order));
 }
 [Test] public void EveryFlagANeedsRequirementDependsOnIsProducedByAnEarlierBeat(){
  var g=new StoryGraph();var produced=new HashSet<string>{"lockdown"};
  // flags set by systems instead of by a beat's completion
  var system=new HashSet<string>();
  foreach(var b in g.Beats){
   foreach(var n in b.Needs)Assert.True(produced.Contains(n)||system.Contains(n),b.Id+" needs "+n+" which no earlier beat produces");
   foreach(var d in b.Done)produced.Add(d);
  }
 }
 [Test] public void ObjectiveFollowsProgress(){
  var g=new StoryGraph();var f=new HashSet<string>();
  Assert.Eq("ride",g.Current(Has(f)).Id);f.Add("lockdown");Assert.Eq("door",g.Current(Has(f)).Id);
  f.Add("tested-door");Assert.Eq("window",g.Current(Has(f)).Id);
  foreach(var x in new[]{"tested-window","glovebox-interest","authentication-known","clue.note","clue.service","trim-inspected"})f.Add(x);
  Assert.Eq("auth",g.Current(Has(f)).Id);Assert.Eq("authenticate",g.Target(Has(f)));
  f.Add("CHESS_CHALLENGE_COMPLETED");f.Add("glovebox-authenticated");Assert.Eq("contents",g.Current(Has(f)).Id);
  Assert.Eq("route-card",g.Target(Has(f)),"first missing pickup");f.Add("clue.route-card");Assert.Eq("usb-device",g.Target(Has(f)));
 }
 [Test] public void PuzzlesDoneOutOfOrderAreNotLost(){
  var g=new StoryGraph();var f=new HashSet<string>{"lockdown","tested-door","tested-window","glovebox-interest","authentication-known","clue.note","clue.service","trim-inspected","CHESS_CHALLENGE_COMPLETED","glovebox-authenticated","clue.route-card","item.usb","item.phone","item.maintenance-card","security.diagnostics","security.window","item.knight","rule.jump","rule.charge"};
  f.Add("puzzle.radio"); // the player found the transmission early
  Assert.Eq("seat",g.Current(Has(f)).Id,"guide continues with the next unfinished beat");
  foreach(var x in new[]{"puzzle.seat","dock.card","puzzle.climate","puzzle.overhead","maintenance-mode","security.diversion","security.route"})f.Add(x);
  Assert.Eq(null,g.Active(Has(f)).FirstOrDefault(b=>b.Id=="radio")?.Id,"radio is already complete");
 }
 [Test] public void PhasesProgressInOrder(){
  var g=new StoryGraph();var last=-1;
  foreach(var b in g.Beats){Assert.True((int)b.Phase>=last-1,b.Id+" phase "+b.Phase+" regresses");last=Math.Max(last,(int)b.Phase);}
 }
 [Test] public void ProgressIsMonotonic(){
  var g=new StoryGraph();var flags=new HashSet<string>();float prev=-1;
  while(true){var b=g.Current(Has(flags));float p=g.Progress(Has(flags));Assert.True(p>=prev,"progress went down");prev=p;if(b==null)break;foreach(var d in b.Done)flags.Add(d);}
  Assert.True(prev>.99f,"ends at 100%: "+prev);
 }
 [Test] public void ClueTextsAgreeWithThePuzzleSolutions(){
  Assert.True(StoryText.MaintenanceCardBack.Contains("19")&&StoryText.MaintenanceCardBack.Contains("23")&&StoryText.MaintenanceCardBack.Contains("17"));
  Assert.True(StoryText.MaintenanceCardBack.Contains("FAN 3"));
  Assert.True(StoryText.RadioRecording.Contains("preset four")&&StoryText.RadioPreset==4);
  Assert.True(StoryText.MaintenanceCardBack.Contains("two")&&StoryText.MaintenanceCardBack.Contains("three"));
 }
}
}
