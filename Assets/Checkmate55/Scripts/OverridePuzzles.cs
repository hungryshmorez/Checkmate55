using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// Physical puzzles: climate pattern, seat interlock, hidden radio transmission, USB, overhead light rhythm, window / mile marker.
// Nothing here is a separate puzzle screen: every step is a real control in the cabin with visible and audible feedback.
public sealed class OverridePuzzles {
 readonly OverrideDirector d;SceneManager game=>d.game;OverrideBuilder b=>d.Build;
 public float TintCar,TintPlayer,SeatCreep,VisorDown;     // written by the threat presenter / tint buttons, shown here
 public int LastMileRead;public float LastMile;
 float dockOpen,flash,markerCheck,dockMsg;readonly List<float> sos=new List<float>();public float SignalUntil;
 PhysicalControl seatControl;float lightFlicker;float climateFlash;float speakNext;
 public OverridePuzzles(OverrideDirector director){d=director;}
 // ---------------------------------------------------------------------------------------------------------------- setup
 public void Initialize(){
  seatControl=game.Registry.Get("passenger-seat");
  d.Climate.SolvedEvent+=()=>{};d.Light.SolvedEvent+=()=>{};
  d.Phone.StateChanged+=OnPhoneState;
  d.Escape.StepDone+=s=>d.Say("Sequence step: "+EscapeSequence.Names[Array.IndexOf(EscapeSequence.Steps,s)],false);
 }
 // What the guide ring / objective line say once the glove box is open.
 public string Target=>d.Story.Target(f=>game.State.Has(f));
 public string Objective{get{var cur=d.Story.Current(f=>game.State.Has(f));return cur==null?"Authentication complete · Explore the cabin.":cur.Objective;}}
 public string Hint{get{var cur=d.Story.Current(f=>game.State.Has(f));return cur==null?"":cur.Hint;}}
 // ---------------------------------------------------------------------------------------------------------------- interception
 // Returns true when the touch was fully handled here (the foundation's own response is skipped).
 public bool Intercept(PhysicalControl c){
  string id=c.id;
  if(id.StartsWith("chess-"))return false;
  if(TryContest(c))return true;
  if(id.StartsWith("radio-"))return RadioPress(id);
  switch(id){
   case "climate-minus":return ClimatePress(d.Climate.Adjust(-1));
   case "climate-plus":return ClimatePress(d.Climate.Adjust(1));
   case "climate-fan":return ClimatePress(d.Climate.CycleFan());
   case "climate":if(d.Climate.Frozen){d.Say("Climate controls are not available.");return true;}return false;
   case "left-tint":case "right-tint":TintPlayer=TintPlayer>0?0:1;game.Audio.ControlSound(id,ControlAction.Switch,false);d.Say(TintPlayer>0?"Privacy glass engaged.":"Privacy glass cleared.",false);return true;
   case "usb-device":case "phone-device":case "maint-card":TakeItem(id);return true;
   case "usb-port-service":return ServicePort();
   case "usb-port-phone":return PhonePort();
   case "service-dock":return Dock();
   case "phone-screen":return PhoneTap();
   case "charging":
    // After authentication the ports are not generic: the stick must be plugged in by hand.
    if(game.State.Has("glovebox-authenticated")&&game.State.Has("clue.route-card")){
     if(!game.State.Has("item.usb")){d.Say("Two powered USB-C ports. Nothing is connected. Something from the glove box would fit the SERVICE port.",false);return true;}
     d.Say("Plug the stick into the port marked with a spanner (right-hand port).",false);return true;
    }
    return false;
   case "request-stop":
    if(d.Escape.Active&&d.Escape.StopTap(Time.time))d.Final.OnStopTriple(Time.time);else if(d.Escape.Active)d.Final.OnStopTap(Time.time);
    return false;
   case "ridegame":return d.RideGame.OnButton();
   case "authenticate":return Authenticate();
  }
  return false;
 }
 bool TryContest(PhysicalControl c){
  string system=SystemOf(c.id);if(system==null||!d.Contests.IsOpen(system))return false;
  game.Audio.Click(380);
  if(d.Contests.Press(system,Time.time)){d.Say("Override accepted: "+system.ToUpper()+" is yours again.",false);}
  else d.Say("The car holds the "+system+" … keep pressing ("+d.Contests.Progress(system)+"/3).",false);
  return true;
 }
 static string SystemOf(string id){
  switch(id){
   case "left-window":case "right-window":return "window";
   case "left-tint":case "right-tint":return "tint";
   case "passenger-seat":return "seat";
   case "climate":case "climate-minus":case "climate-plus":case "climate-fan":return "climate";
   case "radio-mute":case "radio-vol-down":case "radio-vol-up":return "audio";
  }
  if(id.StartsWith("radio-"))return "radio";
  return null;
 }
 bool ClimatePress(bool applied){
  if(!applied){d.Say("Climate controls are not available.",false);return true;}
  game.Audio.Click(700);d.Threat.OnPlayer("climate.change",d.Hostile,d.Mood);
  game.Audio.SetVent(d.Climate.Fan>0);
  return true;
 }
 bool RadioPress(string id){
  var r=d.Radio;bool ok=true;
  switch(id){
   case "radio-p1":case "radio-p2":case "radio-p3":case "radio-p4":case "radio-p5":ok=r.Select(id[7]-'0');if(ok){d.Records.PreferredPreset=id[7]-'0';}break;
   case "radio-vol-down":ok=r.VolumeStep(-1);break;case "radio-vol-up":ok=r.VolumeStep(1);break;case "radio-mute":ok=r.ToggleMute();break;
   case "radio-bal-l":ok=r.BalanceStep(-1);break;case "radio-bal-r":ok=r.BalanceStep(1);break;case "radio-fade-f":ok=r.FaderStep(1);break;case "radio-fade-r":ok=r.FaderStep(-1);break;
  }
  if(!ok)d.Say("The car has the radio.",false);
  return true;
 }
 public void OnRadioChanged(string what){
  if(what=="station")d.Threat.OnPlayer("radio.station",d.Hostile,d.Mood);
  else if(what=="volume.down"||what=="mute")d.Threat.OnPlayer("radio.volume.down",d.Hostile,d.Mood);
 }
 // ---- items ---------------------------------------------------------------------------------------------------------------------
 void TakeItem(string id){
  switch(id){
   case "usb-device":game.State.Flag("item.usb");game.Inventory.Acquire("usb-stick");d.Clues.Add("usb","USB STICK","A USB stick. A spanner is scratched into the casing.");d.Say("A USB stick. A spanner is scratched into the casing: SERVICE.",false);break;
   case "phone-device":game.State.Flag("item.phone");game.Inventory.Acquire("phone");d.Phone.Held=true;d.Clues.Add("phone","PASSENGER 04'S PHONE",StoryText.PhoneNote);d.Say(StoryText.PhoneNote.Replace("\n"," "),false);break;
   case "maint-card":game.State.Flag("item.maintenance-card");game.Inventory.Acquire("maintenance-card");d.Clues.Add("maint","MAINTENANCE CARD",StoryText.MaintenanceCardFront+"\n\n"+StoryText.MaintenanceCardBack);d.Say("A maintenance card. On the back, in pen: "+StoryText.MaintenanceCardBack.Replace("\n"," "),false);break;
  }
  if(b!=null&&b.items.TryGetValue(id,out var t))t.gameObject.SetActive(false);
  game.Audio.ChessSettle();
 }
 // ---- ports / dock / phone --------------------------------------------------------------------------------------------------------------
 bool ServicePort(){
  if(!game.State.Has("glovebox-authenticated")){d.Say("Two powered USB-C ports. Nothing is connected.",false);return true;}
  if(game.State.Has("item.phone")&&!game.State.Has("item.usb")){d.Say("A phone does not belong in the service port.",false);return true;}
  if(!game.State.Has("item.usb")){d.Say("The SERVICE port is empty. Look in the glove box.",false);return true;}
  if(!game.State.Has("usb.inserted")){game.State.Flag("usb.inserted");game.Audio.ChessSettle();d.Say("The stick seats with a click. A service link opens.",false);}
  game.Security.StartChallenge(game.Security.diagnostics);return true;
 }
 bool PhonePort(){
  if(game.State.Has("item.phone")&&!d.Phone.Plugged){
   d.Phone.Held=true;d.Phone.Plugged=true;game.State.Flag("phone.plugged");game.Audio.ChessSettle();
   if(b!=null){b.phoneBody.gameObject.SetActive(true);b.phoneCable.gameObject.SetActive(true);}
   d.Say("The phone lights up: charging.",false);return true;
  }
  if(game.State.Has("item.usb")&&!d.Phone.Plugged){d.Say("Charge only. This port carries no diagnostics.",false);return true;}
  d.Say(d.Phone.Plugged?"The phone is charging.":"A charge port. Nothing is plugged in.",false);return true;
 }
 bool Dock(){
  bool open=game.State.Has("dock.card")||(game.State.Has("puzzle.seat")&&seatControl!=null&&seatControl.Active);
  if(game.State.Has("dock.card")){d.Say(d.Escape.Active?"Card seated. SERVICE STANDBY.":"Card seated. SERVICE STANDBY: complete both service tests.",false);return true;}
  if(!open){d.Say("Service dock sealed. Seat-rail interlock engaged.",false);d.Threat.OnPlayer("forbidden",d.Hostile,d.Mood);return true;}
  if(!game.State.Has("item.maintenance-card")){d.Say("The dock is waiting for a maintenance card.",false);return true;}
  game.State.Flag("dock.card");game.Inventory.Remove("maintenance-card");game.Audio.Latch();game.Audio.Chime();
  d.Say("Card accepted. SERVICE STANDBY. Test 1: climate. Test 2: overhead light.",false);
  return true;
 }
 bool PhoneTap(){
  var p=d.Phone;
  switch(p.State){
   case PhoneState.Missing:case PhoneState.Dead:d.Say("The screen is black.",false);break;
   case PhoneState.Charging:d.Say("Charging… "+(int)p.Battery+"%.",false);break;
   case PhoneState.Jammed:d.Say("NO SERVICE. Something is jamming the signal.",false);d.Threat.OnPlayer("forbidden",d.Hostile,d.Mood);break;
   case PhoneState.Ready:
    if(!game.State.Has("clue.milemarker")){d.Say("The emergency call needs your location. Read the nearest mile marker through clear glass.",false);break;}
    if(p.Call()){game.State.Flag("outside.contact");game.Audio.Chime();Sound_Dial();d.Say("Emergency services: \"Stay on the line. What is your location?\" — A55, mile "+LastMile.ToString("0.0")+".",false);}
    break;
   default:d.Say("The call is connected.",false);break;
  }
  return true;
 }
 void Sound_Dial(){if(d.Sound!=null)d.Sound.Dial();}
 void OnPhoneState(PhoneState s){
  if(s==PhoneState.Jammed){d.Clues.Add("final","PASSENGER 04 / UNFINISHED",StoryText.FinalNote);game.State.Flag("clue.finalnote");d.Say("The phone wakes up. One note is still open: \"UNFINISHED\".",false);}
  if(s==PhoneState.Ready)d.Say("Signal. One bar. Tap the screen to call.",false);
 }
 // ---- authenticate: story-specific challenges ------------------------------------------------------------------------------------------------
 bool Authenticate(){
  if(d.Mode.ChessOnly||d.Mode.Mode==GameMode.Calibration)return d.Modes.OnAuthenticate();
  if(d.Mode.Hostile&&game.State.Has("security.route")&&game.State.Has("police.visible")&&!game.State.Has("security.cyber")){d.Final.StartCyberEscalation();return true;}
  if(d.Mode.Hostile&&game.State.Has("collision.started")&&!d.Final.Ended){d.Final.EnsureBoard();return true;}
  return false;
 }
 // ---------------------------------------------------------------------------------------------------------------- after a normal touch
 public void AfterControl(PhysicalControl c){
  string id=c.id;
  switch(id){
   case "left-window":case "right-window":if(c.Active&&d.Hostile)d.Threat.OnPlayer("window.open",true,d.Mood);break;
   case "left-handle":case "right-handle":d.Threat.OnPlayer("door.try",d.Hostile,d.Mood);break;
   case "glovebox":case "security-trim":d.Threat.OnPlayer("forbidden",d.Hostile,d.Mood);break;
   case "climate":d.Climate.Toggle();d.Threat.OnPlayer("climate.change",d.Hostile,d.Mood);break;
   case "route-card":d.Clues.Add("route","ROUTE CARD",StoryText.RouteCard);break;
   case "seat-scratch":d.Clues.Add("scratch","SCRATCHED NOTATION","70+  N : 3 × 1\nc2 → f3\nIT LIES.");break;
   case "right-speaker":if(game.State.Has("rule.charge"))d.Clues.Add("recording1","RECORDING · RIGHT SPEAKER","At 90+, a rook can cross one friendly piece into another lane. At 110+, the road itself changes the rules.");break;
   case "passenger-seat":
    if(c.Active&&game.State.Has("item.maintenance-card")&&!game.State.Has("puzzle.seat")){game.State.Flag("puzzle.seat");d.Say("Something clicks in the console: the service dock is unlatching.",false);game.Audio.Latch();}
    break;
   case "overhead-light":
    d.Light.Press(Time.time);
    if(d.Hostile){sos.Add(Time.time);sos.RemoveAll(t=>Time.time-t>3f);if(sos.Count>=4){SignalUntil=Time.time+25f;sos.Clear();if(game.State.Has("police.visible"))d.Say("Interior lights flashing: the officers can see it.",false);}}
    if(d.Escape.Active&&d.Light.Solved){d.Escape.Mark("escape.lights");}
    break;
  }
 }
 public void OnFlag(string f){}
 public void OnRecordingHeard(){
  if(game.State.Has("puzzle.radio"))return;
  game.State.Flag("puzzle.radio");game.State.Flag("clue.recording");game.State.Flag("rule.ghost");game.State.Flag("rule.castle");
  d.Clues.Add("recording4","HIDDEN TRANSMISSION · PRESET 4",StoryText.RadioRecording);
  d.AudioJam=false;
  d.Say(StoryText.RadioRecording.Replace("\n"," "),false);
  BrowserPlatform.Speak(StoryText.RadioRecording.Replace("\n"," "),.55f,.82f,.9f);
 }
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void Update(float dt){
  if(b==null)return;
  var glove=game.Ride.glove;
  bool gloveOpen=glove!=null&&glove.State==CompartmentState.OPEN&&game.State.Has("glovebox-authenticated");
  b.itemsRoot.gameObject.SetActive(gloveOpen);
  foreach(var kv in b.items){bool taken=(kv.Key=="usb-device"&&game.State.Has("item.usb"))||(kv.Key=="phone-device"&&game.State.Has("item.phone"))||(kv.Key=="maint-card"&&game.State.Has("item.maintenance-card"));kv.Value.gameObject.SetActive(!taken);}
  // service dock: open while the passenger seat interlock is released (or once the card is seated)
  bool dockTarget=game.State.Has("dock.card")||(game.State.Has("puzzle.seat")&&seatControl!=null&&seatControl.Active);
  dockOpen=Mathf.MoveTowards(dockOpen,dockTarget?1:0,dt*1.6f);
  if(b.dockCover!=null){var p=b.dockCover.localPosition;p.x=-dockOpen*.1f;b.dockCover.localPosition=p;}
  if(b.dockLed!=null)Mats.SetGlow(b.dockLed.sharedMaterial,game.State.Has("dock.card")?new Color(.2f,1f,.7f):dockTarget?new Color(1f,.75f,.15f):new Color(1f,.12f,.06f),1.2f);
  if(b.dockCard!=null)b.dockCard.gameObject.SetActive(game.State.Has("dock.card"));
  // phone screen
  if(b.phoneText!=null){b.phoneText.text=d.Phone.Display(d.MileNow);if(b.phoneGlow!=null)Mats.SetGlow(b.phoneGlow.sharedMaterial,d.Phone.State==PhoneState.Ready?new Color(.3f,1f,.5f):d.Phone.State==PhoneState.Jammed?new Color(1f,.5f,.15f):new Color(.25f,.9f,.95f),d.Phone.Plugged?1f:.1f);}
  d.Phone.JammerOff=game.State.Has("maintenance-mode");d.Phone.HasLocation=game.State.Has("clue.milemarker");
  if(d.Phone.PowerOk&&!game.State.Has("phone.powered"))game.State.Flag("phone.powered");
  // puzzle completions
  if(d.Climate.Solved&&game.State.Has("dock.card")&&!game.State.Has("puzzle.climate")){game.State.Flag("puzzle.climate");game.Audio.Chime();game.Audio.SetVent(true);climateFlash=1.2f;d.Say("SERVICE TEST 1 PASSED. The vents breathe out once.",false);}
  if(d.Light.Solved&&game.State.Has("dock.card")&&!game.State.Has("puzzle.overhead")){game.State.Flag("puzzle.overhead");game.Audio.Chime();lightFlicker=1.5f;d.Say("SERVICE TEST 2 PASSED. The dome light flickers amber.",false);}
  if(game.State.Has("puzzle.climate")&&game.State.Has("puzzle.overhead")&&!game.State.Has("maintenance-mode")){game.State.Flag("maintenance-mode");game.Audio.LockClick();game.Audio.Chime();d.Say("MAINTENANCE MODE. The external signal jammer is offline.",false);}
  if(game.State.Has("outside.contact")&&!d.Police.Active)d.Police.Begin();
  // escape sequence checks that watch a state rather than a tap
  if(d.Escape.Active){if(game.State.Has("dock.card"))d.Escape.Mark("escape.card");if(d.Climate.Fan==3)d.Escape.Mark("escape.fan");}
  // dome light flicker feedback
  if(lightFlicker>0){lightFlicker-=dt;var lamp=game.Registry.Get("overhead-light");if(lamp&&lamp.cabinLight){lamp.cabinLight.enabled=Mathf.Sin(Time.time*40)>0;lamp.cabinLight.color=new Color(1f,.7f,.3f);if(lightFlicker<=0){lamp.cabinLight.color=new Color(1,.82f,.58f);}}}
  // glass tint, seat creep, visors
  AnimateGlass(dt);
  if(seatControl!=null)seatControl.bias=new Vector3(0,0,-.34f*SeatCreep);
  for(int i=0;i<b.visors.Count;i++){var v=b.visors[i];v.localRotation=Quaternion.Slerp(b.visorRot[i],b.visorRot[i]*Quaternion.Euler(68,0,0),VisorDown);v.localPosition=b.visorHome[i]+new Vector3(0,-.09f,-.03f)*VisorDown;}
  AnimateMarkers(dt);
 }
 void AnimateGlass(float dt){
  float side=Mathf.Max(TintCar*.9f,TintPlayer*.62f),front=TintCar*.5f;
  for(int i=0;i<b.tint.Count;i++){var r=b.tint[i];bool ws=r==b.windshieldTint;float a=ws?front:side;r.enabled=a>.01f;if(r.enabled){var col=r.sharedMaterial.color;col.a=Mathf.MoveTowards(col.a,a,dt*.6f);col.r=col.g=col.b=0;r.sharedMaterial.color=col;}}
 }
 public float WindshieldAlpha{get{return b!=null&&b.windshieldTint!=null&&b.windshieldTint.enabled?b.windshieldTint.sharedMaterial.color.a:0;}}
 void AnimateMarkers(float dt){
  float speed=game.Motion.speed;float mileNow=d.MileNow;
  for(int i=0;i<b.markers.Length;i++){
   var m=b.markers[i];if(m==null)continue;var p=m.localPosition;p.z-=speed*dt;
   if(p.z<-40){p.z+=3*105f;b.markerText[i].text="A55\nMILE "+(mileNow+p.z/1609.344f).ToString("0.0");}
   m.localPosition=p;
   // live mile value while it approaches (it never changes after passing)
   if(p.z>5)b.markerText[i].text="A55\nMILE "+Mathf.Round((mileNow+p.z/1609.344f)*10)/10f;
  }
  markerCheck-=dt;if(markerCheck>0)return;markerCheck=.15f;
  if(!game.State.Has("phone.powered")||game.State.Has("clue.milemarker"))return;
  var cam=game.Player.view.transform;bool clear=WindshieldAlpha<.3f;
  for(int i=0;i<b.markers.Length;i++){
   var m=b.markers[i];Vector3 to=m.position+Vector3.up*2.4f-cam.position;float dist=to.magnitude;if(dist<5||dist>38)continue;
   if(Vector3.Angle(cam.forward,to)>32f)continue;
   if(!clear)continue;
   LastMile=Mathf.Round((mileNow+m.localPosition.z/1609.344f)*10)/10f;game.State.Flag("clue.milemarker");d.Clues.Add("mile","MILE MARKER","A55 · MILE "+LastMile.ToString("0.0"));
   d.Say("Mile marker: A55, mile "+LastMile.ToString("0.0")+". That is where you are.",false);game.Audio.Chime();break;
  }
 }
 public void UpdateRadio(float dt){
  if(b==null||b.radioTitle==null)return;
  var r=d.Radio;
  b.radioTitle.text=d.AudioJam?"§§ SIGNAL LOST §§":r.Muted?"MUTED":d.Sound!=null?d.Sound.StationLabel(r.Station):"PRESET "+r.Station;
  b.radioDisplay.text=r.Display(d.AudioJam);
  for(int i=1;i<=5;i++){var rend=b.presetLed[i];if(rend)Mats.SetGlow(rend.sharedMaterial,i==r.Station?new Color(.25f,1f,.85f):new Color(.18f,.35f,.38f),i==r.Station?1.3f:.15f);}
  if(b.padDot)b.padDot.localPosition=new Vector3(r.Balance*.036f,.0075f,r.Fader*.034f);
 }
 public void UpdateClimate(){}
 public void LateUpdate(){
  if(b==null)return;
  if(b.warningText!=null)b.warningText.text=WarningLine;
 }
 string warningLine="";float warningUntil;
 public string WarningLine=>Time.time<warningUntil?warningLine:"";
 public void ShowWarning(string line,float seconds){warningLine=line;warningUntil=Time.time+seconds;}
}
}
