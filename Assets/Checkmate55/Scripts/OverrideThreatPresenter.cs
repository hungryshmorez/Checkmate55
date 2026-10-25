using System;
using System.Collections;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// Carries out what the ThreatDirector decides. Fear comes from LOSS OF CONTROL, not from darkness: the daylight stays, the road keeps
// rolling past, and the controls quietly stop obeying. Every takeaway can be won back by pressing the same control again and again.
public sealed class OverrideThreatPresenter {
 readonly OverrideDirector d;SceneManager game=>d.game;Rng rng;
 float seatTarget,visorUntil,jamUntil,beltUntil,seatSpeed=.07f;
 readonly System.Collections.Generic.Dictionary<string,float> openedAt=new System.Collections.Generic.Dictionary<string,float>();
 public ThreatAction LastAction{get;private set;}
 public bool ForceHold;                 // a human car pays for this action: its effects always take hold (and open a contest)
 bool Hot(float level)=>ForceHold||d.Threat.Intensity>level;
 public OverrideThreatPresenter(OverrideDirector director){d=director;rng=new Rng(director.game.RunSeed+41);}
 // ---------------------------------------------------------------------------------------------------------------- per frame
 public void Update(float dt){
  if(!d.Mode.Hostile||d.Faulted("threat"))return;
  var ctx=new ThreatContext{Phase=d.Phase,Hostile=d.Hostile,ChessActive=game.Chess.Active,PuzzleBusy=game.Chess.Active&&d.Phase!=StoryPhase.Final,PoliceDistance=d.Police.Active?d.Police.Distance:-1,OpenContests=d.Contests.Open,Mood=d.Mood};
  if(d.ThreatsEnabled){var a=d.Threat.Tick(dt,ctx);if(a!=null){Execute(a);d.Roles?.OnThreat(a);}}
  var p=d.Puzzles;
  // seat creeps slowly toward the passenger's knees; winning the contest pushes it back quickly
  p.SeatCreep=Mathf.MoveTowards(p.SeatCreep,seatTarget,dt*(seatTarget>p.SeatCreep?seatSpeed:.45f));
  if(Time.time>visorUntil)p.VisorDown=Mathf.MoveTowards(p.VisorDown,0,dt*.4f);else p.VisorDown=Mathf.MoveTowards(p.VisorDown,1,dt*.5f);
  if(d.AudioJam&&Time.time>jamUntil&&d.Contests.IsOpen("audio")==false)d.AudioJam=false;
  if(Time.time<beltUntil)game.Player.ExternalLean=Vector3.Lerp(game.Player.ExternalLean,new Vector3(0,-.01f,-.045f),dt*5);else game.Player.ExternalLean=Vector3.Lerp(game.Player.ExternalLean,Vector3.zero,dt*3);
  // the car eventually forgets a takeaway the passenger never noticed, so no one can be stuck forever
  foreach(var s in new System.Collections.Generic.List<string>(d.Contests.OpenSystems)){float t;if(openedAt.TryGetValue(s,out t)&&Time.time-t>70f){d.Contests.Cancel(s);Release(s);}}
 }
 // ---------------------------------------------------------------------------------------------------------------- actions
 void BeginContest(string system,string help){
  if(string.IsNullOrEmpty(system)||d.Contests.IsOpen(system))return;
  d.Contests.Begin(system,Time.time);openedAt[system]=Time.time;d.Puzzles.ShowWarning(help,6f);
 }
 public void Execute(ThreatAction a){
  LastAction=a;bool said=false;
  switch(a.Kind){
   case ThreatKind.RadioChange:
    if(!ForceHold&&d.Radio.Favorite>0&&d.Threat.Intensity>.45f&&rng.Chance(.6f)){d.Radio.RemoveFavorite();a.Line="Your favourite station is no longer available.";}
    else{int p=rng.Range(1,6);if(p==d.Radio.Station)p=p%5+1;d.Radio.CarSelect(p);}
    if(Hot(.4f)){d.Radio.Locked=true;BeginContest("radio","CAR HOLDS THE RADIO · PRESS A RADIO BUTTON REPEATEDLY");}break;
   case ThreatKind.VolumeUp:d.Radio.CarVolume(Mathf.Min(1,d.Radio.Volume+.4f));if(Hot(.5f)){d.Radio.Locked=true;BeginContest("audio","CAR HOLDS THE VOLUME · PRESS VOL / MUTE REPEATEDLY");}break;
   case ThreatKind.VolumeDown:d.Radio.CarVolume(.08f);break;
   case ThreatKind.SpeakerRoute:{
    // the car sends the sound to a corner - never the corner that carries the hidden transmission
    float[] cx={1,1,-1};float[] cy={1,-1,1};int i=rng.Range(0,3);d.Radio.CarRoute(cx[i],cy[i]);break;}
   case ThreatKind.FavoriteRemoved:d.Radio.RemoveFavorite();break;
   case ThreatKind.WindowClose:{
    bool any=false;foreach(var id in new[]{"left-window","right-window"}){var c=game.Registry.Get(id);if(c!=null&&c.Active){c.SetActive(false);any=true;}}
    if(game.Security.WindowReleased){game.Security.EndWindowLease();any=true;}
    if(any){game.Audio.ControlSound("left-window",ControlAction.Switch,false);BeginContest("window","WINDOWS LOCKED · PRESS A WINDOW SWITCH REPEATEDLY");}
    else{d.Puzzles.TintCar=Mathf.Max(d.Puzzles.TintCar,.5f);a.Line=CarVoice.Line("tint",d.Mood,rng);}
    break;}
   case ThreatKind.TintUp:d.Puzzles.TintCar=1;BeginContest("tint","PRIVACY GLASS LOCKED · PRESS A TINT BUTTON ON THE DOORS");break;
   case ThreatKind.SunshadeClose:visorUntil=Time.time+9f;break;
   case ThreatKind.ClimateShutdown:d.Climate.Frozen=true;game.Audio.SetVent(false);BeginContest("climate","CLIMATE OFFLINE · PRESS THE CLIMATE BUTTONS REPEATEDLY");break;
   case ThreatKind.ClimateReset:d.Climate.Reset();game.Audio.SetVent(true);break;
   case ThreatKind.SeatCreep:seatTarget=1;BeginContest("seat","FRONT SEAT MOVING · PRESS ITS SEAT SWITCH REPEATEDLY");break;
   case ThreatKind.BeltTighten:beltUntil=Time.time+1.8f;game.Player.FovShake=-4f;game.Audio.LockClick();break;
   case ThreatKind.LockCycle:d.StartCoroutine(LockCycle());break;
   case ThreatKind.Accelerate:d.Boost(12f+a.Strength*8f,12f);game.Player.FovShake=3f;break;
   case ThreatKind.LaneChange:{int lane=game.Motion.lane;game.Motion.lane=lane==1?(rng.Chance(.5f)?0:2):1;break;}
   case ThreatKind.WarningDisplay:d.Puzzles.ShowWarning("UNAUTHORIZED ACTIVITY: REAR "+(rng.Chance(.5f)?"LEFT":"RIGHT"),5f);game.Audio.Chime();break;
   case ThreatKind.NavChange:game.Ride.DistanceMiles+=rng.Range(1.1f,2.6f);d.Puzzles.ShowWarning("ROUTE RECALCULATED",4f);break;
   case ThreatKind.PuzzleInterference:Interfere();break;
   case ThreatKind.JamAudio:d.AudioJam=true;jamUntil=Time.time+20f;d.Radio.CarVolume(1f);BeginContest("audio","EXTERNAL AUDIO JAMMED · PRESS VOL / MUTE REPEATEDLY");break;
  }
  if(!said&&!string.IsNullOrEmpty(a.Line))d.Say(a.Line);
 }
 IEnumerator LockCycle(){for(int i=0;i<3;i++){game.Audio.LockClick();yield return new WaitForSeconds(.38f);}}
 // While the board is open the road bleeds into the game: hazard warnings and swerves.
 void Interfere(){
  if(game.Chess.Active&&!game.Chess.Busy){game.Chess.ExternalHazard(1+(d.Mph>=110?1:0));d.Puzzles.ShowWarning("ROAD EVENT · HAZARD WARNING ON THE TRAY",4f);}
  else{game.Motion.lane=(game.Motion.lane+1)%3;d.Puzzles.ShowWarning("LANE CHANGE",3f);}
 }
 // ---------------------------------------------------------------------------------------------------------------- contests
 public void OnContestWon(string system){
  Release(system);d.Threat.OnCountered();
  d.Say(CarVoice.Line("discovery",d.Mood,rng));
  d.Records.SecretRulesMask|=0;
 }
 void Release(string system){
  switch(system){
   case "window":game.Security.GrantWindowLease(14f);break;
   case "tint":d.Puzzles.TintCar=0;d.Puzzles.TintPlayer=0;break;
   case "seat":seatTarget=0;break;
   case "climate":d.Climate.Frozen=false;game.Audio.SetVent(d.Climate.Fan>0);break;
   case "audio":d.AudioJam=false;d.Radio.Locked=d.Contests.IsOpen("radio");break;
   case "radio":d.Radio.Locked=d.Contests.IsOpen("audio");break;
  }
  openedAt.Remove(system);
 }
}
}
