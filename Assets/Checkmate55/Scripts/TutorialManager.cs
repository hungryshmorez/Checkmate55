using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
public sealed class TutorialManager {
 public bool Looked,Touched;SceneManager game;
 public void Initialize(SceneManager owner){game=owner;}
 // Override owns the guide once a mode has started: calibration lessons, chess-only modes and the post-lockdown story graph.
 OverrideDirector Ov=>game!=null&&game.Override!=null&&!game.Override.WaitingForMode?game.Override:null;
 bool OverrideGuides(OverrideDirector o)=>o.Mode.Mode==GameMode.Calibration||o.Mode.ChessOnly||(o.Mode.Hostile&&game.Ride.Hostile&&game.State.Has("glovebox-authenticated"));
 public string TargetId {get{if(game==null||game.Chess.Active)return null;var s=game.State;
  var ov=Ov;if(ov!=null&&OverrideGuides(ov)){var t=ov.Modes.TargetOverride;return t!=null||!ov.Mode.Hostile?t:ov.Story.Target(ov.Has);}
  if(!game.Ride.Hostile)return !Touched?"climate":"request-stop";
  if(s.Has("glovebox-authenticated"))return game.Security.Target;
  if(!s.Has("tested-door"))return "left-handle";
  if(!s.Has("tested-window"))return "left-window";
  if(!s.Has("glovebox-interest"))return "glovebox";
  if(!s.Has("authentication-known"))return "passenger-display";
  if(!s.Has("clue.note"))return "passenger-pocket";
  if(!s.Has("clue.service"))return "charging";
  if(!s.Has("trim-inspected"))return "security-trim";
  if(!s.Has("CHESS_CHALLENGE_COMPLETED"))return "authenticate";
  return null;
 }}
 public string Objective {get{if(game==null)return "";if(game.Chess.Active)return game.Chess.Instruction;
  var ov=Ov;if(ov!=null&&OverrideGuides(ov)){var po=ov.Modes.PromptOverride;if(po!=null)return po;if(ov.Mode.Hostile){var b=ov.Story.Current(ov.Has);return b!=null?b.Objective:"";}return "";}
  if(!game.Ride.Hostile)return "Explore the car. When ready, REQUEST STOP on the rear display.";
  if(game.State.Has("glovebox-authenticated"))return game.Security.Objective;
  switch(TargetId){case "left-handle":return "The route has changed. Try a door handle.";case "left-window":return "The door is secured. Test the window switch.";case "glovebox":return "Find a way into restricted storage. Inspect the glove box latch.";case "passenger-display":return "The glove box needs authorization. Read the rear display’s storage policy.";case "passenger-pocket":return "Find the service instructions in the front passenger seat pocket.";case "charging":return "Follow the note: inspect the USB charging ports between the front seats.";case "security-trim":return "Follow the service label: lift the trim beside the rear cupholders.";case "authenticate":return game.Ride.Ready?"Tap AUTHENTICATE on the rear display to deploy the chess security tray.":"Return to the rear display. Tap authentication to prepare the link.";default:return "Authentication complete · Explore the cabin, switch seats, or replay the security test.";}
 }}
 public string Prompt {get{if(game==null)return "";if(game.Chess.Active)return game.Save.Hints?Objective:game.Chess.challenge.guidanceText;var ov=Ov;if(ov!=null&&ov.Mode.Mode==GameMode.Calibration){var po=ov.Modes.PromptOverride;if(!string.IsNullOrEmpty(po))return po;}if(!game.Save.Hints)return "";if(!game.Save.TutorialCompleted){if(!Looked)return "DRAG TO LOOK · You are a passenger. Investigate the car to find a way out.";if(!Touched)return "TAP OBJECTS TO INTERACT · Try the glowing climate control. HINT points the way.";}return Objective;}}
 public void Hint(){game.Save.Hints=true;if(game.Chess.Active){game.Chess.Hint();return;}var c=game.Registry.Get(TargetId??"");if(c)game.Player.FocusControl(c.transform);var ov=Ov;var beat=ov!=null&&ov.Mode.Hostile?ov.Story.Current(ov.Has):null;game.Dialogue.Say(beat!=null&&!string.IsNullOrEmpty(beat.Hint)?Objective+"  "+beat.Hint:Objective);}
}
}
