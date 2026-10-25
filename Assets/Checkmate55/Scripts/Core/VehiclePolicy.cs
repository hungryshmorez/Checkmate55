using System;
namespace Checkmate55.Core {
public struct VehicleInputs {
 public bool Hostile,RouteOverride,CheckBrake,PoliceBrake,Final,Stopped;
 public float Cruise,ThreatBoost,PoliceBrakeMph,FinalMph;
}
// The target speed the car drives toward. Pure so that the story, the police and the threat director all agree.
public static class VehiclePolicy {
 public const float NormalRideMph=53.7f;     // 24 m/s: the original relaxed ride
 public static float TargetMph(VehicleInputs v){
  if(v.Stopped)return 0;
  if(!v.Hostile)return NormalRideMph;
  float t=v.RouteOverride?42f:v.Cruise;
  if(v.CheckBrake)t=Math.Min(t,48f);
  t+=v.ThreatBoost;
  if(v.Final)t=Math.Max(t,v.FinalMph);
  if(v.PoliceBrake)t=Math.Min(t,v.PoliceBrakeMph);
  return Math.Max(0,Math.Min(140,t));
 }
 public static int Tier(float mph)=>mph>=110?3:mph>=90?2:mph>=70?1:0;
}
}
