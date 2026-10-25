using UnityEngine;
namespace Checkmate55 {
// Physical rewards and exploration live here, independently of chess presentation.
public sealed class VehicleSecurity : MonoBehaviour {
 public ChessChallenge diagnostics,window,diversion,route;
 public Transform storageClue,lostKnight;
 public TextMesh rulePlaque;
 SceneManager game;float brakeUntil,windowUntil,lookAway;bool corrupted,windowWasOpen;
 public float CruiseMph {get;private set;}=75;
 public bool WindowReleased=>Time.time<windowUntil;
 public float WindowSeconds=>Mathf.Max(0,windowUntil-Time.time);
 public bool BrakeActive=>Time.time<brakeUntil;
 public void SetCruise(float mph){CruiseMph=mph;}
 public float TargetSpeed{get{float basic=(game.State.Has("security.route")&&!game.Chess.Active?42:Time.time<brakeUntil?48:CruiseMph);return (game.Override!=null&&!game.Override.WaitingForMode?game.Override.TargetMph(basic):basic)/2.23694f;}}
 // OVERRIDE: the car can end a window lease early; winning the contest grants a fresh one.
 public void EndWindowLease(){windowUntil=0;}
 public void GrantWindowLease(float seconds){windowUntil=Time.time+seconds;windowWasOpen=true;game.Audio.ControlSound("left-window",ControlAction.Switch,false);}
 public string Result {get;private set;}="";
 public bool Corrupted=>corrupted;
 public void Initialize(SceneManager owner){game=owner;game.Chess.Completed+=Reward;game.Chess.RoadRules.ChessMoveConsequence+=Consequence;
  if(game.State.Has("glovebox-authenticated")){game.Ride.glove.Unlock();game.Ride.glove.Pull();}
  if(game.State.Has("security.diversion")){CruiseMph=95;game.Motion.lane=0;}
  game.Inventory.Acquire(game.State.Has("item.knight")?"route-knight":"passenger-ticket");
 }
 void OnDestroy(){if(!game)return;game.Chess.Completed-=Reward;game.Chess.RoadRules.ChessMoveConsequence-=Consequence;}
 void Update(){if(!game||game.Paused)return;
  if(storageClue)storageClue.gameObject.SetActive(game.Ride.glove.State==CompartmentState.OPEN);
  if(lostKnight)lostKnight.gameObject.SetActive(game.State.Has("security.window")&&!game.State.Has("item.knight"));
  if(windowWasOpen&&!WindowReleased){windowWasOpen=false;game.Audio.LockClick();game.Dialogue.Say("Window permission expired. The seal closes. Authenticate at the switch to release it again.");}
  if(rulePlaque)rulePlaque.text=game.State.Has("rule.jump")?"PASSENGER 04 / DO NOT TRUST IT\n70+ : N = 3 × 1\n90+ : R crosses ONE friendly piece\nA–C LEFT / D–E CENTER / F–H RIGHT":"PASSENGER 04\nTHE BOARD IS THE CONTROL SYSTEM\nFIND THE KNIGHT IN THE SEAT POCKET";
  if(game.Chess.Active&&!game.Chess.Busy&&game.Chess.challenge==diagnostics&&!corrupted&&!game.State.Has("security.corruption-seen")){
   Vector3 toward=game.Chess.Board.tray.position-game.Player.view.transform.position;
   if(Vector3.Angle(game.Player.view.transform.forward,toward)>65)lookAway+=Time.deltaTime;else lookAway=0;
   if(lookAway>1.6f){corrupted=true;game.State.Flag("security.corruption-seen");game.Chess.ReplacePosition("4k3/1q6/8/8/8/8/6KP/R7 w - - 0 1");game.Dialogue.Say("I have made no changes. Continue your authentication.");game.Chess.InstructionOverride("POSITION CHANGED: queen a7 → b7. The right speaker holds a recorded checksum.");}
  }
 }
 public void Brake(){brakeUntil=Time.time+9;game.Audio.Chime();}
 static bool HiddenKind(string kind)=>kind=="roadJump"||kind=="laneCharge"||kind=="ghostPawn"||kind=="kingReversal";
 void Consequence(ChessMove move){if(move==null)return;var challenge=game.Chess.challenge;if(move.color=="w"){
   if(challenge.checkBrakes&&game.Chess.Snapshot.check){Brake();game.Chess.InstructionOverride("CHECK → BRAKING. The vehicle yields speed.");}
   if(challenge.road&&challenge.laneFollowsMove){game.Motion.lane=RoadChessRules.Lane(move.to);game.Chess.RoadRules.OnVehicleEvent("lane-change");}
   if(HiddenKind(move.kind)&&!game.AllRulesKnown){game.State.Flag("rule.used-hidden");game.Dialogue.Say("That move is illegal. … Where did you learn that command?");game.Audio.LockClick();}
  }else if(move.captured=="r"&&game.Chess.challenge==diversion){game.Motion.lane=0;CruiseMph=95;Brake();game.Dialogue.Say("Material surrendered. Lane priority reassigned. Do not repeat that command.");}
 }
 void Reward(string id){switch(id){
  case "CHESS_CHALLENGE_COMPLETED":game.Ride.glove.Unlock();game.Ride.glove.Pull();game.Audio.Latch();Result="The glove box latch releases. The compartment opens — inspect the passenger's route card.";break;
  case "security.diagnostics":game.State.Flag(id);game.Audio.Latch();game.Registry.Get("driver-seat").locked=false;Result="Diagnostics connected. Seat controls released. A–C / D–E / F–H map to road lanes. Try a window switch.";break;
  case "security.window":game.State.Flag(id);windowUntil=Time.time+15;windowWasOpen=true;game.Audio.ControlSound("left-window",ControlAction.Switch,false);game.Audio.LockClick();Result="Window override: 15 seconds. The glass lowers. Air catches a loose knight in the seat pocket.";break;
  case "security.diversion":game.State.Flag(id);CruiseMph=95;game.Motion.lane=0;game.Audio.Chime();Result="Rook surrendered. Braking and left-lane diversion accepted. Route security is now exposed on the rear display.";break;
  case "security.route":game.State.Flag(id);game.Motion.lane=2;game.Audio.Latch();game.Audio.Chime();Result="Navigation latch released. SERVICE LAY-BY selected. The car slows to 42 MPH. You have taken control of its route.";break;
 }
 game.Dialogue.Say(Result);
 }
 public ChessChallenge NextChallenge {
  get {if(!game.State.Has("CHESS_CHALLENGE_COMPLETED"))return null;if(!game.State.Has("security.diagnostics"))return diagnostics;if(!game.State.Has("security.window"))return window;if(!game.State.Has("security.diversion"))return diversion;return route;}
 }
 public bool CanBegin(ChessChallenge c){
  if(c==diagnostics&&!game.State.Has("clue.route-card")){game.Dialogue.Say("The glove box is open. Inspect its route card before accessing diagnostics.");return false;}
  if((c==diversion||c==route)&&!game.State.Has("item.knight")){game.Dialogue.Say("Route module incomplete: one white knight is missing. Search the passenger seat pocket.");return false;}
  return true;
 }
 public bool Use(PhysicalControl c){
  if(c.id=="route-card"){game.State.Flag("clue.route-card");game.Dialogue.Say("Passenger 04: 'It thinks in chess. Check = brake. Surrender a rook = change lanes. USB opens diagnostics. The window uses a defensive test.'");return true;}
  if(c.id=="lost-knight"){game.State.Flag("item.knight");game.Inventory.Acquire("route-knight");game.Audio.ChessSettle();game.Dialogue.Say("A heavy white knight. Its base says: 'Look beneath the rear seat. Listen to the right speaker.' The route module is whole again.");return true;}
  if(c.id=="seat-scratch"){game.State.Flag("rule.jump");game.Dialogue.Say("Scratched beneath the seat: 'At 70+, the knight can jump THREE across, ONE up. c2 → f3. The car lies about legality.'");return true;}
  if(c.id=="right-speaker"&&game.State.Has("glovebox-authenticated")){
   game.State.Flag("rule.charge");if(corrupted&&game.Chess.Active&&game.Chess.challenge==diagnostics){corrupted=false;game.Chess.RestoreIntegrity();}
   game.Dialogue.Say("Passenger recording: 'At 90+, a rook can cross one friendly piece into another lane. At 110+, only the pulsing lane accepts hidden moves. If it edits the board, this recording restores the checksum.'");return true;
  }
  if(c.id=="security-speed"||c.id=="security-lane"){
   if(!game.State.Has("security.diagnostics")){game.Dialogue.Say("Diagnostics access required.");return true;}
   if(game.Chess.Busy){game.Dialogue.Say("Wait for the magnetic pieces to settle.");return true;}
   if(c.id=="security-speed"){CruiseMph=CruiseMph<85?95:CruiseMph<105?115:75;brakeUntil=0;game.Dialogue.Say("Diagnostic cruise request: "+CruiseMph+" MPH. Watch the physical instruments.");}
   else{game.Motion.lane=(game.Motion.lane+1)%3;game.Dialogue.Say("Lane request accepted. The active files follow the vehicle.");}
   return true;
  }
  if(game.Chess.Active)return false;
  if(c.id=="glovebox"&&game.State.Has("glovebox-authenticated")){if(game.Ride.glove.State!=CompartmentState.OPEN)game.Ride.glove.Pull();game.State.Flag("clue.route-card");game.Dialogue.Say("The open compartment holds a route card: CHECK = BRAKE. ROOK SACRIFICE = DIVERSION. Inspect USB diagnostics, then the window switch.");return true;}
  if(c.id=="charging"&&game.State.Has("clue.route-card")){StartChallenge(diagnostics);return true;}
  if(c.id.EndsWith("-window")&&game.State.Has("security.diagnostics")){
   if(WindowReleased){game.Dialogue.Say("Window released: "+Mathf.CeilToInt(WindowSeconds)+" seconds remain.");return true;}StartChallenge(window);return true;
  }
  if(c.id=="driver-seat"&&game.State.Has("security.diagnostics"))return false;
  return false;
 }
 public void StartChallenge(ChessChallenge c){if(CanBegin(c)){game.Chess.challenge=c;game.Chess.Begin();}}
 public string Target {
  get {if(!game.State.Has("glovebox-authenticated"))return null;if(!game.State.Has("clue.route-card"))return "glovebox";if(!game.State.Has("security.diagnostics"))return "charging";if(!game.State.Has("security.window"))return "left-window";if(!game.State.Has("item.knight"))return "lost-knight";if(!game.State.Has("rule.jump"))return "seat-scratch";if(!game.State.Has("rule.charge"))return "right-speaker";if(!game.State.Has("security.route"))return "authenticate";return null;}
 }
 public string Objective {
  get {switch(Target){case "glovebox":return "The latch released. Inspect the open glove box and its route card.";case "charging":return "The USB service port can authenticate diagnostic access. Tap it.";case "left-window":return "Test the window's defensive authentication. A safe king releases it for 15 seconds.";case "lost-knight":return "A piece is missing. Recover the white knight from the passenger seat pocket.";case "seat-scratch":return "The knight's base points beneath the rear seat. Inspect the scratched notation.";case "right-speaker":return "Listen to the previous passenger's recording in the right speaker.";case "authenticate":return game.State.Has("security.diversion")?"Return to authentication. Check the king to override navigation — normal moves or the hidden Road Jump.":"Authenticate a diversion. Offer your rook to force braking and a lane change.";default:return "Route override secured · SERVICE LAY-BY. Explore, replay window authentication, or restart the ride.";}}
 }
 public void RefreshDisplays(){if(!game||!game.State.Has("glovebox-authenticated"))return;
  game.Ride.storageText.text="STORAGE RELEASED";
  game.Ride.displayAction.text=game.State.Has("security.route")?"ROUTE OVERRIDE ACCEPTED\nSERVICE LAY-BY":NextChallenge.title+"\nTAP TO AUTHENTICATE";
  game.Ride.dockText.text=WindowReleased?"WINDOW RELEASED / "+Mathf.CeilToInt(WindowSeconds)+" s":game.State.Has("security.diagnostics")?"DIAGNOSTICS CONNECTED\nLANE "+(game.Motion.lane+1)+" / "+Mathf.RoundToInt(game.Motion.speed*2.23694f)+" MPH":"STORAGE OPEN\nINSPECT ROUTE CARD";
  if(game.State.Has("security.route"))game.Ride.navigation.text="SERVICE LAY-BY\nROUTE OVERRIDE ACCEPTED\n"+Mathf.RoundToInt(game.Motion.speed*2.23694f)+" MPH / APPROACH";
 }
}
}
