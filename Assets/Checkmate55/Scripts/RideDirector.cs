using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
// Extends the authored cabin. Input always consults live state through SceneManager.
public sealed class RideDirector : MonoBehaviour {
 public Transform boardingDoor,leftGlass,rightGlass,trimPanel,dockCover;
 public Transform[] lockPins;
 public Renderer[] accents;
 public TextMesh navigation,instruments,displayStatus,displayAction,guidanceText,climateText,storageText,dockText;
 public TextMesh[] guidanceOptions;
 public GloveCompartment glove;
 public Vector3 BoardingOffset {get;private set;}
 public float DistanceMiles {get;set;}=55;
 // OVERRIDE: non-hostile modes (Calibration, Passenger Chess) never lock the car; Story waits for a real stretch of normal ride first.
 public bool LockdownAllowed=true;public float LockdownNotBefore=75,LockdownAt=300;float rideClock;
 public bool Hostile=>game!=null&&game.State.Has("lockdown");
 public bool Ready=>game!=null&&game.State.Has("authentication-ready");
 SceneManager game;float boarding,uiClock;Vector3 leftHome,rightHome,trimHome,dockHome;bool trimOpen;HashSet<string> explored=new HashSet<string>();
 readonly Color cool=new Color(.62f,.89f,.92f),hot=new Color(1,.36f,.27f);
 public void Initialize(SceneManager owner){
  game=owner;leftHome=leftGlass.localPosition;rightHome=rightGlass.localPosition;trimHome=trimPanel.localPosition;dockHome=dockCover.localPosition;
  game.Motion.speed=0;game.State.Enter(RideState.BOARDING);BoardingOffset=new Vector3(-.8f,.08f,-.12f);boardingDoor.localRotation=Quaternion.Euler(0,64,0);
  game.AI.React("boarding");game.Puzzles.Completed+=OnPuzzle;RefreshDisplays();
 }
 void OnDestroy(){if(game!=null)game.Puzzles.Completed-=OnPuzzle;}
 void OnPuzzle(PuzzleDefinition puzzle){if(puzzle.Id=="passenger-auth"){game.Audio.Chime();game.AI.React("puzzle");}}
 void Update(){if(game==null||game.Paused||(game.Override!=null&&game.Override.WaitingForMode))return;float dt=Time.deltaTime;
  if(game.State.Current==RideState.BOARDING){boarding+=dt;BoardingOffset=Vector3.Lerp(new Vector3(-.8f,.08f,-.12f),Vector3.zero,Mathf.SmoothStep(0,1,boarding/1.35f));boardingDoor.localRotation=Quaternion.Euler(0,Mathf.Lerp(64,0,Mathf.SmoothStep(0,1,(boarding-.8f)/1.4f)),0);if(boarding>=2.3f){boardingDoor.localRotation=Quaternion.identity;game.Audio.Latch();game.State.Flag("boarded");game.State.Enter(RideState.NORMAL_RIDE);}}
  else {bool drive=Hostile||(game.Override!=null&&game.Override.DrivesSpeed);game.Motion.speed=Mathf.MoveTowards(game.Motion.speed,drive?game.Security.TargetSpeed:24f,dt*(drive?4:8));}
  if(Hostile)DistanceMiles=Mathf.Max(.1f,DistanceMiles-game.Motion.speed*dt/1609.344f);
  if(game.State.Current==RideState.NORMAL_RIDE){rideClock+=dt;if(LockdownAllowed&&(explored.Count>=5&&rideClock>=LockdownNotBefore||rideClock>=LockdownAt))TriggerLockdown();}
  SlideWindow(leftGlass,leftHome,game.Registry.Get("left-window"));SlideWindow(rightGlass,rightHome,game.Registry.Get("right-window"));
  trimPanel.localPosition=Vector3.Lerp(trimPanel.localPosition,trimHome+(trimOpen?Vector3.up*.055f:Vector3.zero),dt*5);
  dockCover.localPosition=Vector3.Lerp(dockCover.localPosition,dockHome+(Ready?Vector3.forward*.12f:Vector3.zero),dt*4);
  if(uiClock<=0){RefreshDisplays();uiClock=.2f;}uiClock-=dt;
 }
 void SlideWindow(Transform glass,Vector3 home,PhysicalControl control){glass.localPosition=Vector3.MoveTowards(glass.localPosition,home+Vector3.down*((control.Active&&!Hostile||Hostile&&game.Security.WindowReleased)?.56f:0),Time.deltaTime*.4f);}
 public void TriggerLockdown(){if(Hostile||game.State.Current==RideState.BOARDING)return;
  game.State.Flag("lockdown");game.State.Enter(RideState.LOCKDOWN);DistanceMiles=55;game.Audio.LockClick();game.Audio.Chime();game.AI.React("lockdown");
  foreach(var pin in lockPins)pin.localPosition+=Vector3.down*.025f;
  foreach(var r in accents){var m=r.material;m.color=new Color(.7f,.035f,.018f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1,.035f,.01f)*.7f);}
  RefreshDisplays();
 }
 public bool BeforeUse(PhysicalControl c){
  if(game.Chess&&game.Chess.Handle(c.id))return true;
  if(game.Security.Use(c))return true;
  if(game.Chess&&game.Chess.Active&&(c.id=="armrest"||c.id=="security-trim")){game.Dialogue.Say("Retract the security tray before moving its armrest.");return true;}
  if(c.id.StartsWith("guidance-")){game.Save.Guidance=int.Parse(c.id.Substring(9));game.State.Flag("guidance-selected");game.Dialogue.Say("Guidance: "+GuidanceName(game.Save.Guidance)+". Preference saved.");RefreshDisplays();return true;}
  if(c.id=="request-stop"){
   if(game.State.Current==RideState.BOARDING)game.Dialogue.Say("Boarding in progress. The door is closing.");
   else if(!LockdownAllowed)game.Dialogue.Say("Stop request acknowledged. This is a calibration ride: nothing is wrong.");
   else {TriggerLockdown();game.AI.React("exit");}return true;
  }
  if(c.id=="authenticate"){Authenticate();return true;}
  if(Hostile&&(c.id.EndsWith("-window")||c.id=="driver-seat"&&!game.State.Has("security.diagnostics"))){
   if(c.id.EndsWith("-window")){game.State.Flag("tested-window");game.Dialogue.Say("Window override denied. Passenger security has control.");}else game.Dialogue.Say("Driver controls are reserved for the vehicle.");
   RefreshDisplays();return true;
  }
  return false;
 }
 public void AfterUse(PhysicalControl c){
  explored.Add(c.id);
  switch(c.id){
   case "left-handle":case "right-handle":if(Hostile)game.State.Flag("tested-door");game.AI.React("door",c.UseCount);break;
   case "left-window":case "right-window":game.Dialogue.Say(c.Active?"The window lowers. Warm air rushes in.":"The window rises into its seal.");break;
   case "glovebox":game.State.Flag("glovebox-interest");glove.Pull();if(glove.State==CompartmentState.LOCKED)game.AI.React("glovebox");break;
   case "passenger-display":
    if(Hostile&&game.State.Has("glovebox-interest")&&game.State.Has("tested-door")&&game.State.Has("tested-window")){game.State.Flag("authentication-known");game.Dialogue.Say("RESTRICTED STORAGE: passenger authentication required. Service instructions are in the seat pocket.");}
    else game.Dialogue.Say(Hostile?"Destination UNKNOWN. Doors and windows secured. Restricted storage: consult passenger services.":"Riverside station. Choose your guidance below, or request a stop.");break;
   case "passenger-pocket":case "driver-pocket":
    game.Inventory.Acquire("passenger-note");game.State.Flag("clue.note");game.Dialogue.Say("A folded note: 'Storage uses passenger authentication. Inspect the USB service ports, then lift the armrest trim.'");break;
   case "charging":game.State.Flag("clue.service");game.Dialogue.Say("USB service label: AUTH LINK beneath cupholder trim. Prepare link, then authenticate on the rear display.");break;
   case "security-trim":
    if(!game.State.Has("clue.service")){game.Dialogue.Say("A removable trim panel beside the cupholders. A tiny USB service symbol is stamped into its edge.");break;}
    trimOpen=true;game.State.Flag("trim-inspected");game.AI.React("panel");break;
   case "armrest":if(Ready){game.State.Flag("console-ready");game.Dialogue.Say("The security dock is armed. A folded chess mechanism waits beneath the lid. Tap authentication on the rear display to deploy the lap tray.");}break;
   case "cupholders":game.Dialogue.Say("A fine seam runs beside the cupholders. The trim carries a USB service symbol.");break;
   case "climate":game.Dialogue.Say(c.Active?"Climate set to 19°. The rear fan spins up.":"Climate set to 21°. The fan settles.");break;
   case "left-speaker":case "right-speaker":game.AI.React(Hostile?"secret":"boarding");break;
  }
  RefreshDisplays();
 }
 public void Authenticate(){
  if(game.State.Has("glovebox-authenticated")){game.Security.StartChallenge(game.Security.NextChallenge);return;}
  if(Ready){game.Chess.Begin();return;}
  var p=game.Puzzles.Definitions["passenger-auth"];
  if(p.Available(game.State)){game.Puzzles.Advance(p.Id,1,game.State);game.Puzzles.Resolve(p.Id,game.State);}
  else if(!Hostile)game.Dialogue.Say("Passenger storage authentication is available when security is active.");
  else if(!game.State.Has("glovebox-interest"))game.Dialogue.Say("Select a restricted compartment first. The front glove box has a maintenance indicator.");
  else if(!game.State.Has("tested-door")||!game.State.Has("tested-window"))game.Dialogue.Say("Confirm the passenger restrictions. Try a door handle and window switch.");
  else if(!game.State.Has("authentication-known"))game.Dialogue.Say("Tap the rear display status area to inspect the storage policy.");
  else if(!game.State.Has("clue.note"))game.Dialogue.Say("Service instructions are in the front seat pocket.");
  else if(!game.State.Has("clue.service"))game.Dialogue.Say("Inspect the charging ports beneath the rear climate controls.");
  else game.Dialogue.Say("Lift the service trim beside the armrest cupholders to prepare the authentication link.");
  RefreshDisplays();
 }
 public static string GuidanceName(int value)=>value==0?"FULL GUIDANCE":value==1?"CHESS PLAYER":"MINIMAL";
 public void RefreshDisplays(){if(game==null)return;var color=Hostile?hot:cool;int mph=Mathf.RoundToInt(game.Motion.speed*2.23694f);
  navigation.text=Hostile?"DESTINATION  UNKNOWN\n"+DistanceMiles.ToString("F1")+" MI   /   "+mph+" MPH\nPASSENGER SECURITY ACTIVE":"RIVERSIDE STATION\n"+mph+" MPH   /   AUTONOMOUS\nA55  NORTH";navigation.color=color;
  instruments.text=mph+"\nMPH";instruments.color=color;
  displayStatus.text=Hostile?"UNKNOWN  /  "+DistanceMiles.ToString("F1")+" MI\n"+mph+" MPH   ·   DOORS SECURED":"RIVERSIDE STATION\n"+mph+" MPH   ·   COMFORT RIDE";displayStatus.color=color;
  guidanceText.text="PASSENGER GUIDANCE";
  for(int i=0;i<3;i++){guidanceOptions[i].text=(game.Save.Guidance==i?"•  ":"   ")+GuidanceName(i);guidanceOptions[i].color=game.Save.Guidance==i?Color.white:new Color(.47f,.64f,.69f);}
  displayAction.text=Ready?"AUTH LINK READY\nTAP TO AUTHENTICATE":game.State.Has("authentication-known")?"STORAGE: AUTH REQUIRED\nTAP TO PREPARE AUTHENTICATION":"RESTRICTED STORAGE\nPASSENGER AUTHENTICATION";displayAction.color=color;
  climateText.text=game.Override!=null?game.Override.ClimateReadout():(game.Registry.Get("climate").Active?"−     19°     +":"−     21°     +");
  storageText.text=game.State.Has("glovebox-authenticated")?"IDENTITY VERIFIED":Ready?"AUTH PENDING":"LOCKED / AUTH";storageText.color=color;
  dockText.text=Ready?"AUTH LINK READY\nCHESS MODULE / STANDBY":trimOpen?"SERVICE LINK\nRETURN TO REAR DISPLAY":"PASSENGER SECURITY\nSERVICE TRIM";dockText.color=color;game.Security?.RefreshDisplays();
 }
}
}
