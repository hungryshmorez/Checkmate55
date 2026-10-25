#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Checkmate55 {
public sealed class FoundationTwoDesktopVerification:MonoBehaviour {
 List<string> log=new List<string>();int failed;
 void Check(bool ok,string message){log.Add((ok?"PASS: ":"FAIL: ")+message);if(!ok)failed++;}
 IEnumerator Frames(){yield return null;yield return null;}
 IEnumerator Start(){DontDestroyOnLoad(gameObject);var g=FindAnyObjectByType<SceneManager>();yield return new WaitUntil(()=>g.State.Current==RideState.NORMAL_RIDE);var mouse=InputSystem.AddDevice<Mouse>();var keyboard=InputSystem.AddDevice<Keyboard>();int preference=g.Save.Guidance;g.Save.Guidance=1;
  var stop=g.Registry.Get("request-stop");g.Player.LookAtControl(stop.transform);yield return Frames();Vector2 pos=g.Player.view.WorldToScreenPoint(stop.transform.position);
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});yield return Frames();InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+new Vector2(2,1),buttons=0});yield return Frames();
  Check(stop.UseCount==1&&g.Ride.Hostile,"Desktop mouse can trigger lockdown through physical REQUEST STOP");
  var climate=g.Registry.Get("climate");g.Player.LookAtControl(climate.transform);yield return Frames();pos=g.Player.view.WorldToScreenPoint(climate.transform.position);int count=climate.UseCount;float yaw=g.Player.yaw;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});yield return Frames();InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+new Vector2(95,0),buttons=1});yield return Frames();InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+new Vector2(95,0),buttons=0});yield return Frames();
  Check(Mathf.Abs(g.Player.yaw-yaw)>5&&climate.UseCount==count,"Desktop mouse drag looks without tapping after lockdown");
  g.Player.LookAtControl(climate.transform);yield return Frames();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));yield return Frames();InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();Check(climate.UseCount==count+1,"Keyboard Space interacts after lockdown");
  yaw=g.Player.yaw;InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.D));yield return new WaitForSeconds(.25f);InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Frames();Check(g.Player.yaw>yaw+3,"Keyboard look remains functional");
  g.Restart();yield return null;yield return new WaitUntil(()=>FindAnyObjectByType<SceneManager>()!=null);g=FindAnyObjectByType<SceneManager>();yield return new WaitUntil(()=>g.State.Current==RideState.NORMAL_RIDE);
  Check(!g.Ride.Hostile&&!g.State.Has("authentication-ready")&&g.Inventory.Items.Count==0,"Restart returns to fresh normal ride and empty inventory");Check(g.Save.Guidance==1,"Guidance survives scene restart");g.Save.Guidance=preference;
  foreach(var id in new[]{"climate","left-window","overhead-light","passenger-seat"})g.Registry.Get(id).Use(g);yield return Frames();Check(g.State.Current==RideState.NORMAL_RIDE,"Four distinct gadgets permit normal exploration");g.Registry.Get("passenger-pocket").Use(g);yield return Frames();Check(g.Ride.Hostile,"Fifth distinct gadget triggers event-driven lockdown");
  InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);g.Save.ClearProgress();g.Player.Recenter();log.Add("Failures: "+failed);System.IO.File.WriteAllLines("docs/FOUNDATION2_DESKTOP_VERIFICATION.txt",log);Debug.Log("FOUNDATION 02 DESKTOP: "+(failed==0?"PASS":"FAIL"));Destroy(gameObject);
 }
}
}
#endif
