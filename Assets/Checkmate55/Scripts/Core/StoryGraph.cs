using System;
using System.Collections.Generic;
using System.Linq;
namespace Checkmate55.Core {
// One step of the campaign. A beat is active when all of its Needs flags exist and not all of its Done flags do.
public sealed class Beat {
 public string Id;public string[] Needs,Done;public string Objective,Hint;
 public string[] Targets;                 // physical control ids the guide ring may point at (first still-missing one wins)
 public string[] TargetFlags;             // for each target: the flag that proves it was used (same length as Targets), or null
 public int ParSec,FirstSec;              // planned speed-run seconds / planned first-run seconds
 public StoryPhase Phase;
 public bool Auto;                        // completes by itself (waiting beats)
}
public sealed class StoryGraph {
 public readonly List<Beat> Beats=new List<Beat>();
 static string[] A(params string[] s)=>s;
 void Add(string id,StoryPhase phase,string[] needs,string[] done,string objective,string hint,int par,int first,string[] targets=null,string[] targetFlags=null,bool auto=false)
  =>Beats.Add(new Beat{Id=id,Phase=phase,Needs=needs,Done=done,Objective=objective,Hint=hint,ParSec=par,FirstSec=first,Targets=targets??new string[0],TargetFlags=targetFlags,Auto=auto});
 public StoryGraph(){
  var none=A();
  // ---- 0-5 min: the normal ride -------------------------------------------------------------------------------------
  Add("ride",StoryPhase.Ride,none,A("lockdown"),"Explore the car. When you are ready, REQUEST STOP on the rear display.","Try the climate, the windows, the seats. Nothing here is wrong. Yet.",20,240,A("climate","request-stop"));
  // ---- 5-10: lockdown and the first escape objective ---------------------------------------------------------------
  Add("door",StoryPhase.Lockdown,A("lockdown"),A("tested-door"),"The route has changed. Try a door handle.","Left door handle.",4,20,A("left-handle"));
  Add("window",StoryPhase.Lockdown,A("tested-door"),A("tested-window"),"The door is secured. Test the window switch.","The switch on the left door.",4,20,A("left-window"));
  Add("glovebox",StoryPhase.Lockdown,A("tested-window"),A("glovebox-interest"),"Find a way into restricted storage. Inspect the glove box latch.","The glove box is in the dashboard, front right.",6,30,A("glovebox"));
  Add("policy",StoryPhase.Lockdown,A("glovebox-interest"),A("authentication-known"),"The glove box needs authorization. Read the rear display's storage policy.","Tap the status area of the rear door display.",3,15,A("passenger-display"));
  Add("note",StoryPhase.Lockdown,A("authentication-known"),A("clue.note"),"Find the service instructions in the front passenger seat pocket.","Seat pocket, behind the front passenger seat.",6,30,A("passenger-pocket"));
  Add("usb-label",StoryPhase.Lockdown,A("clue.note"),A("clue.service"),"Follow the note: inspect the USB charging ports between the front seats.","Rear center console, below the climate controls.",3,20,A("charging"));
  Add("trim",StoryPhase.Lockdown,A("clue.service"),A("trim-inspected"),"Follow the service label: lift the trim beside the rear cupholders.","Rear armrest, beside the cupholders.",3,20,A("security-trim"));
  Add("auth",StoryPhase.Lockdown,A("trim-inspected"),A("CHESS_CHALLENGE_COMPLETED","glovebox-authenticated"),"Authenticate on the rear display to deploy the chess security tray.","Put the black king in check within three moves.",35,100,A("authenticate"));
  // ---- 10-17: glove box, physical puzzles, standard chess authentication reward -----------------------------------
  Add("contents",StoryPhase.Exploration,A("glovebox-authenticated"),A("clue.route-card","item.usb","item.phone","item.maintenance-card"),"The glove box is open. Take what the previous passenger left behind.","A route card, a USB stick, a phone and a maintenance card.",14,60,A("route-card","usb-device","phone-device","maint-card"),A("clue.route-card","item.usb","item.phone","item.maintenance-card"));
  Add("diagnostics",StoryPhase.Exploration,A("clue.route-card","item.usb"),A("security.diagnostics"),"Plug the USB stick into the SERVICE port (the one marked with a spanner).","Rear console, below the climate controls: the right-hand port.",28,110,A("usb-port-service"));
  Add("window-lease",StoryPhase.Exploration,A("security.diagnostics"),A("security.window"),"Test the window's defensive authentication. Keep your king safe.","Left window switch.",25,80,A("left-window"));
  Add("knight",StoryPhase.Exploration,A("security.window"),A("item.knight"),"A piece is missing. Recover the white knight from the front seat pocket.","Seat pocket, front passenger seat.",6,30,A("lost-knight"));
  Add("scratch",StoryPhase.Exploration,A("item.knight"),A("rule.jump"),"The knight's base points beneath the rear seat. Inspect the scratched notation.","Under the rear seat, to your left.",5,30,A("seat-scratch"));
  Add("speaker",StoryPhase.Exploration,A("rule.jump"),A("rule.charge"),"Listen to the previous passenger's recording in the right speaker.","Right rear door speaker.",5,30,A("right-speaker"));
  // ---- 17-24: previous passenger, maintenance interlock, hidden transmission, Road Chess --------------------------------
  Add("seat",StoryPhase.RoadChess,A("rule.charge"),A("puzzle.seat"),"The maintenance card mentions a seat interlock. Slide the front passenger seat forward.","The front passenger seat switch, beside the seat.",6,40,A("passenger-seat"));
  Add("dock",StoryPhase.RoadChess,A("puzzle.seat"),A("dock.card"),"A service dock opened on the rear console. Insert the maintenance card.","Rear center console, top.",6,30,A("service-dock"));
  Add("climate",StoryPhase.RoadChess,A("dock.card"),A("puzzle.climate"),"SERVICE TEST 1: set the cabin to the card's temperature pattern, then the fan.","Cold, warm, colder - then fan three. Use the rear climate buttons.",18,80,A("climate-plus","climate-minus","climate-fan"));
  Add("light",StoryPhase.RoadChess,A("dock.card"),A("puzzle.overhead"),"SERVICE TEST 2: press the overhead reading light in the card's rhythm.","Two presses, a pause, three presses.",12,60,A("overhead-light"));
  Add("maintenance",StoryPhase.RoadChess,A("puzzle.climate","puzzle.overhead"),A("maintenance-mode"),"Maintenance mode engaged. The jammer is offline.","",2,10,null,null,true);
  Add("radio",StoryPhase.RoadChess,A("rule.charge"),A("puzzle.radio"),"Passenger 04 hid a transmission. Tune the preset, then isolate the rear-left speaker.","Preset 4, balance fully left, fader fully to the rear.",22,110,A("radio-p4","radio-bal-l","radio-fade-r"),A(null,null,null));
  Add("diversion",StoryPhase.RoadChess,A("item.knight","rule.jump","glovebox-authenticated"),A("security.diversion"),"Authenticate a diversion. Offer your rook to force braking and a lane change.","The car values material over its route.",22,70,A("authenticate"));
  Add("route",StoryPhase.RoadChess,A("security.diversion"),A("security.route"),"Return to authentication. Check the king to override navigation - normal moves or a hidden Road Jump.","Lane and speed decide the vehicle's special moves.",35,110,A("authenticate"));
  // ---- 24-30: outside contact, police pursuit, Cyber Chess escalation ------------------------------------------------------
  Add("phone",StoryPhase.Contact,A("maintenance-mode","item.phone","security.route"),A("phone.powered"),"Maintenance mode lifted the jammer. Put the phone on charge in the OTHER USB port.","Rear console, the left-hand port.",8,40,A("usb-port-phone"));
  Add("marker",StoryPhase.Contact,A("phone.powered"),A("clue.milemarker"),"There is one gap in the signal. Read the nearest mile marker through clear glass.","Lower the window or clear the tint. Mile markers pass on the right.",14,60,A("left-window","right-window"));
  Add("call",StoryPhase.Contact,A("clue.milemarker"),A("outside.contact"),"Place the call and give them the mile marker.","Tap the phone screen.",6,30,A("phone-screen"));
  Add("police",StoryPhase.Pursuit,A("outside.contact"),A("police.visible"),"Hold on. Somebody heard you.","Listen for sirens.",18,40,null,null,true);
  Add("cyber",StoryPhase.Pursuit,A("police.visible"),A("security.cyber"),"The car is jamming the call. Break its route security: Cyber Chess.","Firewall, EMP and Overdrive are on the tray.",45,150,A("authenticate"));
  // ---- final: collision course -----------------------------------------------------------------------------------------------
  Add("ridegame",StoryPhase.Pursuit,A("security.cyber"),A("ridegame.quota"),"The route will not end until the game does. Keep playing your RIDE GAME: the car counts your own moves.","RIDE GAME on the rear console (or press G). Moves the car makes for you do not count.",150,200,A("ridegame"));
  Add("collision",StoryPhase.Final,A("security.cyber","ridegame.quota"),A("collision.started"),"COLLISION COURSE CONFIRMED.","",4,10,null,null,true);
  Add("final",StoryPhase.Final,A("collision.started"),A("ending.any"),"CHECKMATE THE CAR BEFORE IMPACT.","Every rule you have found can be used against it.",80,180,A("authenticate"));
 }
 public Beat Get(string id)=>Beats.FirstOrDefault(b=>b.Id==id);
 static bool All(string[] flags,Func<string,bool> has){if(flags==null)return true;foreach(var f in flags)if(!has(f))return false;return true;}
 public bool IsDone(Beat b,Func<string,bool> has)=>All(b.Done,has);
 public bool IsActive(Beat b,Func<string,bool> has)=>All(b.Needs,has)&&!All(b.Done,has);
 // The first active beat in campaign order: what the objective line and guide ring talk about.
 public Beat Current(Func<string,bool> has){foreach(var b in Beats)if(IsActive(b,has))return b;return null;}
 public IEnumerable<Beat> Active(Func<string,bool> has)=>Beats.Where(b=>IsActive(b,has));
 public StoryPhase PhaseFor(Func<string,bool> has){var c=Current(has);return c==null?StoryPhase.Ended:c.Phase;}
 // The control the ring should point at: the first target of the current beat that has not been used yet.
 public string Target(Func<string,bool> has){
  var b=Current(has);if(b==null||b.Targets.Length==0)return null;
  if(b.TargetFlags!=null)for(int i=0;i<b.Targets.Length;i++)if(b.TargetFlags[i]!=null&&!has(b.TargetFlags[i]))return b.Targets[i];
  return b.Targets[0];
 }
 public int ParTotal=>Beats.Sum(b=>b.ParSec);
 public int FirstRunTotal=>Beats.Sum(b=>b.FirstSec);
 public float Progress(Func<string,bool> has){int total=FirstRunTotal,done=0;foreach(var b in Beats)if(IsDone(b,has))done+=b.FirstSec;return total==0?0:(float)done/total;}
 public int Remaining(Func<string,bool> has){int r=0;foreach(var b in Beats)if(!IsDone(b,has))r+=b.FirstSec;return r;}
}
// Words the player reads on cards, in recordings and on screens. Short on purpose: evidence, not exposition.
public static class StoryText {
 public const string RouteCard="PASSENGER 04\nCHECK = BRAKE\nSURRENDER = DIVERSION\nUSB → DIAGNOSTICS\nTHE WINDOW WANTS A SAFE KING";
 public const string MaintenanceCardFront="SERVICE ACCESS · UNIT 55\nMAINTENANCE ONLY";
 public const string MaintenanceCardBack="DOCK: seat FORWARD.\nCLIMATE: cold, warm, colder.\n19 · 23 · 17 → FAN 3\nLIGHT: two · pause · three";
 public const string PhoneNote="Battery 0%.\nUnlocked home screen: 'Mum' - 31 missed calls.\nLast message: 'they won't let me out of the 55'";
 public const string RadioRecording="(static)... if you hear this, I made it onto preset four.\nIt told me the board had no hidden rules. It lied.\nA pawn can walk through a piece that blocks it - once. The king can trade places with a rook. Only for someone who knows.\nFinish the sequence I could not. Do not let it reach the end of the route.";
 public const string FinalNote="PASSENGER 04 / UNFINISHED:\n1. Card in the dock\n2. Fan to maximum\n3. Lights: two - three\n4. REQUEST STOP three times and HOLD";
 public static readonly int[] ClimatePattern={19,23,17};
 public const int ClimateFan=3;
 public static readonly int[] LightPattern={2,3};
 public const int RadioPreset=4;
}
}
