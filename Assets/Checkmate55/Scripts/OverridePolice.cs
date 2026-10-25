using System.Collections.Generic;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// Police materially change the ride: they force the car to brake (speed tier drops) and to change lane (the Road Chess active lane
// changes immediately). Sirens fade in first, then cruisers appear behind the vehicle; a PIT attempt pulls one alongside.
public sealed class OverridePolice {
 readonly OverrideDirector d;SceneManager game=>d.game;
 readonly List<Transform> cars=new List<Transform>();readonly List<Renderer> redLights=new List<Renderer>(),blueLights=new List<Renderer>();Transform root;
 float brakeUntil,pitTimer,pitSide=1;public float BrakeMph{get;private set;}=60;
 public bool BrakeActive=>Time.time<brakeUntil;
 Material body,stripe,redMat,blueMat;
 public OverridePolice(OverrideDirector director){d=director;}
 public void Initialize(){
  var env=game.Motion.environment;root=new GameObject("Pursuit • police cruisers").transform;if(env!=null)root.SetParent(env,false);
  body=Mats.Solid("Cruiser white",new Color(.92f,.93f,.95f),.6f);stripe=Mats.Solid("Cruiser stripe",new Color(.05f,.12f,.3f),.4f);redMat=Mats.Glow("Cruiser red",new Color(1f,.05f,.04f),.1f);blueMat=Mats.Glow("Cruiser blue",new Color(.1f,.25f,1f),.1f);
  for(int i=0;i<3;i++)cars.Add(MakeCar(i));
  foreach(var c in cars)c.gameObject.SetActive(false);
  d.Police.Event+=OnEvent;
 }
 Transform MakeCar(int i){
  var car=new GameObject("Cruiser "+i).transform;car.SetParent(root,false);
  Box("Body",car,new Vector3(0,.55f,0),new Vector3(1.85f,.75f,4.5f),body);Box("Cabin",car,new Vector3(0,1.12f,-.15f),new Vector3(1.6f,.55f,2.2f),stripe);Box("Door stripe",car,new Vector3(0,.55f,0),new Vector3(1.87f,.18f,3.2f),stripe);
  Box("Light bar",car,new Vector3(0,1.45f,-.15f),new Vector3(1.2f,.1f,.3f),stripe);
  var r=Box("Red",car,new Vector3(-.3f,1.52f,-.15f),new Vector3(.5f,.1f,.28f),Mats.Glow("Cruiser red "+i,new Color(1f,.05f,.04f),.1f));var b=Box("Blue",car,new Vector3(.3f,1.52f,-.15f),new Vector3(.5f,.1f,.28f),Mats.Glow("Cruiser blue "+i,new Color(.1f,.25f,1f),.1f));
  redLights.Add(r.GetComponent<Renderer>());blueLights.Add(b.GetComponent<Renderer>());
  Box("Rear window",car,new Vector3(0,1.05f,1.05f),new Vector3(1.4f,.4f,.05f),Mats.Solid("Cruiser glass",new Color(.05f,.08f,.1f),.9f));
  return car;
 }
 static Transform Box(string n,Transform p,Vector3 pos,Vector3 size,Material m){var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.name=n;t.SetParent(p,false);t.localPosition=pos;t.localScale=size;t.GetComponent<Renderer>().sharedMaterial=m;Object.Destroy(t.GetComponent<Collider>());return t;}
 void OnEvent(PoliceEvent e){
  switch(e.Type){
   case PoliceEventType.SirenFaint:d.Say("Sirens, far behind us.",false);break;
   case PoliceEventType.Visible:game.State.Flag("police.visible");game.Audio.Chime();d.Say(CarVoice.Line("police",d.Mood,new Rng(game.RunSeed+5)));d.Puzzles.ShowWarning("EXTERNAL VEHICLES: PURSUIT",6f);break;
   case PoliceEventType.RadioLine:d.Say("Officer: \""+e.Text+"\"",false);BrowserPlatform.Speak(e.Text,.8f,.98f,1f);break;
   case PoliceEventType.RadioGarbled:d.Say("Officer: \""+e.Text+"\"",false);break;
   case PoliceEventType.ForceBrake:BrakeMph=Mathf.Max(38f,d.Mph-e.Value);brakeUntil=Time.time+6.5f;d.Puzzles.ShowWarning("POLICE FORCE BRAKING · "+Mathf.RoundToInt(BrakeMph)+" MPH",5f);game.Audio.LockClick();game.Player.FovShake=2.5f;break;
   case PoliceEventType.ForceLane:if(e.Lane>=0){game.Motion.lane=e.Lane;}d.Puzzles.ShowWarning("POLICE FORCE LANE CHANGE → "+RoadChessRules.LaneNames[Mathf.Clamp(game.Motion.lane,0,2)],5f);game.Audio.LockClick();break;
   case PoliceEventType.PitAttempt:pitTimer=2.4f;pitSide=game.Motion.lane==0?1:-1;d.Say("Police move alongside!",false);break;
   case PoliceEventType.SirenLoud:break;
  }
 }
 public void Update(float dt){
  if(!d.Police.Active)return;
  var inp=new PoliceInputs{Jamming=d.AudioJam,Tinted=d.Puzzles.WindshieldAlpha>.3f||d.Puzzles.TintCar>.5f,Signalled=Time.time<d.Puzzles.SignalUntil,Transmitted=game.State.Has("outside.contact"),CarMph=d.Mph,CarLane=game.Motion.lane};
  d.Police.Tick(dt,inp);
  d.Sound.SetSiren(d.Police.Siren*(d.AudioJam?.5f:1f),.25f);
  // cruisers behind the vehicle (in road space, so they follow the lane the car drives in)
  bool show=d.Police.Visible;int n=d.Police.Cars;
  for(int i=0;i<cars.Count;i++){
   bool on=show&&i<n;if(cars[i].gameObject.activeSelf!=on)cars[i].gameObject.SetActive(on);if(!on)continue;
   // never closer than the car's own length: no cruiser drives through the car
   float z=-(Mathf.Max(d.Police.Distance,OverrideExterior.Front-OverrideExterior.Rear+4f)+i*13f);float carRoadX=game.Motion.environment!=null?-game.Motion.environment.localPosition.x:(game.Motion.lane-1)*3.5f;float x=carRoadX+(i==0?0:i==1?-3.4f:3.4f);     // relative to where the car actually is, mid lane change included
   if(i==0&&pitTimer>0){z=Mathf.Lerp(z,-1.2f,Mathf.SmoothStep(0,1,Mathf.Clamp01((2.4f-pitTimer)/1.2f)));x+=pitSide*Mathf.Lerp(0,3.4f,Mathf.SmoothStep(0,1,Mathf.Clamp01((2.4f-pitTimer)/1.2f)));}
   cars[i].localPosition=Vector3.Lerp(cars[i].localPosition,new Vector3(x,-.38f,z),Mathf.Clamp01(dt*3));
   bool phase=Mathf.Sin(Time.time*(7+i))>0;Mats.SetGlow(redLights[i].sharedMaterial,new Color(1f,.05f,.04f),phase?1.6f:.1f);Mats.SetGlow(blueLights[i].sharedMaterial,new Color(.1f,.25f,1f),phase?.1f:1.6f);
  }
  if(pitTimer>0){pitTimer-=dt;if(pitTimer<=0){game.Audio.ChessCapture();d.Sound.Thud();game.Player.FovShake=-5f;game.Motion.lane=Mathf.Clamp(game.Motion.lane+(int)(-pitSide),0,2);d.Puzzles.ShowWarning("PIT ATTEMPT · LANE FORCED",4f);}}
  if(d.Police.ForcedStop&&d.Contests.Freed.Count>=EndingRules.SystemsNeeded&&!d.Final.Ended)d.Final.Finish(Ending.SystemEscape);
 }
}
}
