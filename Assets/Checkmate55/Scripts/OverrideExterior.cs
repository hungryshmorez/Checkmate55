using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
// The outside of the car. Until now the cabin was a floating interior: stepping out (or looking back from the road) showed nothing around it.
// This builds the full body at startup from primitives and the scene's own materials - doors and fenders with wheel arches, sills, pillars,
// roof, hood with grille and lamps, trunk, bumpers, plate, wipers, mirror arms and four turning wheels - and keeps the world honest about it:
// traffic and police can no longer drive through the car (they steer clear), and escape endings play a short walk-out around the car.
public sealed class OverrideExterior {
 readonly OverrideDirector d;SceneManager game=>d.game;
 Transform root,cabin;Material paint,trim,black,tire,glassMat,headMat,tailMat,plateMat;
 readonly List<Transform> spinners=new List<Transform>();readonly List<Transform> steerers=new List<Transform>();
 float spin;Transform[] traffic;int[] side;Renderer[] tailRenders;
 public const float HalfWidth=1.18f,Front=3.62f,Rear=-3.36f,GroundY=-.42f,WheelR=.4f;
 public bool Built{get;private set;}
 public OverrideExterior(OverrideDirector director){d=director;}
 // ---------------------------------------------------------------------------------------------------------------- geometry helpers
 static Transform P(string n,Transform parent,Vector3 pos,Vector3 size,Material m,Vector3 euler=default,PrimitiveType type=PrimitiveType.Cube){
  var go=GameObject.CreatePrimitive(type);go.name=n;var t=go.transform;t.SetParent(parent,false);t.localPosition=pos;t.localScale=size;t.localEulerAngles=euler;
  var c=go.GetComponent<Collider>();if(c!=null)UnityEngine.Object.Destroy(c);
  var r=go.GetComponent<Renderer>();if(m!=null)r.sharedMaterial=m;return t;
 }
 static Transform Bar(string n,Transform parent,Vector3 a,Vector3 b,float width,Material m){var t=P(n,parent,(a+b)/2,new Vector3(width,(b-a).magnitude,width),m);t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);return t;}
 // ---------------------------------------------------------------------------------------------------------------- build
 public void Build(){
  var interior=UnityEngine.Object.FindFirstObjectByType<VehicleInterior>();if(interior==null)return;cabin=interior.transform;
  paint=Mats.Solid("Body paint",new Color(.055f,.085f,.13f),.92f,.65f);trim=Mats.Find("Satin aluminium")??Mats.Metal("Exterior trim",new Color(.6f,.66f,.7f));
  black=Mats.Solid("Exterior black",new Color(.018f,.02f,.024f),.55f);tire=Mats.Solid("Tyre rubber",new Color(.025f,.025f,.027f),.15f);
  glassMat=Mats.Find("Clear laminated glazing")??Mats.Glass("Exterior glass",new Color(.62f,.8f,.86f,.05f));
  headMat=Mats.Glow("Headlamp",new Color(.95f,.97f,1f),1.5f);tailMat=Mats.Glow("Tail lamp",new Color(1f,.05f,.04f),.9f);plateMat=Mats.Solid("Number plate",new Color(.92f,.9f,.8f),.3f);
  root=new GameObject("Exterior • body shell").transform;root.SetParent(cabin,false);
  BuildBodySides();BuildRoofAndGlass();BuildNose();BuildTail();BuildWheels();
  var env=game.Motion.environment;traffic=game.Motion.traffic??new Transform[0];side=new int[traffic.Length];
  Built=true;
 }
 void BuildBodySides(){
  P("Underbody",root,new Vector3(0,-.1f,.1f),new Vector3(2.34f,.1f,6.9f),black);
  var hinge=game.Ride!=null?game.Ride.boardingDoor:null;
  for(int s=-1;s<=1;s+=2){
   float x=s*1.17f;
   // front fender (ahead of the wheel arch) and the band above the arch
   P("Front fender fore",root,new Vector3(x,.53f,3.28f),new Vector3(.07f,.62f,.56f),paint);
   P("Front fender arch brow",root,new Vector3(x,.92f,2.78f),new Vector3(.07f,.16f,1.55f),paint);
   P("Front door skin",root,new Vector3(x,.61f,1.18f),new Vector3(.07f,.78f,1.65f),paint);
   // the rear door swings with the foundation's hinge so boarding still works
   Transform doorParent=root;Vector3 shift=Vector3.zero;
   if(s<0&&hinge!=null){doorParent=hinge;shift=-hinge.localPosition;}
   P("Rear door skin",doorParent,new Vector3(x,.61f,-.55f)+shift,new Vector3(.07f,.78f,1.78f),paint);
   P("Rear door seam front",doorParent,new Vector3(x*1.012f,.61f,.355f)+shift,new Vector3(.02f,.78f,.02f),black);
   P("Rear door seam rear",doorParent,new Vector3(x*1.012f,.61f,-1.455f)+shift,new Vector3(.02f,.78f,.02f),black);
   P("Rear door handle",doorParent,new Vector3(x*1.018f,.9f,-.17f)+shift,new Vector3(.03f,.035f,.26f),trim);
   P("Rear quarter fore",root,new Vector3(x,.61f,-1.615f),new Vector3(.07f,.78f,.33f),paint);
   P("Rear arch brow",root,new Vector3(x,.92f,-2.25f),new Vector3(.07f,.16f,.94f),paint);
   P("Rear quarter aft",root,new Vector3(x,.61f,-2.985f),new Vector3(.07f,.78f,.54f),paint);
   P("Front door seam",root,new Vector3(x*1.012f,.61f,2.0f),new Vector3(.02f,.78f,.02f),black);
   P("Front door handle",root,new Vector3(x*1.018f,.9f,1.1f),new Vector3(.03f,.035f,.26f),trim);
   P("Side sill",root,new Vector3(x*1.005f,.14f,.1f),new Vector3(.09f,.12f,3.8f),black);
   P("Belt chrome",root,new Vector3(x*1.012f,1.018f,.05f),new Vector3(.025f,.022f,6.5f),trim);
   P("Wheel arch liner front",root,new Vector3(s*.96f,.62f,2.55f),new Vector3(.16f,.5f,1.0f),black);
   P("Wheel arch liner rear",root,new Vector3(s*.96f,.62f,-2.25f),new Vector3(.16f,.5f,1.0f),black);
   // pillars, wrapped on the outside so the glass reads as framed
   Bar("A pillar cover",root,new Vector3(s*1.15f,1.0f,2.12f),new Vector3(s*1.0f,1.92f,1.43f),.15f,paint);
   Bar("B pillar cover",root,new Vector3(s*1.15f,1.02f,.35f),new Vector3(s*1.15f,1.9f,.35f),.14f,black);
   Bar("C pillar cover",root,new Vector3(s*1.14f,1.02f,-1.6f),new Vector3(s*1.0f,1.92f,-1.34f),.17f,paint);
   Bar("Window top trim",root,new Vector3(s*1.1f,1.9f,-1.34f),new Vector3(s*1.1f,1.9f,1.5f),.05f,trim);
   Bar("Mirror arm",root,new Vector3(s*1.19f,1.0f,1.56f),new Vector3(s*1.32f,1.04f,1.53f),.05f,black);
   P("Door mirror repeater",root,new Vector3(s*1.2f,.98f,2.9f),new Vector3(.02f,.05f,.14f),Mats.Glow("Indicator amber",new Color(1f,.6f,.1f),.35f));
  }
 }
 void BuildRoofAndGlass(){
  P("Roof panel",root,new Vector3(0,2.115f,.05f),new Vector3(2.2f,.06f,3.0f),paint);
  P("Panoramic roof glass",root,new Vector3(0,2.15f,-.3f),new Vector3(1.5f,.012f,1.6f),black);
  P("Roof rail left",root,new Vector3(-1.08f,2.16f,.05f),new Vector3(.05f,.05f,3.0f),trim);P("Roof rail right",root,new Vector3(1.08f,2.16f,.05f),new Vector3(.05f,.05f,3.0f),trim);
  P("Antenna fin",root,new Vector3(0,2.19f,-1.25f),new Vector3(.05f,.08f,.2f),black,new Vector3(-20,0,0));
  // rear window: a sloped pane from the roof edge down to the parcel shelf
  P("Rear window glass",root,new Vector3(0,1.52f,-1.83f),new Vector3(2.0f,.008f,1.14f),glassMat,new Vector3(-41.8f,0,0));
  P("Rear window frame",root,new Vector3(0,1.15f,-2.27f),new Vector3(2.1f,.1f,.1f),black);
  // wipers lie on the windscreen
  Bar("Wiper blade driver",root,new Vector3(-.75f,1.17f,2.08f),new Vector3(.1f,1.37f,1.91f),.022f,black);
  Bar("Wiper blade passenger",root,new Vector3(-.3f,1.14f,2.12f),new Vector3(.8f,1.3f,1.97f),.022f,black);
  P("Cowl",root,new Vector3(0,1.02f,2.13f),new Vector3(2.2f,.09f,.22f),black);
 }
 void BuildNose(){
  P("Hood",root,new Vector3(0,.99f,2.88f),new Vector3(2.3f,.07f,1.52f),paint,new Vector3(7,0,0));
  P("Hood crease left",root,new Vector3(-.38f,1.015f,2.88f),new Vector3(.03f,.012f,1.4f),paint,new Vector3(7,0,0));P("Hood crease right",root,new Vector3(.38f,1.015f,2.88f),new Vector3(.03f,.012f,1.4f),paint,new Vector3(7,0,0));
  P("Front fascia",root,new Vector3(0,.7f,3.58f),new Vector3(2.3f,.4f,.08f),paint);
  P("Grille",root,new Vector3(0,.64f,3.63f),new Vector3(1.0f,.26f,.03f),black);
  for(int i=0;i<5;i++)P("Grille slat",root,new Vector3(0,.54f+i*.055f,3.65f),new Vector3(.96f,.012f,.015f),trim);
  var badge=P("Badge",root,new Vector3(0,.79f,3.64f),new Vector3(.12f,.12f,.012f),trim,default,PrimitiveType.Cylinder);badge.localEulerAngles=new Vector3(90,0,0);
  P("Front bumper",root,new Vector3(0,.3f,3.58f),new Vector3(2.34f,.36f,.2f),paint);P("Front splitter",root,new Vector3(0,.1f,3.64f),new Vector3(2.0f,.07f,.16f),black);
  for(int s=-1;s<=1;s+=2){
   P("Headlamp",root,new Vector3(s*.82f,.74f,3.62f),new Vector3(.48f,.12f,.05f),headMat);
   P("Headlamp bezel",root,new Vector3(s*.82f,.74f,3.6f),new Vector3(.54f,.17f,.05f),black);
   P("Daytime strip",root,new Vector3(s*.82f,.62f,3.64f),new Vector3(.42f,.02f,.02f),headMat);
   P("Fog lamp",root,new Vector3(s*.85f,.3f,3.69f),new Vector3(.2f,.06f,.03f),headMat);
  }
 }
 void BuildTail(){
  P("Trunk lid",root,new Vector3(0,1.07f,-2.8f),new Vector3(2.3f,.07f,1.1f),paint,new Vector3(-2,0,0));
  P("Rear fascia",root,new Vector3(0,.72f,-3.28f),new Vector3(2.3f,.6f,.08f),paint);
  P("Rear bumper",root,new Vector3(0,.3f,-3.3f),new Vector3(2.34f,.36f,.2f),paint);P("Diffuser",root,new Vector3(0,.12f,-3.33f),new Vector3(1.8f,.1f,.16f),black);
  var tails=new List<Renderer>();
  for(int s=-1;s<=1;s+=2){
   tails.Add(P("Tail lamp",root,new Vector3(s*.82f,.9f,-3.33f),new Vector3(.5f,.11f,.05f),tailMat).GetComponent<Renderer>());
   P("Reverse lamp",root,new Vector3(s*.5f,.4f,-3.42f),new Vector3(.12f,.04f,.02f),plateMat);
   P("Exhaust tip",root,new Vector3(s*.7f,.1f,-3.42f),new Vector3(.12f,.12f,.1f),trim,new Vector3(90,0,0),PrimitiveType.Cylinder);
  }
  tailRenders=tails.ToArray();
  P("Number plate",root,new Vector3(0,.58f,-3.35f),new Vector3(.52f,.12f,.02f),plateMat);
  var go=new GameObject("Plate lettering");go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(0,.58f,-3.364f);go.transform.localEulerAngles=new Vector3(0,180,0);
  var tm=go.AddComponent<TextMesh>();var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");tm.font=font;go.GetComponent<Renderer>().sharedMaterial=font.material;tm.text="CM-55";tm.anchor=TextAnchor.MiddleCenter;tm.fontSize=64;tm.characterSize=.0125f;tm.color=new Color(.08f,.08f,.1f);
  var depth=go.GetComponent<Renderer>();if(depth!=null)depth.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
 }
 void BuildWheels(){
  float[] zs={2.55f,-2.25f};
  for(int s=-1;s<=1;s+=2)for(int k=0;k<2;k++){
   var mount=new GameObject((k==0?"Front ":"Rear ")+(s<0?"left":"right")+" wheel").transform;mount.SetParent(root,false);mount.localPosition=new Vector3(s*1.03f,-.02f,zs[k]);
   var spinner=new GameObject("Spinner").transform;spinner.SetParent(mount,false);
   var tyre=P("Tyre",spinner,Vector3.zero,new Vector3(WheelR*2,.14f,WheelR*2),tire,new Vector3(0,0,90),PrimitiveType.Cylinder);
   var rim=P("Rim",spinner,new Vector3(s*.075f,0,0),new Vector3(WheelR*1.42f,.02f,WheelR*1.42f),trim,new Vector3(0,0,90),PrimitiveType.Cylinder);
   var hub=P("Hub",spinner,new Vector3(s*.09f,0,0),new Vector3(.12f,.015f,.12f),black,new Vector3(0,0,90),PrimitiveType.Cylinder);
   for(int i=0;i<5;i++){var spoke=P("Spoke",spinner,new Vector3(s*.085f,0,0),new Vector3(.02f,.012f,WheelR*1.35f),black);spoke.localRotation=Quaternion.Euler(i*36f,0,0);}
   P("Brake disc",spinner,new Vector3(-s*.05f,0,0),new Vector3(WheelR*1.1f,.012f,WheelR*1.1f),Mats.Solid("Brake disc",new Color(.35f,.33f,.32f),.4f,.5f),new Vector3(0,0,90),PrimitiveType.Cylinder);
   spinners.Add(spinner);if(k==0)steerers.Add(mount);
  }
 }
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void Update(float dt){
  if(!Built)return;
  float speed=game.Motion.speed;spin=(spin+speed/WheelR*Mathf.Rad2Deg*dt)%360f;
  for(int i=0;i<spinners.Count;i++)spinners[i].localRotation=Quaternion.Euler(spin,0,0);
  float steer=Mathf.Clamp(game.Motion.Sway*9f,-16f,16f);
  foreach(var m in steerers)m.localRotation=Quaternion.Euler(0,steer,0);
  bool braking=game.Motion.acceleration<-1.2f||(d.PolicePresenter!=null&&d.PolicePresenter.BrakeActive);
  if(tailRenders!=null)foreach(var r in tailRenders)Mats.SetGlow(r.sharedMaterial,new Color(1f,.05f,.04f),braking?2.4f:.9f);
  KeepTrafficClear(dt);
 }
 // The world scrolls past a stationary car, so "driving through the car" is a traffic problem. Anything that would overlap the body
 // steers into a neighbouring lane in good time (never off the carriageway), whichever way the player's own lane changes.
 void KeepTrafficClear(float dt){
  var env=game.Motion.environment;if(env==null||traffic==null)return;
  for(int i=0;i<traffic.Length;i++){
   var t=traffic[i];if(t==null||!t.gameObject.activeInHierarchy)continue;
   float wz=t.position.z;float local=t.localPosition.x;float rel=local+env.localPosition.x;
   bool zone=wz>Rear-5.5f&&wz<Front+48f;
   if(!zone||Mathf.Abs(rel)>3.3f){if(!zone)side[i]=0;continue;}
   if(side[i]==0){
    // pick the neighbouring lane that stays on the road; prefer the side the vehicle is already leaning to
    float a=3.8f-env.localPosition.x,b=-3.8f-env.localPosition.x;bool aOk=Mathf.Abs(a)<=5.2f,bOk=Mathf.Abs(b)<=5.2f;
    side[i]=aOk&&bOk?(rel>=0?1:-1):aOk?1:-1;
   }
   float target=side[i]*3.8f-env.localPosition.x;float rate=(wz<Front+10f?7f:3.2f);
   var p=t.localPosition;p.x=Mathf.MoveTowards(p.x,target,rate*dt);t.localPosition=p;
  }
 }
 // ---------------------------------------------------------------------------------------------------------------- walking out (escape endings)
 // The camera leaves the seat through the rear-left door, stands on the verge, turns round and looks at the car, then drifts slowly round it.
 public IEnumerator PlayExit(){
  var cam=game.Player;var tr=cam.transform;if(!Built){yield break;}
  var hinge=game.Ride.boardingDoor;
  cam.InputLocked=true;game.Player.SetBodyVisible(false);
  var start=tr.position;var startRot=tr.rotation;
  // the door opens
  for(float t=0;t<1f;t+=Time.deltaTime/1.1f){if(hinge!=null)hinge.localRotation=Quaternion.Euler(0,Mathf.Lerp(0,64,Mathf.SmoothStep(0,1,t)),0);yield return null;}
  Vector3 door=cabin.TransformPoint(new Vector3(-1.75f,1.45f,-.5f));Vector3 outside=cabin.TransformPoint(new Vector3(-5.4f,1.7f,-.2f));Vector3 look=cabin.TransformPoint(new Vector3(0,1.1f,0));
  cam.CinematicActive=true;cam.CinematicPos=start;cam.CinematicRot=startRot;
  // 1) out through the door
  for(float t=0;t<1f;t+=Time.deltaTime/2.2f){float k=Mathf.SmoothStep(0,1,t);Vector3 p=k<.5f?Vector3.Lerp(start,door,k*2):Vector3.Lerp(door,outside,(k-.5f)*2);cam.CinematicPos=p;cam.CinematicRot=Quaternion.Slerp(startRot,Quaternion.LookRotation(Vector3.Lerp(look,cabin.TransformPoint(new Vector3(0,1.6f,-6)),0)-p),Mathf.SmoothStep(0,1,t));yield return null;}
  // 2) a slow arc round the back of the car
  for(float t=0;t<1f;t+=Time.deltaTime/5.5f){
   float ang=Mathf.Lerp(-90f,-150f,Mathf.SmoothStep(0,1,t));Vector3 c=cabin.position+new Vector3(0,0,0);
   Vector3 off=Quaternion.AngleAxis(ang,Vector3.up)*Vector3.forward*Mathf.Lerp(5.6f,7.5f,t);
   Vector3 p=new Vector3(c.x+off.x,1.7f,c.z+off.z);cam.CinematicPos=p;cam.CinematicRot=Quaternion.LookRotation(look-p);yield return null;
  }
  yield return new WaitForSeconds(.6f);
 }
 public void EndExit(){var cam=game.Player;cam.CinematicActive=false;}
}
}
