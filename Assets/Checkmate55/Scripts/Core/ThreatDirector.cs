using System;
using System.Collections.Generic;
using System.Linq;
namespace Checkmate55.Core {
public enum StoryPhase { Ride, Lockdown, Exploration, RoadChess, Contact, Pursuit, Final, Ended }
public enum ThreatKind { None, RadioChange, VolumeUp, VolumeDown, SpeakerRoute, FavoriteRemoved, WindowClose, TintUp, ClimateShutdown, ClimateReset, SeatCreep, LockCycle, Accelerate, LaneChange, WarningDisplay, NavChange, SunshadeClose, BeltTighten, PuzzleInterference, JamAudio, Dialogue }
public sealed class ThreatAction {
 public ThreatKind Kind;public string Arg="",Line="",Reason="";public float Strength=1;public bool Reaction;
 public override string ToString()=>Kind+(Arg.Length>0?"("+Arg+")":"")+(Reaction?" [reaction]":"");
}
public sealed class ThreatContext {
 public StoryPhase Phase;public bool Hostile,ChessActive,PuzzleBusy;public float PoliceDistance=-1;public int OpenContests;public CarMood Mood;
}
// Decides WHEN and WHAT the car does to the passenger. Never random spam: intensity rises and falls, every kind has a
// cooldown, there is a global gap and a per-minute budget, and most actions are reactions to something the player just did.
public sealed class ThreatDirector {
 sealed class Pending {public float Due;public ThreatAction Action;}
 readonly Rng rng;readonly Dictionary<ThreatKind,float> until=new Dictionary<ThreatKind,float>();
 readonly List<Pending> queue=new List<Pending>();readonly List<float> proactive=new List<float>();
 readonly Dictionary<string,List<float>> counters=new Dictionary<string,List<float>>();
 float time,nextAllowed,spike;StoryPhase phase=StoryPhase.Ride;ThreatKind last=ThreatKind.None;
 public float Intensity{get;private set;}
 public float Fear{get;private set;}
 public int Issued{get;private set;}
 public float Time=>time;
 public event Action<ThreatAction> Issue;
 public ThreatDirector(int seed){rng=new Rng(seed);}
 public static float BaseIntensity(StoryPhase p){switch(p){case StoryPhase.Lockdown:return .2f;case StoryPhase.Exploration:return .35f;case StoryPhase.RoadChess:return .5f;case StoryPhase.Contact:return .6f;case StoryPhase.Pursuit:return .8f;case StoryPhase.Final:return .9f;default:return 0;}}
 static float Cooldown(ThreatKind k){switch(k){
  case ThreatKind.RadioChange:return 45;case ThreatKind.VolumeUp:case ThreatKind.VolumeDown:return 30;case ThreatKind.SpeakerRoute:return 70;case ThreatKind.FavoriteRemoved:return 120;
  case ThreatKind.WindowClose:return 40;case ThreatKind.TintUp:return 60;case ThreatKind.ClimateShutdown:return 75;case ThreatKind.ClimateReset:return 40;case ThreatKind.SeatCreep:return 90;
  case ThreatKind.LockCycle:return 50;case ThreatKind.Accelerate:return 45;case ThreatKind.LaneChange:return 35;case ThreatKind.WarningDisplay:return 30;case ThreatKind.NavChange:return 80;
  case ThreatKind.SunshadeClose:return 80;case ThreatKind.BeltTighten:return 100;case ThreatKind.PuzzleInterference:return 30;case ThreatKind.JamAudio:return 40;default:return 25;}}
 // Systems the passenger can win back by pressing the same control repeatedly (see ContestBoard).
 public static string ContestSystem(ThreatKind k){switch(k){case ThreatKind.WindowClose:return "window";case ThreatKind.SeatCreep:return "seat";case ThreatKind.ClimateShutdown:return "climate";case ThreatKind.TintUp:return "tint";case ThreatKind.JamAudio:return "audio";case ThreatKind.SpeakerRoute:case ThreatKind.RadioChange:return "radio";default:return "";}}
 public static string LineContext(ThreatKind k){switch(k){case ThreatKind.WindowClose:return "window-open";case ThreatKind.TintUp:case ThreatKind.SunshadeClose:return "tint";case ThreatKind.ClimateShutdown:case ThreatKind.ClimateReset:return "temp";
  case ThreatKind.RadioChange:case ThreatKind.SpeakerRoute:case ThreatKind.FavoriteRemoved:case ThreatKind.JamAudio:return "radio";case ThreatKind.VolumeUp:case ThreatKind.VolumeDown:return "volume";case ThreatKind.SeatCreep:case ThreatKind.BeltTighten:return "seat";
  case ThreatKind.LockCycle:return "door";case ThreatKind.Accelerate:return "accelerate";default:return "";}}
 static (ThreatKind,float)[] Pool(StoryPhase p){switch(p){
  case StoryPhase.Lockdown:case StoryPhase.Exploration:return new[]{(ThreatKind.SeatCreep,1f),(ThreatKind.TintUp,1f),(ThreatKind.RadioChange,1.5f),(ThreatKind.VolumeUp,1f),(ThreatKind.WindowClose,1f),(ThreatKind.ClimateShutdown,1f),(ThreatKind.SunshadeClose,.5f),(ThreatKind.LockCycle,.7f),(ThreatKind.WarningDisplay,1f)};
  case StoryPhase.RoadChess:return new[]{(ThreatKind.PuzzleInterference,2f),(ThreatKind.NavChange,1f),(ThreatKind.Accelerate,1.2f),(ThreatKind.TintUp,1f),(ThreatKind.RadioChange,1f),(ThreatKind.SeatCreep,.8f),(ThreatKind.LaneChange,1f),(ThreatKind.WarningDisplay,.8f)};
  case StoryPhase.Contact:case StoryPhase.Pursuit:return new[]{(ThreatKind.JamAudio,2f),(ThreatKind.TintUp,2f),(ThreatKind.Accelerate,2f),(ThreatKind.LaneChange,1.5f),(ThreatKind.NavChange,1f),(ThreatKind.WindowClose,1f),(ThreatKind.BeltTighten,.8f),(ThreatKind.SeatCreep,.8f),(ThreatKind.ClimateShutdown,.6f)};
  case StoryPhase.Final:return new[]{(ThreatKind.PuzzleInterference,2.5f),(ThreatKind.RadioChange,1f),(ThreatKind.SeatCreep,1f),(ThreatKind.Accelerate,1.2f),(ThreatKind.JamAudio,1f),(ThreatKind.LockCycle,.6f),(ThreatKind.TintUp,.8f)};
  default:return new (ThreatKind,float)[0];}}
 public void SetPhase(StoryPhase p){if(p==phase)return;phase=p;nextAllowed=Math.Max(nextAllowed,time+12);}
 public StoryPhase Phase=>phase;
 bool Ready(ThreatKind k){float c;if(!until.TryGetValue(k,out c))return true;return time>=c;}
 void Stamp(ThreatKind k,bool reaction){float cd=Cooldown(k)*(1.35f-.5f*Intensity);if(reaction)cd=Math.Min(cd,14);until[k]=time+cd;}
 void Queue(ThreatKind k,float delay,string arg,string reason,float strength=1){queue.Add(new Pending{Due=time+delay,Action=new ThreatAction{Kind=k,Arg=arg,Reason=reason,Strength=strength,Reaction=true}});}
 // ---- inputs from the world
 public void OnPlayer(string what,bool hostile,CarMood mood){
  if(what=="window.open"&&hostile)Queue(ThreatKind.WindowClose,rng.Range(3.5f,8f),"","player opened a window");
  else if(what=="climate.change"&&mood>=CarMood.Controlling)Queue(ThreatKind.ClimateReset,rng.Range(4f,7f),"","player changed the temperature");
  else if(what=="radio.station"&&hostile)Queue(ThreatKind.RadioChange,rng.Range(5f,11f),"","player picked a station");
  else if((what=="radio.volume.down"||what=="radio.mute")&&hostile)Queue(ThreatKind.VolumeUp,rng.Range(2.5f,6f),"","player lowered the volume");
  else if(what=="door.try"&&hostile){
   var l=Counter("door");l.Add(time);l.RemoveAll(t=>time-t>20);
   if(l.Count>=3){l.Clear();Queue(ThreatKind.LockCycle,.4f,"","repeated door attempts");}
  }
  else if(what=="forbidden"&&hostile){Queue(ThreatKind.SeatCreep,rng.Range(1f,3f),"","forbidden system access");Queue(ThreatKind.TintUp,rng.Range(4f,7f),"","forbidden system access");}
 }
 List<float> Counter(string k){if(!counters.TryGetValue(k,out var l))counters[k]=l=new List<float>();return l;}
 public void OnDiscovery(bool hostile){
  spike=Math.Min(.4f,spike+.25f);
  if(hostile){Queue(ThreatKind.Accelerate,rng.Range(1.5f,3f),"","important discovery",1f);Queue(ThreatKind.TintUp,rng.Range(3.5f,6f),"","important discovery");}
 }
 public void OnCountered(){Fear=Math.Min(1,Fear+.08f);spike=Math.Min(.4f,spike+.05f);}
 public void Force(ThreatAction a){a.Reaction=true;Emit(a);}
 void Emit(ThreatAction a){last=a.Kind;Stamp(a.Kind,a.Reaction);Issued++;Issue?.Invoke(a);}
 // ---- the clock
 public ThreatAction Tick(float dt,ThreatContext ctx){
  time+=dt;
  if(ctx.Phase!=phase)SetPhase(ctx.Phase);
  float breathe=.12f*(float)Math.Sin(time*2*Math.PI/55.0);
  float target=Math.Max(0,Math.Min(1,BaseIntensity(phase)+spike+(BaseIntensity(phase)>0?breathe:0)+Fear*.1f));
  Intensity+=(target-Intensity)*Math.Min(1,dt*.25f);spike=Math.Max(0,spike-dt*.02f);
  proactive.RemoveAll(t=>time-t>60);
  if(!ctx.Hostile||phase==StoryPhase.Ride||phase==StoryPhase.Ended){queue.Clear();return null;}
  // Reactions to something the player just did come first.
  for(int i=0;i<queue.Count;i++){var p=queue[i];
   if(time<p.Due)continue;
   if(ctx.PuzzleBusy&&p.Action.Kind!=ThreatKind.Dialogue&&p.Action.Kind!=ThreatKind.PuzzleInterference){p.Due=time+4;continue;}
   queue.RemoveAt(i);
   if(!Ready(p.Action.Kind))return null;
   if(ctx.ChessActive&&phase!=StoryPhase.Final&&ContestSystem(p.Action.Kind)!=""){return null;}
   p.Action.Line=CarVoice.Line(LineContext(p.Action.Kind),ctx.Mood,rng);Emit(p.Action);return p.Action;
  }
  if(time<nextAllowed)return null;
  int budget=Intensity>.6f?4:3;if(proactive.Count>=budget)return null;
  if(ctx.PuzzleBusy)return null;
  var pool=Pool(phase).Where(e=>e.Item1!=last&&Ready(e.Item1)&&!(ContestSystem(e.Item1)!=""&&ctx.OpenContests>=2)&&!(ctx.ChessActive&&phase!=StoryPhase.Final&&e.Item1!=ThreatKind.PuzzleInterference)).ToList();
  if(pool.Count==0){nextAllowed=time+3;return null;}
  float total=pool.Sum(e=>e.Item2),r=rng.Next()*total;ThreatKind pick=pool[pool.Count-1].Item1;
  foreach(var e in pool){r-=e.Item2;if(r<=0){pick=e.Item1;break;}}
  var action=new ThreatAction{Kind=pick,Strength=.6f+.4f*Intensity,Reason="intensity "+Intensity.ToString("0.00"),Line=CarVoice.Line(LineContext(pick),ctx.Mood,rng)};
  float gap=(34f-(34f-11f)*Intensity)*rng.Range(.8f,1.2f);nextAllowed=time+gap;proactive.Add(time);Emit(action);return action;
 }
}
}
