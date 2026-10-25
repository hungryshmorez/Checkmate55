using System;
using System.Collections.Generic;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// The conductor of CHECKMATE 55: OVERRIDE. It owns the pure-logic systems (story, threat, radio, police ...) and the modules that
// present them in the cabin. Every module call is fenced: if a new system ever throws, the foundation game keeps running.
public sealed class OverrideDirector : MonoBehaviour {
 public SceneManager game;
 public OverrideBuilder Build;public HandRig Hands;
 public readonly StoryGraph Story=new StoryGraph();
 public readonly ContestBoard Contests=new ContestBoard();
 public readonly RadioModel Radio=new RadioModel();
 public readonly ClimateModel Climate=new ClimateModel();
 public readonly RhythmLock Light=new RhythmLock(StoryText.LightPattern);
 public readonly PhoneModel Phone=new PhoneModel();
 public readonly ClueBook Clues=new ClueBook();
 public readonly EscapeSequence Escape=new EscapeSequence();
 public ThreatDirector Threat;public PoliceModel Police;public Records Records=new Records();
 public ModeConfig Mode=ModeCatalog.For(GameMode.Story);
 public OverridePuzzles Puzzles;public OverrideThreatPresenter Presenter;public OverrideAudio Sound;public OverridePolice PolicePresenter;public OverrideFinal Final;public OverrideModes Modes;public OverrideUI UI;public OverrideRoles Roles;public OverrideExterior Exterior;public OverrideRideGame RideGame;
 public bool ThreatsEnabled=true;       // false when a human car (or a remote host) decides the threats instead of the ThreatDirector
 public bool WaitingForMode{get;private set;}=true;
 public float StoryClock{get;private set;}
 public CarMood Mood{get;private set;}
 public bool VoiceOn=true;
 public float ThreatBoostMph;float boostUntil;
 public bool AudioJam;                 // the car is jamming external audio (radio blasts, police unintelligible)
 public bool TintedByCar;
 readonly Dictionary<string,int> faults=new Dictionary<string,int>();
 float moodClock,mile;Rng voiceRng;
 // ---------------------------------------------------------------------------------------------------------------- startup
 public void Initialize(SceneManager owner){
  game=owner;voiceRng=new Rng(game.RunSeed+3);
  Records=Records.Parse(PlayerPrefs.GetString("cm55.records",""));VoiceOn=PlayerPrefs.GetInt("cm55.voice",1)==1;
  Threat=new ThreatDirector(game.RunSeed+11);Police=new PoliceModel(game.RunSeed+29);
  Radio.PreferredStart(Records.PreferredPreset);
  Safe("builder",()=>{Build=new OverrideBuilder(game);Build.Build();});
  Safe("hands",()=>{Hands=HandRig.Create(game);game.Interactions.Hands=Hands;game.Player.Hands=Hands;});
  Sound=gameObject.AddComponent<OverrideAudio>();Safe("sound",()=>Sound.Initialize(this));
  Puzzles=new OverridePuzzles(this);Presenter=new OverrideThreatPresenter(this);PolicePresenter=new OverridePolice(this);Final=new OverrideFinal(this);Modes=new OverrideModes(this);Roles=new OverrideRoles(this);Exterior=new OverrideExterior(this);RideGame=new OverrideRideGame(this);UI=gameObject.AddComponent<OverrideUI>();UI.director=this;
  Safe("puzzles",Puzzles.Initialize);Safe("police",PolicePresenter.Initialize);Safe("final",Final.Initialize);Safe("modes",Modes.Initialize);Safe("roles",Roles.Initialize);Safe("exterior",Exterior.Build);Safe("ridegame",()=>{if(Build!=null){RideGame.ControlRoot=Build.rideControlRoot;RideGame.Label=Build.rideLabel;RideGame.Led=Build.rideLamp;}RideGame.Initialize();});
  Radio.Changed+=Puzzles.OnRadioChanged;Radio.RecordingHeard+=Puzzles.OnRecordingHeard;
  Contests.Won+=Presenter.OnContestWon;
  game.State.Discovered+=OnDiscovered;
  game.Chess.EventRaised+=Final.OnChessEvent;game.Chess.Ended+=Final.OnChessEnded;game.Chess.Completed+=Final.OnChessCompleted;
  WaitingForMode=true;
#if UNITY_EDITOR
  if(PlayerPrefs.GetInt("cm55.verify",0)==1&&GetComponent<OverrideVerification>()==null)gameObject.AddComponent<OverrideVerification>();
#endif
  int auto=PlayerPrefs.GetInt("cm55.autostart",0);
  if(auto>0){PlayerPrefs.DeleteKey("cm55.autostart");var m=(GameMode)(auto-1);if(Enum.IsDefined(typeof(GameMode),m)&&!ModeCatalog.For(m).Networked)StartMode(m);}
 }
 void OnDestroy(){if(game!=null){game.State.Discovered-=OnDiscovered;if(game.Chess){game.Chess.EventRaised-=Final.OnChessEvent;game.Chess.Ended-=Final.OnChessEnded;game.Chess.Completed-=Final.OnChessCompleted;}}}
 // Called by the title menu.
 public void StartMode(GameMode mode,Action<ModeConfig> configure=null){
  Mode=ModeCatalog.For(mode);configure?.Invoke(Mode);
  WaitingForMode=false;StoryClock=0;
  game.AllRulesKnown=Mode.AllRulesKnown;game.AllowChessWithoutAuth=Mode.ChessOnly||mode==GameMode.Calibration;
  game.Ride.LockdownAllowed=Mode.Hostile;game.Motion.speed=0;
  Safe("mode",()=>Modes.Begin(Mode));Safe("roles",()=>Roles.Begin(Mode));Safe("ridegame",()=>RideGame.Begin(Mode));
  game.AI.React("boarding");
 }
 // ---------------------------------------------------------------------------------------------------------------- per frame
 void Update(){
  if(game==null)return;
  if(WaitingForMode){game.Motion.speed=24f;Safe("net",Roles.PumpLobby);Safe("exterior",()=>Exterior.Update(Time.deltaTime));return;}
  if(game.Paused)return;
  float dt=Time.deltaTime;StoryClock+=dt;
  moodClock-=dt;if(moodClock<=0){moodClock=.5f;Mood=MoodModel.For(f=>game.State.Has(f));}
  if(Time.time>boostUntil)ThreatBoostMph=Mathf.MoveTowards(ThreatBoostMph,0,dt*6);
  mile+=game.Motion.speed*dt/1609.344f;
  Safe("radio",()=>{Radio.Tick(dt);Puzzles.UpdateRadio(dt);});
  Safe("climate",()=>{Climate.Tick(dt);Light.Tick(Time.time);Phone.Tick(dt);Puzzles.UpdateClimate();});
  Safe("puzzles",()=>Puzzles.Update(dt));
  Safe("threat",()=>Presenter.Update(dt));
  Safe("police",()=>PolicePresenter.Update(dt));
  Safe("final",()=>Final.Update(dt));
  Safe("modes",()=>Modes.Update(dt));Safe("roles",()=>Roles.Update(dt));Safe("exterior",()=>Exterior.Update(dt));Safe("ridegame",()=>RideGame.Update(dt));
 }
 void LateUpdate(){if(game==null||WaitingForMode)return;Safe("displays",()=>{Puzzles.LateUpdate();Modes.LateUpdate();});}
 // ---------------------------------------------------------------------------------------------------------------- fences
 public void Safe(string module,Action a){
  try{a();}catch(Exception e){int n;faults.TryGetValue(module,out n);faults[module]=n+1;if(n<3)Debug.LogError("[CHECKMATE 55: OVERRIDE] "+module+" fault: "+e);}
 }
 public bool Faulted(string module)=>faults.TryGetValue(module,out var n)&&n>=3;
 // ---------------------------------------------------------------------------------------------------------------- hooks from the foundation
 public bool Intercept(PhysicalControl c){bool r=false;if(WaitingForMode)return true;Safe("intercept",()=>r=Puzzles.Intercept(c));return r;}
 public void AfterControl(PhysicalControl c){Safe("after",()=>{Puzzles.AfterControl(c);Roles.OnTouched(c);});}
 void OnDiscovered(string flag){
  Safe("discovered",()=>{
   if(IsImportant(flag)){Threat.OnDiscovery(Hostile);}
   Puzzles.OnFlag(flag);Roles.OnFlag(flag);
  });
 }
 static bool IsImportant(string f)=>f=="CHESS_CHALLENGE_COMPLETED"||f.StartsWith("security.")||f.StartsWith("rule.")||f.StartsWith("puzzle.")||f=="clue.milemarker"||f=="outside.contact"||f=="maintenance-mode"||f=="clue.recording";
 // Looks a challenge up by id (room play: every client builds the same boards from the same ids).
 public ChessChallenge FindChallenge(string id){
  ChessChallenge found=null;
  foreach(var c in Resources.FindObjectsOfTypeAll<ChessChallenge>())if(c!=null&&c.id==id)found=c;
  if(found==null&&Final!=null)found=Final.Lookup(id);
  if(found==null&&Modes!=null&&id=="passenger")found=Modes.BuildPassengerChallenge();
  return found;
 }
 // ---------------------------------------------------------------------------------------------------------------- shared helpers
 public bool Has(string flag)=>game.State.Has(flag);
 public void Flag(string flag){game.State.Flag(flag);}
 public bool Hostile=>Mode.Hostile&&game.Ride.Hostile;
 // Chess-only road modes let the engine's speed and lane move the physical car while the tray is open.
 public bool DrivesSpeed=>!WaitingForMode&&Mode.ChessOnly&&Mode.Road&&game.Chess.Active||(Modes!=null&&Modes.ForcedMph>0);
 public float Mph=>game.Motion.speed*2.23694f;
 public float MileNow=>7f+mile;
 public int Lane=>game.Motion.lane;
 public StoryPhase Phase{get{
  if(!Mode.Hostile)return StoryPhase.Ride;
  if(Final.Ended)return StoryPhase.Ended;
  if(Has("collision.started"))return StoryPhase.Final;
  if(Has("police.visible"))return StoryPhase.Pursuit;
  if(Has("outside.contact"))return StoryPhase.Contact;
  if(Has("rule.charge"))return StoryPhase.RoadChess;
  if(Has("glovebox-authenticated"))return StoryPhase.Exploration;
  if(Has("lockdown"))return StoryPhase.Lockdown;
  return StoryPhase.Ride;}}
 // The car speaks: subtitle + browser voice with pitch / rate that follow its mood.
 public void Say(string text,bool car=true){
  if(string.IsNullOrEmpty(text))return;
  game.Dialogue.Say(text);
  if(car&&VoiceOn&&!AudioJam&&!(Roles!=null&&Roles.LocalIsCar))BrowserPlatform.Speak(text,MoodModel.Pitch(Mood),MoodModel.Rate(Mood),1f);
 }
 public void Boost(float mph,float seconds){ThreatBoostMph=Mathf.Max(ThreatBoostMph,mph);boostUntil=Time.time+seconds;}
 public string ClimateReadout()=>Build!=null&&Build.climateReadout!=null?(Climate.Frozen?"CLIMATE\nOFFLINE":Climate.Temp+"°   FAN "+Climate.Fan):(game.Registry.Get("climate").Active?"−     19°     +":"−     21°     +");
 public float RunSeconds=>StoryClock;
 public void SaveRecords(){PlayerPrefs.SetString("cm55.records",Records.ToString());PlayerPrefs.Save();}
 public float TargetMph(float basicMph){
  var v=new VehicleInputs{Hostile=game.Ride.Hostile||DrivesSpeed,Cruise=Modes!=null&&Modes.ForcedMph>0?Modes.ForcedMph:game.Security.CruiseMph,RouteOverride=game.State.Has("security.route")&&!game.Chess.Active,CheckBrake=game.Security.BrakeActive,
   ThreatBoost=ThreatBoostMph,PoliceBrake=PolicePresenter!=null&&PolicePresenter.BrakeActive,PoliceBrakeMph=PolicePresenter!=null?PolicePresenter.BrakeMph:60,Final=Final!=null&&Final.Active,FinalMph=Final!=null?Final.CruiseMph:0,Stopped=Final!=null&&Final.Stopped};
  return VehiclePolicy.TargetMph(v);
 }
}
}
