#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Checkmate55 {
public sealed class FoundationThreeVerification : MonoBehaviour {
 List<string> log=new List<string>();SceneManager g;Touchscreen touch;int finger=100,fail;
 void Check(bool pass,string msg){log.Add((pass?"PASS: ":"FAIL: ")+msg);if(!pass){fail++;Debug.LogWarning("VERIFICATION: "+msg);}}
 IEnumerator Frames(){yield return null;yield return null;}
 void Event(UnityEngine.InputSystem.TouchPhase phase,Vector2 p){InputSystem.QueueStateEvent(touch,new TouchState{touchId=finger,phase=phase,position=p});}
 IEnumerator Tap(string id,bool aim=true){var c=g.Registry.Get(id);if(aim)g.Player.LookAtControl(c.transform);yield return Frames();Physics.SyncTransforms();var p=(Vector2)g.Player.view.WorldToScreenPoint(c.transform.position);int n=c.UseCount;Check(g.Interactions.Pick(p)==c,"Ray target "+id);finger++;Event(UnityEngine.InputSystem.TouchPhase.Began,p);yield return Frames();Event(UnityEngine.InputSystem.TouchPhase.Ended,p+Vector2.one);yield return Frames();Check(c.UseCount==n+1,"Touch selects "+id+" exactly once");}
 IEnumerator Drag(bool lockdown){var c=g.Registry.Get("climate");g.Player.LookAtControl(c.transform);yield return Frames();Vector2 p=g.Player.view.WorldToScreenPoint(c.transform.position);float y=g.Player.yaw;int n=c.UseCount;finger++;Event(UnityEngine.InputSystem.TouchPhase.Began,p);yield return Frames();if(lockdown)g.Ride.TriggerLockdown();Event(UnityEngine.InputSystem.TouchPhase.Moved,p+new Vector2(95,0));yield return Frames();Event(UnityEngine.InputSystem.TouchPhase.Ended,p+new Vector2(95,0));yield return Frames();Check(Mathf.Abs(g.Player.yaw-y)>5,"Touch drag changes view across state change");Check(n==c.UseCount,"Drag does not trigger a tap");}
 IEnumerator IdleChess(){float until=Time.time+12;while(g.Chess.Busy&&Time.time<until)yield return null;Check(!g.Chess.Busy,"Chess animation finishes");}
 IEnumerator Start(){g=FindFirstObjectByType<SceneManager>();yield return Frames();touch=InputSystem.AddDevice<Touchscreen>();g.Save.Hints=true;g.Save.Guidance=0;
  Check(g.State.Current==RideState.BOARDING,"Original boarding sequence active");Check(!g.Chess.Active&&!g.Chess.Board.tray.gameObject.activeSelf,"Playable board hidden during exploration");yield return new WaitForSeconds(2.5f);
  Check(g.State.Current==RideState.NORMAL_RIDE,"Normal daytime ride begins");Check(g.Player.transform.localPosition.y>1.4f,"Seated eye height retained");
  yield return Tap("climate");Check(g.Registry.Get("climate").Active,"Climate works");float glassY=g.Ride.leftGlass.localPosition.y;yield return Tap("left-window");yield return new WaitForSeconds(1.6f);Check(g.Ride.leftGlass.localPosition.y<glassY-.4f,"Window lowers");
  g.Seats.Choose(0);yield return new WaitForSeconds(1.7f);Check(g.Seats.Offset.x<-.59f&&FindFirstObjectByType<PlayerBody>().SeatOffset.x<-.59f,"Seat switch moves camera and body together");g.Seats.Choose(1);yield return new WaitForSeconds(1.7f);
  yield return Tap("guidance-2");Check(g.Save.Guidance==2,"Physical guidance selection persists");yield return Tap("guidance-0");
  yield return Drag(true);Check(g.Ride.Hostile,"Lockdown triggered without replacing cabin");Check(g.Ride.navigation.text.Contains("UNKNOWN")&&g.Ride.navigation.text.Contains("MI"),"Speed/distance remain on physical screen");
  yield return Tap("left-handle");yield return Tap("left-window");yield return Tap("glovebox");Check(g.Tutorial.TargetId=="passenger-display","Guide follows live exploration flags");
  yield return Tap("passenger-display");yield return Tap("passenger-pocket");yield return Tap("charging");yield return Tap("security-trim");yield return Tap("authenticate");Check(g.Ride.Ready&&!g.Chess.Active,"Preparation requires exploration before chess");yield return Tap("authenticate");yield return IdleChess();yield return new WaitForSeconds(1.5f);
  Check(g.State.Current==RideState.CHESS_CHALLENGE&&g.Chess.Active,"Chess challenge deployed");Check(Vector3.Distance(g.Chess.Board.tray.localPosition,new Vector3(0,.81f,-.67f))<.01f,"Tray rests over lap at authored position");int seat=g.Seats.Seat;g.Seats.Cycle();Check(g.Seats.Seat==seat,"Seat change safely waits for tray retraction");
  yield return Tap("chess-h2",false);Check(g.Chess.Selected=="h2","Tap on square selects white pawn");Check(g.Chess.Board.Squares["h3"].sharedMaterial==g.Chess.Board.legal,"Legal destination visibly highlighted");
  ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("docs/CHESS_"+(Screen.width<Screen.height?"PORTRAIT":"LANDSCAPE")+".png"));yield return Frames();
  yield return Tap("chess-h3",false);yield return IdleChess();Check(g.Chess.PlayerMoves==1&&g.Chess.Snapshot.turn=="w","Player moves and vehicle replies through chess.js");
  yield return Tap("chess-a1",false);yield return Tap("chess-e1",false);yield return IdleChess();
  Check(g.State.Has("CHESS_CHALLENGE_COMPLETED")&&g.State.Has("glovebox-authenticated"),"Generic event routed to authentication flag");Check(!g.Chess.Active&&!g.Chess.Board.tray.gameObject.activeSelf,"Board retracts on success");Check(g.State.Current==RideState.EXPLORATION,"Returns to car exploration");Check(g.Ride.glove.State==CompartmentState.LOCKED,"Future glove box content not prematurely implemented");
  Check(new SaveManager().TutorialCompleted,"Tutorial completion persists");var saved=new GameState();new SaveManager().LoadFlags(saved);Check(saved.Has("CHESS_CHALLENGE_COMPLETED"),"Challenge completion restores from save");yield return new WaitForSeconds(2);yield return Tap("overhead-light");Check(g.Registry.Get("overhead-light").cabinLight.enabled,"Physical interactions continue after chess");
  g.Chess.Begin();yield return IdleChess();yield return new WaitForSeconds(1.5f);foreach(var pair in new[]{new[]{"h2","h3"},new[]{"h3","h4"},new[]{"h4","h5"}}){yield return Tap("chess-"+pair[0],false);yield return Tap("chess-"+pair[1],false);yield return IdleChess();}Check(g.Chess.RetryNeeded,"Move budget failure offers a retry without trapping the player");g.Chess.Retry();Check(g.Chess.PlayerMoves==0&&!g.Chess.RetryNeeded,"Retry restores the authored FEN and move budget");g.Chess.Cancel();yield return IdleChess();Check(!g.Chess.Active,"Tray can be retracted voluntarily after retry");yield return new WaitForSeconds(1.5f);
  g.Seats.Choose(2);yield return new WaitForSeconds(1.7f);Check(g.Seats.Offset.x>.59f,"Right rear seat reachable after chess");g.Seats.Choose(3);yield return new WaitForSeconds(4);Check(g.Seats.Offset.z>1.65f&&g.Player.transform.localPosition.z>.6f,"Front passenger seat reached through aisle");yield return Tap("glovebox");Check(g.Ride.glove.State==CompartmentState.LOCKED,"Glove box interaction works from front seat");g.Seats.Choose(1);yield return new WaitForSeconds(4);
  g.Save.Hints=false;Check(g.Tutorial.Prompt=="","Guidance can be switched off");g.Save.Hints=true;InputSystem.RemoveDevice(touch);g.Player.Recenter();g.Save.ClearProgress();log.Add("Failures: "+fail);System.IO.Directory.CreateDirectory("docs");System.IO.File.WriteAllLines("docs/FOUNDATION3_"+(Screen.width<Screen.height?"PORTRAIT":"LANDSCAPE")+"_VERIFICATION.txt",log);Debug.Log("FOUNDATION 03 VERIFICATION "+(fail==0?"PASS":"FAIL")+" / "+fail+" failures");Destroy(this);
 }
}
}
#endif
