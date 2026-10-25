#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Checkmate55 {
public sealed class FoundationTwoVerification : MonoBehaviour {
 readonly List<string> results=new List<string>();int failed;SceneManager g;Touchscreen touch;int touchId=10;
 void Check(bool ok,string text){results.Add((ok?"PASS: ":"FAIL: ")+text);if(!ok)failed++;}
 IEnumerator Frames(){yield return null;yield return null;}
 void Event(UnityEngine.InputSystem.TouchPhase phase,Vector2 pos){InputSystem.QueueStateEvent(touch,new TouchState{touchId=touchId,phase=phase,position=pos});}
 IEnumerator Tap(string id){var c=g.Registry.Get(id);g.Player.LookAtControl(c.transform);yield return Frames();Physics.SyncTransforms();var pos=(Vector2)g.Player.view.WorldToScreenPoint(c.transform.position);int before=c.UseCount;string hit=g.Interactions.Pick(pos)?.id;touchId++;Event(UnityEngine.InputSystem.TouchPhase.Began,pos);yield return Frames();bool began=touch.primaryTouch.press.isPressed;string phase=touch.primaryTouch.phase.ReadValue().ToString();Event(UnityEngine.InputSystem.TouchPhase.Ended,pos+new Vector2(2,1));yield return Frames();Check(c.UseCount==before+1,"Mobile tap reaches "+id+" exactly once in "+g.State.Current);if(c.UseCount!=before+1)results.Add("DETAIL: "+id+" hit="+hit+" pos="+pos+" began="+began+" phase="+phase+" held="+typeof(PlayerCamera).GetField("held",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(g.Player)+" touchCurrent="+(Touchscreen.current==touch)+" yaw="+g.Player.yaw);}
 IEnumerator Drag(bool transition){var c=g.Registry.Get("climate");g.Player.LookAtControl(c.transform);yield return Frames();var pos=(Vector2)g.Player.view.WorldToScreenPoint(c.transform.position);float yaw=g.Player.yaw;int count=c.UseCount;touchId++;Event(UnityEngine.InputSystem.TouchPhase.Began,pos);yield return Frames();if(transition)g.Ride.TriggerLockdown();Event(UnityEngine.InputSystem.TouchPhase.Moved,pos+new Vector2(90,0));yield return Frames();Event(UnityEngine.InputSystem.TouchPhase.Ended,pos+new Vector2(90,0));yield return Frames();Check(Mathf.Abs(g.Player.yaw-yaw)>5,"Mobile drag looks "+(transition?"across NORMAL_RIDE → LOCKDOWN":"after lockdown"));Check(c.UseCount==count,"Drag release never taps "+(transition?"across state change":"after state change"));}
 IEnumerator Start(){
  g=FindFirstObjectByType<SceneManager>();yield return null;
  Check(g.State.Current==RideState.BOARDING,"Begins in boarding state");Check(Quaternion.Angle(g.Ride.boardingDoor.localRotation,Quaternion.identity)>10,"Rear door starts visibly open");Check(g.Ride.BoardingOffset.magnitude>.05f,"Player enters from door toward seated eye position");
  g.Player.yaw=-20;yield return null;Check(Mathf.Abs(g.Player.yaw+20)<1,"Look control remains available during boarding");yield return new WaitForSeconds(2.6f);
  Check(g.State.Current==RideState.NORMAL_RIDE&&g.State.Has("boarded"),"Boarding enters normal ride quickly");Check(g.Ride.BoardingOffset.magnitude<.001f&&Quaternion.Angle(g.Ride.boardingDoor.localRotation,Quaternion.identity)<.1f,"Camera settles and door closes");
  Check(g.Registry.Count==25,"25 uniquely registered controls in upgraded scene");
  foreach(var c in g.Registry.All){g.Player.LookAtControl(c.transform);Physics.SyncTransforms();var s=g.Player.view.WorldToScreenPoint(c.transform.position);Check(g.Interactions.Pick(s)==c,"Ray reaches "+c.id);}
  touch=InputSystem.AddDevice<Touchscreen>();int preference=g.Save.Guidance;
  yield return Tap("guidance-2");Check(new SaveManager().Guidance==2,"Guidance choice persists locally");yield return Tap("guidance-0");g.Save.Guidance=preference;
  yield return Tap("climate");Check(g.Registry.Get("climate").Active&&g.Ride.climateText.text.Contains("19"),"Climate physically updates temperature display");
  Vector3 glass=g.Ride.leftGlass.localPosition;yield return Tap("left-window");yield return new WaitForSeconds(1.6f);Check(g.Ride.leftGlass.localPosition.y<glass.y-.45f,"Normal ride window physically lowers");
  var seat=g.Registry.Get("passenger-seat");Vector3 seatHome=seat.movingPart.localPosition;seat.Use(g);yield return new WaitForSeconds(.5f);Check(Vector3.Distance(seatHome,seat.movingPart.localPosition)>.1f,"Seat motor physically moves passenger seat and proxy");
  var lamp=g.Registry.Get("overhead-light");lamp.Use(g);Check(lamp.cabinLight.enabled,"Reading light works before lockdown");
  Check(!g.Ride.Hostile,"Four explored controls keep normal ride available");
  yield return Drag(true);Check(g.State.Current==RideState.LOCKDOWN,"Lockdown occurs in the same scene");Check(g.Ride.DistanceMiles<=55&&g.Ride.DistanceMiles>54.8f,"Distance begins near 55.0 miles");
  yield return Tap("climate");yield return Drag(false);yield return Tap("left-window");yield return Tap("left-handle");
  Check(g.State.Has("tested-window")&&g.State.Has("tested-door"),"Door/window attempts record live hostile-state flags");yield return new WaitForSeconds(1.6f);Check(Mathf.Abs(g.Ride.leftGlass.localPosition.y-glass.y)<.01f,"Lockdown seals window and refuses switch");
  Check(g.Motion.speed>24&&g.Ride.navigation.text.Contains("UNKNOWN")&&g.Ride.navigation.text.Contains("MI"),"Physical navigation shows unknown destination, miles and rising speed");
  Check(RenderSettings.ambientIntensity>=.9f&&RenderSettings.sun.intensity>=1,"Daytime lighting stays readable during lockdown");Check(g.Ride.accents[0].material.color.r>.6f,"Warning accents visibly turn red");
  g.Ride.Authenticate();Check(!g.Ride.Ready,"Authentication cannot bypass exploration");
  var glove=g.Registry.Get("glovebox");Vector3 latchHome=glove.movingPart.localPosition;glove.Use(g);yield return null;
  Check(g.Ride.glove.State==CompartmentState.LOCKED&&glove.locked,"Glove compartment remains physically locked");Check(Vector3.Distance(latchHome,glove.movingPart.localPosition)>.0001f,"Locked latch physically pulls");Check(g.Dialogue.Text=="That compartment is not available to passengers.","AI reacts to glove-box attempt");
  yield return Tap("passenger-display");Check(g.State.Has("authentication-known"),"Rear display reveals passenger authentication after restrictions and glovebox");
  yield return Tap("passenger-pocket");Check(g.Inventory.Contains("passenger-note"),"Passenger note added to lightweight inventory");yield return Tap("charging");yield return Tap("security-trim");
  Check(g.State.Has("trim-inspected")&&!g.Ride.Ready,"Physical clue trail prepares service link without auto-solving authentication");yield return Tap("authenticate");
  Check(g.Ride.Ready&&g.Puzzles.Definitions["passenger-auth"].Solved,"Authentication prep emits completion and reward flags");Check(g.Puzzles.Definitions.Count==11,"Modular definitions exist for all ten future puzzle hooks");
  Check(g.Ride.glove.State==CompartmentState.LOCKED&&!g.Chess.Active,"Foundation ends with chess prepared, glove box secured, no chess challenge launched");
  var arm=g.Registry.Get("armrest");Quaternion angle=arm.movingPart.localRotation;arm.Use(g);yield return new WaitForSeconds(.5f);Check(Quaternion.Angle(angle,arm.movingPart.localRotation)>70,"Armrest still folds after authentication readiness");arm.Use(g);
  var ds=g.Registry.Get("driver-seat");int n=ds.UseCount;Vector3 driverPos=ds.movingPart.localPosition;yield return Tap("driver-seat");yield return new WaitForSeconds(.3f);Check(Vector3.Distance(driverPos,ds.movingPart.localPosition)<.001f,"Hostile driver seat restriction responds without moving");
  lamp.Use(g);Check(!lamp.cabinLight.enabled,"Light toggles after lockdown");
  g.TogglePause();float distance=g.Ride.DistanceMiles;yield return new WaitForSecondsRealtime(.3f);Check(g.Ride.DistanceMiles==distance,"Pause freezes ride distance");g.TogglePause();
  Check(FindObjectsByType<PlayerCamera>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<SceneManager>(FindObjectsSortMode.None).Length==1,"Single camera input owner and scene controller preserved");
  InputSystem.RemoveDevice(touch);g.Player.Recenter();g.Save.ClearProgress();results.Add("Failures: "+failed);System.IO.Directory.CreateDirectory("docs");System.IO.File.WriteAllLines("docs/FOUNDATION2_"+(Screen.width<Screen.height?"PORTRAIT":"LANDSCAPE")+"_VERIFICATION.txt",results);Debug.Log("FOUNDATION 02 VERIFICATION: "+(failed==0?"PASS":"FAIL")+" ("+failed+" failures)");Destroy(gameObject);
 }
}
}
#endif
