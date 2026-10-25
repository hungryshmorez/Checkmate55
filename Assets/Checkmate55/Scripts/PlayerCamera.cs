using UnityEngine;
using UnityEngine.InputSystem;
namespace Checkmate55 {
public sealed class PlayerCamera : MonoBehaviour {
 public float yaw,pitch=12,sensitivity=1;
 public Camera view;
 public SceneManager game;
 public bool Dragging {get;private set;}
 Vector2 start,last; PhysicalControl pressedControl; bool held;Vector3 home; float lean;bool focusing;float focusYaw,focusPitch;
 // OVERRIDE: Interaction Focus - looking steadily at an interactable leans toward it, gently centres it and narrows the FOV.
 PhysicalControl focusSeen;float dwell,nudgeYaw,nudgePitch;Vector3 focusLean;
 public float FocusBlend {get;private set;}
 public float FovShake;                              // extra FOV kick used by hard braking / impact (decays by itself)
 public Vector3 ExternalLean;                       // threat effects (seatbelt, creeping seat) push the head by this much (parent space)
 public HandRig Hands;
 public bool CinematicActive;public Vector3 CinematicPos;public Quaternion CinematicRot;      // an exterior shot (the walk-out after an escape ending) overrides the seat view
 void LateUpdate(){if(CinematicActive){transform.position=CinematicPos;transform.rotation=CinematicRot;}}
 public void SetBodyVisible(bool visible){var body=FindFirstObjectByType<PlayerBody>();if(body!=null)foreach(var r in body.GetComponentsInChildren<Renderer>(true))r.enabled=visible;if(Hands!=null)foreach(var r in Hands.GetComponentsInChildren<Renderer>(true))r.enabled=visible;}
 public bool InputLocked;                           // spectating (AI passenger / the car): the human cannot look around or touch the cabin
 void Awake(){home=transform.localPosition;view=GetComponent<Camera>();}
 void Update(){
  if(game==null)return;
  UpdateFocus();
  if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame) game.TogglePause();
  if(game.Paused){held=false;return;}
  var k=InputLocked?null:Keyboard.current;
  if(k!=null){float dx=(k.rightArrowKey.isPressed||k.dKey.isPressed?1:0)-(k.leftArrowKey.isPressed||k.aKey.isPressed?1:0);float dy=(k.downArrowKey.isPressed||k.sKey.isPressed?1:0)-(k.upArrowKey.isPressed||k.wKey.isPressed?1:0);if(dx!=0||dy!=0){game.Tutorial.Looked=true;focusing=false;}yaw+=dx*60*Time.deltaTime;pitch+=dy*60*Time.deltaTime;
   lean=Mathf.MoveTowards(lean,(k.eKey.isPressed?1:0)-(k.qKey.isPressed?1:0),Time.deltaTime*4);
   if(k.spaceKey.wasPressedThisFrame)game.Interactions.UseCenter();
   if(k.rKey.wasPressedThisFrame) Recenter();if(k.gKey.wasPressedThisFrame&&game.Override!=null&&game.Override.RideGame!=null)game.Override.RideGame.Open();if(k.tabKey.wasPressedThisFrame)game.Seats.Cycle();if(k.hKey.wasPressedThisFrame)game.Tutorial.Hint();
  }
  bool down=false,up=false,pressed=false; Vector2 pos=last;
  if(Touchscreen.current!=null&&Touchscreen.current.primaryTouch.press.isPressed){var t=Touchscreen.current.primaryTouch;pos=t.position.ReadValue();down=t.press.wasPressedThisFrame;pressed=true;}
  else if(Touchscreen.current!=null&&Touchscreen.current.primaryTouch.press.wasReleasedThisFrame){pos=Touchscreen.current.primaryTouch.position.ReadValue();up=true;}
  else if(Mouse.current!=null){pos=Mouse.current.position.ReadValue();down=Mouse.current.leftButton.wasPressedThisFrame;up=Mouse.current.leftButton.wasReleasedThisFrame;pressed=Mouse.current.leftButton.isPressed;}
  if(InputLocked){down=false;up=false;pressed=false;held=false;}
  if(down&&!game.UI.OverButton(pos)){held=true;start=last=pos;pressedControl=game.Interactions.Pick(pos);Dragging=false;game.Audio.Begin();}
  if(held&&pressed){var delta=pos-last;if(Vector2.Distance(pos,start)>Mathf.Max(12,Screen.dpi*.07f))Dragging=true;if(Dragging){focusing=false;float scale=110f/Mathf.Min(Screen.width,Screen.height)*sensitivity;yaw+=delta.x*scale;pitch-=delta.y*scale;game.Tutorial.Looked=true;}last=pos;}
  if(held&&up){bool canceled=Touchscreen.current!=null&&Touchscreen.current.primaryTouch.phase.ReadValue()==UnityEngine.InputSystem.TouchPhase.Canceled;if(!Dragging&&!canceled)game.Interactions.UseControl(pressedControl);held=false;pressedControl=null;Dragging=false;}
  if(focusing){yaw=Mathf.LerpAngle(yaw,focusYaw,1-Mathf.Exp(-Time.deltaTime*3));pitch=Mathf.Lerp(pitch,focusPitch,1-Mathf.Exp(-Time.deltaTime*3));if(Mathf.Abs(yaw-focusYaw)+Mathf.Abs(pitch-focusPitch)<.1f)focusing=false;}
  yaw=Mathf.Clamp(yaw,-168,168);pitch=Mathf.Clamp(pitch,-73,83);
  float sway=game.Motion.Sway;
  transform.localRotation=Quaternion.Euler(pitch+nudgePitch,yaw+nudgeYaw,-sway*.9f);
  transform.localPosition=home+(game.Seats?game.Seats.Offset:Vector3.zero)+(game.Ride?game.Ride.BoardingOffset:Vector3.zero)+new Vector3(lean*.08f+sway*.006f,Mathf.Sin(Time.time*1.7f)*.002f,0)+focusLean+(Hands!=null?Hands.ReachLean:Vector3.zero)+ExternalLean;
 }
 // Normal FOV 71 (landscape); focused 56-62. Portrait uses a taller baseline so the same proportional narrowing applies.
 public static float FocusFov(float baseFov,float blend)=>baseFov*(1f-.17f*blend);
 void UpdateFocus(){
  float dt=Time.deltaTime;var seen=game.Interactions!=null?game.Interactions.Focus:null;
  bool chess=game.Chess&&game.Chess.Active;
  bool eligible=seen!=null&&seen.highlight&&!held&&!Dragging&&!chess&&!seen.id.StartsWith("chess-");
  if(eligible&&seen==focusSeen)dwell+=dt;else dwell=0;focusSeen=seen;
  float target=eligible&&dwell>.35f?1:0;
  FocusBlend=Mathf.MoveTowards(FocusBlend,target,dt*(target>FocusBlend?2.2f:3.4f));
  float baseFov=Screen.width<Screen.height?94:(chess?65:71);
  float fov=FocusFov(baseFov,Mathf.SmoothStep(0,1,FocusBlend))+FovShake;FovShake=Mathf.MoveTowards(FovShake,0,dt*14f);
  view.fieldOfView=Mathf.Lerp(view.fieldOfView,fov,dt*(target>0||FocusBlend>0?6:3));
  // subtle centring + a small lean toward the target; both are additive and never change the player's own yaw / pitch
  Vector3 wantNudge=Vector3.zero;Vector3 wantLean=Vector3.zero;
  if(seen!=null&&FocusBlend>.01f){
   Vector3 local=transform.InverseTransformPoint(seen.transform.position);float hyp=Mathf.Sqrt(local.x*local.x+local.z*local.z);
   float yawErr=Mathf.Atan2(local.x,Mathf.Max(.05f,local.z))*Mathf.Rad2Deg,pitchErr=Mathf.Atan2(-local.y,Mathf.Max(.05f,hyp))*Mathf.Rad2Deg;
   wantNudge=new Vector3(Mathf.Clamp(yawErr,-8,8)*.5f,Mathf.Clamp(pitchErr,-7,7)*.5f,0)*FocusBlend;
   Vector3 d=transform.parent!=null?transform.parent.InverseTransformPoint(seen.transform.position)-transform.localPosition:local;d.y*=.35f;wantLean=d.normalized*(Mathf.Min(.1f,d.magnitude*.08f))*FocusBlend;
  }
  nudgeYaw=Mathf.Lerp(nudgeYaw,wantNudge.x,dt*5);nudgePitch=Mathf.Lerp(nudgePitch,wantNudge.y,dt*5);focusLean=Vector3.Lerp(focusLean,wantLean,dt*5);
 }
 public void Recenter(){FocusAngles(0,game.Chess&&game.Chess.Active?61:12);}
 public void FocusAngles(float y,float p){focusYaw=y;focusPitch=p;focusing=true;}
 public void FocusControl(Transform target){var e=Quaternion.LookRotation(target.position-transform.position).eulerAngles;FocusAngles(Mathf.DeltaAngle(0,e.y),Mathf.DeltaAngle(0,e.x));}
 public void LookAtControl(Transform target){var d=target.position-transform.position;var e=Quaternion.LookRotation(d).eulerAngles;yaw=Mathf.DeltaAngle(0,e.y);pitch=Mathf.DeltaAngle(0,e.x);transform.rotation=Quaternion.Euler(pitch,yaw,0);}
}
}
