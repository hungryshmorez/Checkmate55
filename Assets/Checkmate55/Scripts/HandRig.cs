using System;
using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
public enum HandVerb { Reach, Press, Pull, Push, Grab, Turn, Slide, Insert, Pickup, Open, Close }
// Where and how the hand touches an object. Every important control gets an anchor: a verb, an approach offset in the
// control's own space and a follow-through. Anything without an entry gets a sensible default from its action type.
public sealed class HandAnchor {
 public HandVerb Verb=HandVerb.Press;public Vector3 Offset;public float Travel=.05f;public float Degrees;public Vector3 Axis=Vector3.forward;
}
public static class HandAnchors {
 public static HandAnchor For(PhysicalControl c){
  string id=c.id;var a=new HandAnchor();
  switch(id){
   case "glovebox":a.Verb=HandVerb.Grab;a.Travel=.13f;return a;                       // grabs the latch, then pulls the compartment open
   case "climate-fan":a.Verb=HandVerb.Turn;a.Degrees=70;return a;
   case "passenger-seat":case "driver-seat":case "rear-seat":a.Verb=HandVerb.Slide;a.Travel=.04f;return a;
   case "armrest":a.Verb=HandVerb.Open;a.Travel=.12f;return a;
   case "service-dock":case "usb-port-service":case "usb-port-phone":a.Verb=HandVerb.Insert;a.Travel=.04f;return a;
   case "route-card":case "maint-card":case "usb-device":case "phone-device":case "lost-knight":a.Verb=HandVerb.Pickup;return a;
  }
  if(id.EndsWith("-handle")){a.Verb=HandVerb.Pull;a.Travel=.08f;return a;}
  if(id.EndsWith("-pocket")||id=="seat-scratch"){a.Verb=HandVerb.Grab;a.Travel=.07f;return a;}
  switch(c.action){
   case ControlAction.Handle:a.Verb=HandVerb.Pull;a.Travel=.08f;break;
   case ControlAction.Locked:a.Verb=HandVerb.Grab;a.Travel=.05f;break;
   case ControlAction.Pocket:a.Verb=HandVerb.Grab;a.Travel=.07f;break;
   case ControlAction.Climate:a.Verb=HandVerb.Turn;a.Degrees=45;break;
   case ControlAction.Armrest:a.Verb=HandVerb.Open;a.Travel=.1f;break;
   case ControlAction.Seat:a.Verb=HandVerb.Slide;a.Travel=.04f;break;
   default:a.Verb=HandVerb.Press;break;
  }
  return a;
 }
}
// A first-person arm: shoulder -> elbow -> wrist solved with two-bone IK, and a hand with articulated fingers, built from the
// same materials as the seated body. It reaches, presses, pulls, turns, grabs, slides, inserts and picks up, then returns to rest.
public sealed class HandRig : MonoBehaviour {
 public static bool Immediate;                 // verification scripts and accessibility settings may switch the animation off
 public static bool Enabled=true;
 sealed class Limb {
  public Transform root,upper,fore,hand;public Transform[][] joints=new Transform[5][];
  public Vector3 shoulder,rest;public bool left;public float[] curl=new float[5];public Vector3 lastTipWorld;
 }
 Limb right,left,active;SceneManager game;Camera cam;PlayerBody body;Material skin,sleeve;
 PhysicalControl current;HandAnchor anchor;Action onContact;bool contacted;float timer;
 Vector3 targetWorld,approachWorld,startWorld,lastFollow;float reachTime,followTime;
 const float ContactTime=.07f,RetractTime=.22f;
 public bool Busy=>active!=null;
 public Vector3 ReachLean{get;private set;}                    // camera lean that helps a far reach (parent space)
 public static HandRig Create(SceneManager g){
  var rig=new GameObject("First-person hands").AddComponent<HandRig>();rig.transform.SetParent(g.Player.transform,false);rig.game=g;rig.cam=g.Player.view;rig.body=FindFirstObjectByType<PlayerBody>();rig.Build();return rig;
 }
 Material Mat(string name,Color fallback){var m=Mats.Find(name);return m?m:Mats.Solid(name,fallback);}
 void Build(){skin=Mat("Skin",new Color(.57f,.36f,.23f));sleeve=Mat("Navy cotton",new Color(.1f,.15f,.22f));right=MakeLimb(false);left=MakeLimb(true);right.root.gameObject.SetActive(false);left.root.gameObject.SetActive(false);}
 Transform Seg(string name,Transform parent,Material m,PrimitiveType type=PrimitiveType.Capsule){var t=GameObject.CreatePrimitive(type).transform;t.name=name;t.SetParent(parent,false);var r=t.GetComponent<Renderer>();r.sharedMaterial=m;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;Destroy(t.GetComponent<Collider>());return t;}
 Limb MakeLimb(bool isLeft){
  var l=new Limb{left=isLeft};float s=isLeft?-1:1;
  l.root=new GameObject(isLeft?"Left arm":"Right arm").transform;l.root.SetParent(transform,false);
  l.shoulder=new Vector3(.27f*s,-.30f,-.02f);l.rest=new Vector3(.20f*s,-.40f,.38f);
  l.upper=Seg("Jacket sleeve upper",l.root,sleeve);l.fore=Seg("Jacket forearm",l.root,sleeve);
  l.hand=new GameObject("Hand").transform;l.hand.SetParent(l.root,false);
  var palm=Seg("Palm",l.hand,skin,PrimitiveType.Cube);palm.localPosition=new Vector3(0,0,.045f);palm.localScale=new Vector3(.082f,.024f,.09f);
  for(int i=0;i<4;i++){
   var knuckle=new GameObject("Finger "+i).transform;knuckle.SetParent(l.hand,false);knuckle.localPosition=new Vector3((-.03f+i*.02f)*s,0,.09f);
   var j1=Seg("Proximal",knuckle,skin);j1.localScale=new Vector3(.017f,.019f,.017f);j1.localRotation=Quaternion.Euler(90,0,0);j1.localPosition=new Vector3(0,0,.019f);
   var mid=new GameObject("Mid joint").transform;mid.SetParent(knuckle,false);mid.localPosition=new Vector3(0,0,.038f);
   var j2=Seg("Middle",mid,skin);j2.localScale=new Vector3(.015f,.017f,.015f);j2.localRotation=Quaternion.Euler(90,0,0);j2.localPosition=new Vector3(0,0,.017f);
   l.joints[i]=new[]{knuckle,mid};
  }
  var th=new GameObject("Thumb").transform;th.SetParent(l.hand,false);th.localPosition=new Vector3(.042f*s,-.002f,.02f);th.localRotation=Quaternion.Euler(0,30f*s,0);
  var t1=Seg("Thumb base",th,skin);t1.localScale=new Vector3(.02f,.021f,.02f);t1.localRotation=Quaternion.Euler(90,0,0);t1.localPosition=new Vector3(0,0,.022f);
  var tm=new GameObject("Thumb joint").transform;tm.SetParent(th,false);tm.localPosition=new Vector3(0,0,.042f);
  var t2=Seg("Thumb tip",tm,skin);t2.localScale=new Vector3(.017f,.019f,.017f);t2.localRotation=Quaternion.Euler(90,0,0);t2.localPosition=new Vector3(0,0,.017f);
  l.joints[4]=new[]{th,tm};
  for(int i=0;i<5;i++)l.curl[i]=.05f;
  return l;
 }
 // ---- poses: 0 = straight, 1 = fully curled; indices 0..3 fingers, 4 thumb ------------------------------------------------------------
 static readonly float[] Open={.05f,.05f,.05f,.05f,.1f},Point={.0f,.95f,.95f,.95f,.7f},Fist={.9f,.95f,.95f,.95f,.7f},Pinch={.55f,.2f,.1f,.1f,.6f},Flat={0,0,0,0,0},Hook={.65f,.7f,.7f,.7f,.2f};
 static float[] PoseFor(HandVerb v){switch(v){case HandVerb.Press:case HandVerb.Insert:return Point;case HandVerb.Pull:case HandVerb.Grab:case HandVerb.Open:return Hook;case HandVerb.Turn:case HandVerb.Pickup:return Pinch;case HandVerb.Slide:case HandVerb.Push:case HandVerb.Close:return Flat;default:return Open;}}
 void ApplyPose(Limb l,float[] target,float dt,float speed){
  for(int i=0;i<5;i++){
   l.curl[i]=Mathf.MoveTowards(l.curl[i],target[i],dt*speed);float c=l.curl[i];
   if(i<4){l.joints[i][0].localRotation=Quaternion.Euler(c*75,0,0);l.joints[i][1].localRotation=Quaternion.Euler(c*90,0,0);}
   else{float s=l.left?-1:1;l.joints[4][0].localRotation=Quaternion.Euler(-c*10,Mathf.Lerp(30,-5,c)*s,0);l.joints[4][1].localRotation=Quaternion.Euler(c*50,0,0);}
  }
 }
 // ---- arm solver -------------------------------------------------------------------------------------------------------------------------
 void SolveArm(Limb l,Vector3 wristLocal,Quaternion handRot,float handScale){
  Vector3 s=l.shoulder,d=wristLocal-s;float dist=Mathf.Max(.05f,d.magnitude);
  // The arm may stretch: first-person arms are only ever seen from a camera at the head, never measured.
  float total=Mathf.Max(.62f,dist*1.03f);float half=total*.5f;
  Vector3 bend=(Vector3.down+Vector3.right*(l.left?-.55f:.55f)).normalized;
  Vector3 perp=Vector3.ProjectOnPlane(bend,d/dist).normalized;
  float h=Mathf.Sqrt(Mathf.Max(0,half*half-(dist*.5f)*(dist*.5f)));Vector3 elbow=s+d*.5f+perp*h;
  Place(l.upper,s,elbow,.075f);Place(l.fore,elbow,wristLocal,.062f);
  l.hand.localPosition=wristLocal;l.hand.localRotation=handRot;l.hand.localScale=Vector3.one*handScale;
 }
 static void Place(Transform seg,Vector3 a,Vector3 b,float width){
  Vector3 d=b-a;float len=d.magnitude;if(len<1e-4f)return;
  seg.localPosition=(a+b)*.5f;seg.localRotation=Quaternion.FromToRotation(Vector3.up,d/len);seg.localScale=new Vector3(width,len*.5f+width*.35f,width);
 }
 // ---- performing an action ---------------------------------------------------------------------------------------------------------------
 // `contact` runs at the instant the fingertip meets the object, which is when the control actually responds.
 public void Perform(PhysicalControl c,Action contact){
  if(!Enabled||Immediate||c==null||cam==null){contact();return;}
  var a=HandAnchors.For(c);
  Vector3 world=c.transform.TransformPoint(a.Offset);Vector3 camLocal=cam.transform.InverseTransformPoint(world);
  if(camLocal.z<.05f||camLocal.magnitude>4.8f){contact();return;}          // behind the passenger: no sensible reach
  var limb=camLocal.x<-.32f?left:right;
  if(active!=null){                                                     // rapid taps never wait for the animation: finish the old touch now
   if(!contacted){contacted=true;try{onContact?.Invoke();}catch(Exception e){Debug.LogException(e);}}
   if(active!=limb){active.root.gameObject.SetActive(false);body?.SetArmVisible(active.left?-1:1,true);}
   startWorld=active.lastTipWorld;
  }else startWorld=cam.transform.TransformPoint(limb.rest);
  active=limb;current=c;anchor=a;onContact=contact;contacted=false;timer=0;lastFollow=Vector3.zero;
  limb.root.gameObject.SetActive(true);body?.SetArmVisible(limb.left?-1:1,false);
  targetWorld=world;Vector3 toCam=(cam.transform.position-targetWorld).normalized;
  approachWorld=targetWorld+toCam*(a.Verb==HandVerb.Insert||a.Verb==HandVerb.Pickup?.07f:.055f);
  reachTime=Mathf.Clamp(.14f+camLocal.magnitude*.06f,.16f,.32f);followTime=a.Verb==HandVerb.Press?.06f:a.Verb==HandVerb.Turn?.3f:.28f;
 }
 void Update(){
  if(game==null||game.Paused)return;
  if(active==null){ReachLean=Vector3.Lerp(ReachLean,Vector3.zero,Time.deltaTime*6f);return;}
  var l=active;float dt=Time.deltaTime;timer+=dt;float t=timer;
  float reachEnd=reachTime,contactEnd=reachEnd+ContactTime,followEnd=contactEnd+followTime,retractEnd=followEnd+RetractTime;
  Vector3 restWorld=cam.transform.TransformPoint(l.rest);
  var pose=PoseFor(anchor.Verb);Vector3 tip;Quaternion twist=Quaternion.identity;float retractK=0;
  if(t<reachEnd){tip=Vector3.Lerp(startWorld,approachWorld,Smooth(t/reachEnd));ApplyPose(l,pose,dt,6);}
  else if(t<contactEnd){tip=Vector3.Lerp(approachWorld,targetWorld,Smooth((t-reachEnd)/ContactTime));ApplyPose(l,pose,dt,9);}
  else if(t<followEnd){
   if(!contacted){contacted=true;try{onContact?.Invoke();}catch(Exception e){Debug.LogException(e);}}
   float k=Smooth((t-contactEnd)/followTime);Vector3 back=-cam.transform.forward;Vector3 follow=Vector3.zero;
   switch(anchor.Verb){
    case HandVerb.Pull:case HandVerb.Grab:case HandVerb.Open:follow=(back+Vector3.down*.55f).normalized*anchor.Travel*k;pose=Fist;break;     // toward the passenger, slightly down
    case HandVerb.Slide:case HandVerb.Push:case HandVerb.Close:follow=cam.transform.forward*anchor.Travel*k;break;
    case HandVerb.Insert:follow=cam.transform.forward*anchor.Travel*k;break;
    case HandVerb.Turn:twist=Quaternion.AngleAxis(anchor.Degrees*k,cam.transform.forward);break;
    case HandVerb.Pickup:pose=Pinch;follow=(Vector3.up+back).normalized*.06f*k;break;
    default:follow=cam.transform.forward*.012f*(1-k);break;
   }
   lastFollow=follow;tip=targetWorld+follow;ApplyPose(l,pose,dt,10);
  }
  else if(t<retractEnd){
   if(!contacted){contacted=true;try{onContact?.Invoke();}catch(Exception e){Debug.LogException(e);}}
   retractK=Smooth((t-followEnd)/RetractTime);tip=Vector3.Lerp(targetWorld+lastFollow,restWorld,retractK);ApplyPose(l,Open,dt,7);
  }
  else{
   if(!contacted){contacted=true;try{onContact?.Invoke();}catch(Exception e){Debug.LogException(e);}}
   l.root.gameObject.SetActive(false);body?.SetArmVisible(l.left?-1:1,true);active=null;current=null;onContact=null;return;
  }
  l.lastTipWorld=tip;
  // fingers point along the approach direction, relaxing toward the camera's forward on the way back
  Vector3 fwd=(targetWorld-approachWorld).normalized;if(fwd.sqrMagnitude<.01f)fwd=cam.transform.forward;
  if(retractK>0)fwd=Vector3.Slerp(fwd,cam.transform.forward,retractK);
  Vector3 up=Vector3.Cross(Vector3.Cross(fwd,cam.transform.up),fwd).normalized;if(up.sqrMagnitude<.01f)up=cam.transform.up;
  Quaternion worldRot=Quaternion.LookRotation(fwd,up)*twist;
  float dist=(tip-cam.transform.position).magnitude;float scale=Mathf.Clamp(dist/.8f,1f,2.8f);
  float tipLen=(anchor.Verb==HandVerb.Press||anchor.Verb==HandVerb.Insert?.17f:.10f)*scale;      // wrist sits behind the fingertip
  Vector3 wristWorld=tip-fwd*tipLen;
  SolveArm(l,cam.transform.InverseTransformPoint(wristWorld),Quaternion.Inverse(cam.transform.rotation)*worldRot,scale);
  float far=Mathf.Clamp01((dist-.9f)/1.4f);Vector3 dir=cam.transform.parent!=null?cam.transform.parent.InverseTransformDirection((targetWorld-cam.transform.position).normalized):Vector3.forward;dir.y*=.25f;
  ReachLean=Vector3.Lerp(ReachLean,dir*far*.14f,dt*6f);
 }
 static float Smooth(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
}
}
