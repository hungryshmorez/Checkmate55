using System;
using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
// Road Chess configuration + live vehicle mirror. Standard chess stays authoritative (chess.js); the Road layer in
// Tools/Chess/road-engine.cjs only adds the explicit rules below, gated by lane, speed tier and discovered rules.
public sealed class RoadChessRules {
 public const int Shift=1,Jump=2,Charge=4,Overclock=8,Castle=16,Ghost=32,Reversal=64,AllPublic=Shift|Jump|Charge|Overclock|Castle|Ghost;
 public bool Available=>true;
 public bool Enabled;public int ActiveRoadLane,VehicleSpeedTier,OverclockLane;public float SpeedMph;
 // Modes that are not the Story (Passenger Chess, Road Chess, Calibration) know the public rules from the start.
 public bool AllRulesKnown;
 public readonly HashSet<string> DiscoveredRules=new HashSet<string>();
 public readonly Dictionary<string,string> SpecialSquareEffects=new Dictionary<string,string>();
 public Func<ChessSnapshot,IEnumerable<ChessMove>> TemporaryMoveProvider;
 public event Action<ChessMove> ChessMoveConsequence;
 public event Action<string> VehicleEventConsequence;
 public static readonly string[] LaneNames={"LEFT","CENTER","RIGHT"},LaneFiles={"A–C","D–E","F–H"};
 public static int Lane(string square)=>square[0]<'d'?0:square[0]<'f'?1:2;
 public static int TierOf(float mph)=>mph>=110?3:mph>=90?2:mph>=70?1:0;
 public int KnownMask{get{
  if(AllRulesKnown)return AllPublic|(DiscoveredRules.Contains("rule.reversal")?Reversal:0);
  int m=Shift; // the vehicle itself documents Lane Shift on the first Road Chess challenge
  if(DiscoveredRules.Contains("rule.jump"))m|=Jump;if(DiscoveredRules.Contains("rule.charge"))m|=Charge;if(DiscoveredRules.Contains("rule.overclock"))m|=Overclock;
  if(DiscoveredRules.Contains("rule.castle"))m|=Castle;if(DiscoveredRules.Contains("rule.ghost"))m|=Ghost;if(DiscoveredRules.Contains("rule.reversal"))m|=Reversal;return m;}}
 public void Sync(SceneManager g){ActiveRoadLane=Mathf.Clamp(g.Motion.lane,0,2);SpeedMph=g.Motion.speed*2.23694f;VehicleSpeedTier=TierOf(SpeedMph);foreach(var f in g.State.Flags)if(f.StartsWith("rule."))DiscoveredRules.Add(f);}
 // Fills the vehicle part of an engine request. The engine owns everything else (shields, hazards, cards, charges).
 public void Configure(ChessRequest q){q.roadEnabled=Enabled;q.speedMph=SpeedMph;q.lane=ActiveRoadLane;q.overclockLane=OverclockLane;q.jumpKnown=(KnownMask&Jump)!=0;q.chargeKnown=(KnownMask&Charge)!=0;q.knownMask=KnownMask;}
 public string Readout{get{
  string tier=VehicleSpeedTier==0?"BELOW 70  ·  STANDARD CHESS":VehicleSpeedTier==1?"70+  ·  ROAD JUMP":VehicleSpeedTier==2?"90+  ·  JUMP + LANE CHARGE":"110+  ·  ROAD OVERCLOCK";
  return "LANE "+LaneFiles[ActiveRoadLane]+" "+LaneNames[ActiveRoadLane]+"  /  "+Mathf.RoundToInt(SpeedMph)+" MPH\n"+tier;}}
 public void OnChessMove(ChessMove move)=>ChessMoveConsequence?.Invoke(move);
 public void OnVehicleEvent(string id)=>VehicleEventConsequence?.Invoke(id);
}
}
