using System;
using Checkmate55.Core;
namespace Checkmate55.Tests {
[TestFixture] public sealed class RideGameTests {
 [Test] public void QuotaCountsOnlyThePassengersOwnMoves(){
  var g=new RideGameModel();for(int i=0;i<RideGameModel.Quota-1;i++)g.OnPlayerMove();
  Assert.False(g.QuotaMet);Assert.Eq(1,g.Remaining);for(int i=0;i<10;i++)g.OnAutoMove();Assert.False(g.QuotaMet,"moves the car makes for you never count");
  g.OnPlayerMove();Assert.True(g.QuotaMet);Assert.Eq(0,g.Remaining);
 }
 [Test] public void AFinishedGameSatisfiesTheQuota(){var g=new RideGameModel();g.OnOver("w");Assert.True(g.QuotaMet);Assert.True(g.Over);}
 [Test] public void TheCarNagsThenPlaysForYou(){
  var g=new RideGameModel();var seen=new System.Collections.Generic.List<RideGameModel.Nag>();
  for(int t=0;t<400;t++){var n=g.Tick(1f,StoryPhase.Lockdown,true);if(n!=RideGameModel.Nag.None)seen.Add(n);if(n==RideGameModel.Nag.Forced)break;}
  Assert.Eq(3,seen.Count);Assert.Eq(RideGameModel.Nag.Reminder,seen[0]);Assert.Eq(RideGameModel.Nag.Warning,seen[1]);Assert.Eq(RideGameModel.Nag.Forced,seen[2]);
  Assert.Eq(0f,g.Waiting,"the clock restarts after the car moves for you");
 }
 [Test] public void MovingResetsTheClockAndTheNags(){
  var g=new RideGameModel();for(int t=0;t<150;t++)g.Tick(1f,StoryPhase.Lockdown,true);g.OnPlayerMove();
  Assert.Eq(0f,g.Waiting);Assert.Eq(RideGameModel.Nag.None,g.Tick(1f,StoryPhase.Lockdown,true));
  float elapsed=0;RideGameModel.Nag first=RideGameModel.Nag.None;while(first==RideGameModel.Nag.None&&elapsed<500){first=g.Tick(1f,StoryPhase.Lockdown,true);elapsed++;}
  Assert.Eq(RideGameModel.Nag.Reminder,first,"reminders come again after every move");
 }
 [Test] public void TheClockOnlyRunsWhenItShould(){
  var g=new RideGameModel();
  Assert.Eq(RideGameModel.Nag.None,g.Tick(9999,StoryPhase.Ride,true),"the opening ride is relaxed");
  Assert.Eq(RideGameModel.Nag.None,g.Tick(9999,StoryPhase.Final,true),"no ride-game clock during the collision course");
  Assert.Eq(RideGameModel.Nag.None,g.Tick(9999,StoryPhase.Lockdown,false),"nor while the host says the clock is stopped");
  Assert.True(RideGameModel.Interval(StoryPhase.Pursuit)<RideGameModel.Interval(StoryPhase.Lockdown),"the car gets less patient as the ride goes on");
 }
 [Test] public void MaterialLeadBecomesFinaleCharges(){
  Assert.Eq(0,RideGameModel.MaterialDiff(RideGameModel.StartFen));
  Assert.Eq(9,RideGameModel.MaterialDiff("rnb1kbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"));
  Assert.Eq(-10,RideGameModel.MaterialDiff("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/1NBQKBN1 w - - 0 1"),"missing both rooks");
  Assert.Eq(0,RideGameModel.BonusCharges(2));Assert.Eq(1,RideGameModel.BonusCharges(3));Assert.Eq(2,RideGameModel.BonusCharges(6));Assert.Eq(3,RideGameModel.BonusCharges(30));Assert.Eq(0,RideGameModel.BonusCharges(-9));
 }
 [Test] public void TheRouteCannotEndBeforeTheGameHasBeenPlayed(){
  var g=new StoryGraph();var flags=new System.Collections.Generic.HashSet<string>{"lockdown","security.cyber"};
  foreach(var b in g.Beats)if(b.Id!="ridegame"&&b.Id!="collision"&&b.Id!="final"&&b.Done!=null)foreach(var f in b.Done)flags.Add(f);
  Assert.Eq("ridegame",g.Current(f=>flags.Contains(f)).Id,"the next objective is the ride game");Assert.Eq("ridegame",g.Target(f=>flags.Contains(f)));
  flags.Add("ridegame.quota");Assert.Eq("collision",g.Current(f=>flags.Contains(f)).Id);
 }
 [Test] public void ClockFormat(){Assert.Eq("2:05",RideGameModel.Clock(125));Assert.Eq("0:00",RideGameModel.Clock(-3));}
}
}
