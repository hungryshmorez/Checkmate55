using System;
using System.Collections.Generic;
namespace Checkmate55.Core {
public enum PoliceEventType { SirenFaint, SirenLoud, Visible, RadioLine, RadioGarbled, ForceBrake, ForceLane, PitAttempt, FallBack, Closing }
public sealed class PoliceEvent {
 public PoliceEventType Type;public string Text="";public float Value;public int Lane=-1;
 public override string ToString()=>Type+(Text.Length>0?"("+Text+")":"");
}
public sealed class PoliceInputs {
 public bool Jamming;        // the car is jamming external audio
 public bool Tinted;         // rear glass tinted: the police cannot see in
 public bool Signalled;      // the passenger has signalled (overhead SOS) recently
 public bool Transmitted;    // location has been transmitted
 public float CarMph=80;
 public int CarLane=1;
}
// The pursuit. Police actions are not scenery: braking and lane forcing change the car's speed tier and the active Road Chess lane.
public sealed class PoliceModel {
 readonly Rng rng;
 public bool Active{get;private set;}
 public float Elapsed{get;private set;}
 public float Distance{get;private set;}=420;      // metres behind the car
 public float Siren{get;private set;}              // 0..1 audibility
 public bool Visible{get;private set;}
 public float Pressure{get;private set;}           // 0..1: how much of the car's route integrity the police have broken
 public int Cars{get;private set;}=1;
 public int Brakes{get;private set;}
 public int LaneForces{get;private set;}
 public int Pits{get;private set;}
 public bool ForcedStop{get;private set;}
 float nextAction=14,brakeUntil;
 bool sirenFaint,sirenLoud,seen;
 public event Action<PoliceEvent> Event;
 public PoliceModel(int seed){rng=new Rng(seed);}
 public void Begin(){if(Active)return;Active=true;Elapsed=0;Distance=420;Siren=0;Visible=false;Pressure=.1f;nextAction=16;}
 public bool BrakeActive(float now)=>Elapsed<brakeUntil;
 void Emit(PoliceEvent e){Event?.Invoke(e);}
 public List<PoliceEvent> Tick(float dt,PoliceInputs i){
  var events=new List<PoliceEvent>();
  if(!Active)return events;
  Elapsed+=dt;
  void Add(PoliceEvent e){events.Add(e);Emit(e);}
  if(!sirenFaint&&Elapsed>=6){sirenFaint=true;Add(new PoliceEvent{Type=PoliceEventType.SirenFaint,Text="sirens far behind"});}
  if(!seen&&Elapsed>=14){seen=true;Visible=true;Add(new PoliceEvent{Type=PoliceEventType.Visible,Text="Police behind the vehicle"});}
  if(!sirenLoud&&Elapsed>=24){sirenLoud=true;Add(new PoliceEvent{Type=PoliceEventType.SirenLoud});}
  // approach: pressure pulls them in, accelerating / jamming pushes them back
  float rate=(i.CarMph-70)*.06f-(Pressure-.3f)*6f+(i.Tinted?.8f:0);
  Distance=Math.Max(18,Math.Min(600,Distance+rate*dt*(Visible?1:0)));
  float target=Elapsed<6?0:(sirenLoud?(i.Jamming?.45f:1f):.35f);Siren+=(target-Siren)*Math.Min(1,dt*.8f);
  float dp=0;if(i.Signalled)dp+=.035f;if(i.Transmitted)dp+=.02f;if(!i.Tinted&&Visible)dp+=.012f;if(i.Jamming)dp-=.05f;dp-=.004f;
  Pressure=Math.Max(0,Math.Min(1,Pressure+dp*dt));
  Cars=Pressure>.7f?3:Pressure>.4f?2:1;
  if(Elapsed>=nextAction&&Visible){
   nextAction=Elapsed+rng.Range(15f,23f);
   float r=rng.Next();
   if(r<.35f){ // radio: unintelligible while jammed
    string[] lines={"Passenger in the rear seat, can you hear us?","This is the police. Stay calm. We are tracking your vehicle.","If you can hear us, signal with the interior light.","We are not letting this vehicle leave the 55."};
    Add(new PoliceEvent{Type=i.Jamming?PoliceEventType.RadioGarbled:PoliceEventType.RadioLine,Text=i.Jamming?"...ssshk... pass—ger... ssshk...":lines[rng.Range(0,lines.Length)]});
   }else if(r<.62f){Brakes++;brakeUntil=Elapsed+6.5f;Add(new PoliceEvent{Type=PoliceEventType.ForceBrake,Value=25f,Text="Police force the car to brake"});}
   else if(r<.85f){LaneForces++;int lane=i.CarLane==1?(rng.Chance(.5f)?0:2):1;Add(new PoliceEvent{Type=PoliceEventType.ForceLane,Lane=lane,Text="Police force a lane change"});}
   else if(Distance<90){Pits++;Add(new PoliceEvent{Type=PoliceEventType.PitAttempt,Text="Police attempt a PIT manoeuvre"});}
   else Add(new PoliceEvent{Type=PoliceEventType.Closing,Text="Police closing"});
  }
  if(Pressure>=.95f&&Brakes>=2&&!ForcedStop){ForcedStop=true;}
  return events;
 }
}
}
