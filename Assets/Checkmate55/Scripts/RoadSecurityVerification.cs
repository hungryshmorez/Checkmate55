#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Checkmate55 {
public sealed class RoadSecurityVerification : MonoBehaviour {
 List<string> log=new List<string>();SceneManager g;Touchscreen touch;int finger=500,fail;
 void Check(bool pass,string msg){log.Add((pass?"PASS: ":"FAIL: ")+msg);if(!pass){fail++;Debug.LogWarning("ROAD VERIFY: "+msg);}System.IO.File.WriteAllLines("docs/ROAD_SECURITY_PROGRESS.txt",log);}
 void OnDestroy(){if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);}
 IEnumerator Frames(){yield return null;yield return null;}
 void Event(UnityEngine.InputSystem.TouchPhase phase,Vector2 p){InputSystem.QueueStateEvent(touch,new TouchState{touchId=finger,phase=phase,position=p});}
 IEnumerator Tap(string id,bool aim=true){var c=g.Registry.Get(id);if(!c){Check(false,"Missing target "+id);yield break;}if(aim){g.Player.FocusControl(c.transform);yield return new WaitForSeconds(1.8f);}yield return Frames();Physics.SyncTransforms();var p=(Vector2)g.Player.view.WorldToScreenPoint(c.transform.position);int n=c.UseCount;Check(g.Interactions.Pick(p)==c,"Ray hits "+id);finger++;Event(UnityEngine.InputSystem.TouchPhase.Began,p);yield return Frames();Event(UnityEngine.InputSystem.TouchPhase.Ended,p+Vector2.one);yield return Frames();Check(c.UseCount==n+1,"Touch uses "+id+" once");}
 IEnumerator Idle(){float until=Time.time+18;while(g.Chess.Busy&&Time.time<until)yield return null;Check(!g.Chess.Busy,"Tray/turn animation completes");}
 IEnumerator AimBoard(){g.Player.FocusAngles(0,61);yield return new WaitForSeconds(2);}
 IEnumerator Move(string from,string to){yield return Tap("chess-"+from,false);yield return Tap("chess-"+to,false);yield return Idle();}
 IEnumerator Start(){System.IO.Directory.CreateDirectory("docs");g=FindFirstObjectByType<SceneManager>();yield return Frames();touch=InputSystem.AddDevice<Touchscreen>();g.Save.Hints=true;g.Save.Guidance=0;yield return new WaitForSeconds(2.6f);
  Check(g.State.Current==RideState.NORMAL_RIDE,"Boarding settles into original normal ride");yield return Tap("climate");Check(g.Registry.Get("climate").Active,"Climate retained");yield return Tap("left-window");yield return new WaitForSeconds(1.6f);
  var c=g.Registry.Get("climate");g.Player.FocusControl(c.transform);yield return new WaitForSeconds(2);Vector2 p=g.Player.view.WorldToScreenPoint(c.transform.position);float yaw=g.Player.yaw;int count=c.UseCount;finger++;Event(UnityEngine.InputSystem.TouchPhase.Began,p);yield return Frames();g.Ride.TriggerLockdown();Event(UnityEngine.InputSystem.TouchPhase.Moved,p+new Vector2(95,0));yield return Frames();Event(UnityEngine.InputSystem.TouchPhase.Ended,p+new Vector2(95,0));yield return Frames();Check(Mathf.Abs(g.Player.yaw-yaw)>5&&count==c.UseCount,"Touch drag survives lockdown without accidental tap");
  foreach(string id in new[]{"left-handle","left-window","glovebox","passenger-display","passenger-pocket","charging","security-trim","authenticate","authenticate"})yield return Tap(id);
  yield return Idle();yield return AimBoard();Check(g.Chess.Active,"Original authentication deploys in lap");yield return Move("h2","h3");yield return Move("a1","e1");Check(g.Ride.glove.State==CompartmentState.OPEN,"Authentication audibly unlocks and opens physical glove box");Check(g.Motion.speed*2.23694f<70,"Checking king brakes the vehicle");
  yield return Tap("glovebox");yield return Tap("charging");yield return Idle();yield return AimBoard();Check(g.Chess.challenge==g.Security.diagnostics,"USB starts diagnostic challenge");
  g.Player.FocusAngles(100,0);yield return new WaitForSeconds(4);Check(g.Security.Corrupted&&g.Chess.Snapshot.pieces.Any(v=>v.type=="q"&&v.square=="b7"),"Car edits queen only while player looks away");yield return Tap("right-speaker");Check(!g.Security.Corrupted&&g.Chess.Snapshot.pieces.Any(v=>v.type=="q"&&v.square=="a7"),"Passenger recording restores checksum");yield return AimBoard();yield return Move("a1","a7");Check(g.State.Has("security.diagnostics"),"Capturing queen releases diagnostics");
  yield return Tap("left-window");yield return Idle();yield return AimBoard();yield return Move("g1","h2");Check(g.Security.WindowReleased,"Defensive move releases timed window");yield return new WaitForSeconds(1.5f);float glass=g.Ride.leftGlass.localPosition.y;Check(g.Security.WindowSeconds>0&&g.Security.WindowSeconds<15,"Window permission is a live 15-second lease");
  yield return Tap("lost-knight");Check(g.State.Has("item.knight"),"Physical knight recovered");yield return Tap("seat-scratch");Check(g.State.Has("rule.jump"),"Under-seat clue unlocks hidden rule");yield return Tap("right-speaker");
  yield return Tap("authenticate");yield return Idle();yield return AimBoard();yield return Move("b2","a2");Check(g.State.Has("security.diversion")&&g.Motion.lane==0,"Rook sacrifice forces left lane");yield return new WaitForSeconds(2);Check(!g.Security.WindowReleased&&g.Ride.leftGlass.localPosition.y>glass+.3f,"Window re-locks and glass closes after expiry");
  yield return Tap("authenticate");yield return Idle();yield return AimBoard();Check(g.State.Current==RideState.ROAD_CHESS,"Route challenge uses Road Chess state");float until=Time.time+20;while(g.Motion.speed*2.23694f<72&&Time.time<until)yield return null;
  foreach(var id in new[]{"security-lane","security-speed"}){var pixel=g.Player.view.WorldToScreenPoint(g.Registry.Get(id).transform.position);Check(pixel.x>0&&pixel.x<Screen.width&&pixel.y>105*Screen.height/800f&&pixel.y<Screen.height-140*Screen.height/800f&&!g.UI.OverButton(pixel),"Tray control visible above guidance: "+id);}
  yield return Tap("security-lane",false);Check(g.Motion.lane==1,"Tray command changes actual lane");yield return Tap("security-lane",false);yield return Tap("security-lane",false);yield return Tap("security-speed",false);Check(g.Security.CruiseMph==115,"Tray command requests Overclock speed");yield return Tap("security-speed",false);Check(g.Security.CruiseMph==75,"Tray can return to safe Road Jump speed");
  yield return Tap("chess-c2",false);Check(g.Chess.Snapshot.moves.Any(m=>m.from=="c2"&&m.to=="f3"&&m.flags=="road"),"Discovered Road Jump enabled by actual vehicle speed");
  ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("docs/ROAD_BOARD_"+(Screen.width<Screen.height?"PORTRAIT":"LANDSCAPE")+".png"));yield return Frames();
  yield return Tap("chess-f3",false);yield return Idle();Check(g.State.Has("security.route")&&g.State.Has("rule.used-hidden"),"Hidden move accepted and navigation override granted");Check(!g.Chess.Active&&g.Ride.navigation.text.Contains("SERVICE LAY-BY"),"Tray retracts; physical navigation shows diverted route");
  var loaded=new GameState();g.Save.LoadFlags(loaded);Check(loaded.Has("security.route")&&loaded.Has("item.knight")&&loaded.Has("rule.jump"),"Progress, recovered piece and discoveries persist");
  // A second solution uses ordinary chess and remains possible below road-power speeds.
  g.Motion.lane=0;g.Security.StartChallenge(g.Security.route);yield return Idle();yield return AimBoard();yield return Move("a3","a4");Check(!g.Chess.Active,"Ordinary rook check also solves navigation");
  g.Security.StartChallenge(g.Security.route);yield return Idle();yield return AimBoard();g.Chess.Cancel();yield return Idle();Check(!g.Chess.Active,"Voluntary retraction preserves exploration");
  g.Security.StartChallenge(g.Security.window);yield return Idle();yield return AimBoard();yield return Move("a2","g2");Check(g.Chess.RetryNeeded,"Unsafe defense is denied and offers retry");g.Chess.Retry();Check(!g.Chess.RetryNeeded&&g.Chess.PlayerMoves==0&&g.Chess.Snapshot.fen==g.Security.window.FEN,"Retry restores authored position and budget");g.Chess.Cancel();yield return Idle();
  yield return Tap("overhead-light");Check(g.Registry.Get("overhead-light").cabinLight.enabled,"Cabin gadgets still work after Road Chess");g.Seats.Choose(2);yield return new WaitForSeconds(2);Check(g.Seats.Offset.x>.59f,"Seat switching retained");
  g.Save.Hints=false;Check(g.Tutorial.Prompt=="","Guidance still switches off");g.Save.Hints=true;InputSystem.RemoveDevice(touch);g.Player.Recenter();log.Add("Failures: "+fail);System.IO.File.WriteAllLines("docs/ROAD_SECURITY_"+(Screen.width<Screen.height?"PORTRAIT":"LANDSCAPE")+"_VERIFICATION.txt",log);Debug.Log("ROAD SECURITY VERIFICATION: "+fail+" failures");Destroy(this);
 }
}
}
#endif
