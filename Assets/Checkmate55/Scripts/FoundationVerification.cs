#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using System.Collections;
using System.Collections.Generic;
namespace Checkmate55 {
public sealed class FoundationVerification : MonoBehaviour {
 List<string> results=new List<string>(); int failed;
 void Check(bool success,string text){results.Add((success?"PASS: ":"FAIL: ")+text);if(!success)failed++;}
 IEnumerator Start(){
  var g=FindFirstObjectByType<SceneManager>();yield return null;
  Check(g.Registry.Count==19,"19 uniquely registered physical controls");
  foreach(var c in g.Registry.All){g.Player.LookAtControl(c.transform);Physics.SyncTransforms();var s=g.Player.view.WorldToScreenPoint(c.transform.position);Check(g.Interactions.Pick(s)==c,"Ray hits "+c.id);}
  var seat=g.Registry.Get("passenger-seat");var start=seat.movingPart.localPosition;seat.Use(g);yield return new WaitForSeconds(.5f);Check(Vector3.Distance(seat.movingPart.localPosition,start)>.1f,"Passenger seat physically slides");Check(seat.transform.IsChildOf(seat.movingPart),"Seat proxy follows the moving seat");seat.Use(g);
  var arm=g.Registry.Get("armrest");var rotation=arm.movingPart.localRotation;arm.Use(g);yield return new WaitForSeconds(.5f);Check(Quaternion.Angle(rotation,arm.movingPart.localRotation)>70,"Armrest physically folds");arm.Use(g);
  var lamp=g.Registry.Get("overhead-light");lamp.Use(g);Check(lamp.cabinLight.enabled,"Reading light toggles on");lamp.Use(g);Check(!lamp.cabinLight.enabled,"Reading light toggles off");
  var glove=g.Registry.Get("glovebox");glove.Use(g);Check(glove.locked&&!g.State.Has("used.glovebox"),"Glove box remains locked without unlocking progression");
  float z=g.Motion.roadSegments[4].position.z;yield return new WaitForSeconds(.2f);Check(Mathf.Abs(g.Motion.roadSegments[4].position.z-z)>1,"Road pool moves in world space");
  var climate=g.Registry.Get("climate");g.Player.LookAtControl(climate.transform);yield return null;yield return null;
  var ts=InputSystem.AddDevice<Touchscreen>();Vector2 pos=new Vector2(Screen.width/2f,Screen.height/2f);int count=climate.UseCount;
  InputSystem.QueueStateEvent(ts,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=pos});yield return null;yield return null;
  InputSystem.QueueStateEvent(ts,new TouchState{touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=pos+new Vector2(3,2)});yield return null;yield return null;
  Check(climate.UseCount==count+1,"Mobile tap with small finger motion interacts exactly once");
  g.Player.LookAtControl(climate.transform);yield return null;count=climate.UseCount;float yaw=g.Player.yaw;
  InputSystem.QueueStateEvent(ts,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=pos});yield return null;yield return null;
  InputSystem.QueueStateEvent(ts,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=pos+new Vector2(90,0)});yield return null;yield return null;
  InputSystem.QueueStateEvent(ts,new TouchState{touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=pos+new Vector2(90,0)});yield return null;yield return null;
  Check(Mathf.Abs(g.Player.yaw-yaw)>5,"Mobile drag rotates seated view");Check(climate.UseCount==count,"Drag release does not trigger a tap");InputSystem.RemoveDevice(ts);
  var mouse=InputSystem.AddDevice<Mouse>();g.Player.LookAtControl(climate.transform);yield return null;count=climate.UseCount;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});yield return null;yield return null;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=0});yield return null;yield return null;
  Check(climate.UseCount==count+1,"Desktop mouse click interacts exactly once");
  g.Player.LookAtControl(climate.transform);yield return null;count=climate.UseCount;yaw=g.Player.yaw;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos,buttons=1});yield return null;yield return null;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+new Vector2(90,0),buttons=1});yield return null;yield return null;
  InputSystem.QueueStateEvent(mouse,new MouseState{position=pos+new Vector2(90,0),buttons=0});yield return null;yield return null;
  Check(Mathf.Abs(g.Player.yaw-yaw)>5,"Desktop mouse drag rotates view");Check(climate.UseCount==count,"Mouse drag release does not interact");InputSystem.RemoveDevice(mouse);
  Check(FindFirstObjectByType<PlayerBody>().GetComponentsInChildren<Collider>().Length==0,"Player body cannot block rays");
  Check(FindObjectsByType<SceneManager>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<PlayerCamera>(FindObjectsSortMode.None).Length==1,"Single scene controller and camera input owner");
  foreach(int profile in new[]{0,1,2}){g.Performance.Apply(profile);int active=0;foreach(var t in g.Motion.traffic)if(t.gameObject.activeSelf)active++;Check(active==(profile==0?4:profile==1?7:10),"Traffic budget for quality "+profile);}
  g.Performance.Apply(g.Save.Quality);g.Player.Recenter();g.Save.ClearProgress();
  results.Add("Failures: "+failed);System.IO.File.WriteAllLines("docs/VERIFICATION.txt",results);Debug.Log("FOUNDATION VERIFICATION: "+(failed==0?"PASS":"FAIL")+" ("+failed+" failures)");Destroy(gameObject);
 }
}
}
#endif
