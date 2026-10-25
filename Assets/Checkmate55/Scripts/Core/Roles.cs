using System;
using System.Collections.Generic;
using System.Linq;
namespace Checkmate55.Core {
// ---------------------------------------------------------------------------------------------------------------------------
// CO-OP: two humans, ONE White side. There is always exactly one authoritative chess action per White team turn.
// ---------------------------------------------------------------------------------------------------------------------------
public enum SupportKind { Card, Cyber }
public sealed class CoopTurn {
 public readonly CoopControl Mode;public readonly int Players;
 public int Turn{get;private set;}                 // 0-based team turn
 public bool ChessLocked{get;private set;}         // the first confirmed legal move locks the turn
 public bool CardUsed{get;private set;}
 public bool CyberUsed{get;private set;}
 public int LockedBy{get;private set;}=-1;
 public CoopTurn(CoopControl mode,int players){Mode=mode;Players=Math.Max(1,Math.Min(2,players));}
 // Who owns the chess move this turn (-1 = anyone, Shared Control).
 public int ChessOperator{get{if(Players<2)return 0;switch(Mode){case CoopControl.Shared:return -1;case CoopControl.Alternating:return Turn%2;default:return 0;}}}
 public int SupportOperator{get{if(Players<2)return 0;return Mode==CoopControl.TacticalAssist?1:-1;}}
 public bool CanPreview(int player)=>Mode==CoopControl.Shared||player==ChessOperator||Players<2;   // teammates may see (and in Shared, select) pieces
 public bool CanConfirmChess(int player){if(ChessLocked||player<0||player>=Players)return false;int op=ChessOperator;return op==-1||op==player;}
 public bool TryConfirmChess(int player){if(!CanConfirmChess(player))return false;ChessLocked=true;LockedBy=player;return true;}
 public bool CanSupport(int player,SupportKind kind){
  if(player<0||player>=Players)return false;
  if(kind==SupportKind.Card&&CardUsed)return false;if(kind==SupportKind.Cyber&&CyberUsed)return false;
  int op=SupportOperator;return op==-1||op==player;
 }
 public bool TrySupport(int player,SupportKind kind){
  if(!CanSupport(player,kind))return false;
  // Tactical Assist: Passenger B has ONE support action per turn (a card OR the team's Cyber interaction).
  if(Mode==CoopControl.TacticalAssist&&Players>1&&(CardUsed||CyberUsed))return false;
  if(kind==SupportKind.Card)CardUsed=true;else CyberUsed=true;return true;
 }
 public void NextTurn(){Turn++;ChessLocked=false;LockedBy=-1;CardUsed=false;CyberUsed=false;}
 public string Banner{get{
  if(Players<2)return "WHITE TURN";
  string who=ChessOperator==-1?"BOTH PASSENGERS":"PASSENGER "+(char)('A'+ChessOperator);
  return "WHITE TEAM TURN  ·  "+who+" MOVES"+(Mode==CoopControl.TacticalAssist?"  ·  B: SUPPORT":"");}}
}
// ---------------------------------------------------------------------------------------------------------------------------
// DRIVERLESS DUEL: the human car pays for everything with SYSTEM AUTHORITY. No god mode.
// ---------------------------------------------------------------------------------------------------------------------------
public enum CarAction { RadioHijack, WindowClose, TintUp, ClimateCut, LockCycle, SeatAdjust, ForcedLaneChange, HazardGenerate, RouteManipulate, ExtremeAcceleration }
public sealed class SystemAuthority {
 public const float Max=100f,RegenPerSecond=3.2f;
 public float Value{get;private set;}=60f;
 float now;readonly Dictionary<CarAction,float> readyAt=new Dictionary<CarAction,float>();
 public static float Cost(CarAction a){switch(a){
  case CarAction.RadioHijack:return 8;case CarAction.WindowClose:return 8;case CarAction.TintUp:return 12;case CarAction.ClimateCut:return 12;case CarAction.LockCycle:return 14;
  case CarAction.SeatAdjust:return 22;case CarAction.ForcedLaneChange:return 24;case CarAction.HazardGenerate:return 34;case CarAction.RouteManipulate:return 48;default:return 42;}}
 public static float Cooldown(CarAction a){switch(a){
  case CarAction.RadioHijack:return 12;case CarAction.WindowClose:return 10;case CarAction.TintUp:return 18;case CarAction.ClimateCut:return 20;case CarAction.LockCycle:return 15;
  case CarAction.SeatAdjust:return 30;case CarAction.ForcedLaneChange:return 22;case CarAction.HazardGenerate:return 28;case CarAction.RouteManipulate:return 60;default:return 50;}}
 public void Tick(float dt){now+=dt;Value=Math.Min(Max,Value+RegenPerSecond*dt);}
 public float CooldownLeft(CarAction a)=>readyAt.TryGetValue(a,out var t)?Math.Max(0,t-now):0;
 public bool CanAfford(CarAction a)=>Value>=Cost(a);
 public string Why(CarAction a){if(CooldownLeft(a)>0)return "cooling down "+(int)Math.Ceiling(CooldownLeft(a))+"s";if(!CanAfford(a))return "needs "+Cost(a)+" authority";return "";}
 public bool Try(CarAction a){if(Why(a).Length>0)return false;Value-=Cost(a);readyAt[a]=now+Cooldown(a);return true;}
 // Everything the car may NOT do, by design (the validator rejects any request that names one of these).
 public static readonly string[] Forbidden={"killPassenger","openRequiredProgression","closeRequiredProgression","deletePiece","illegalChessMove","skipPassengerTurn"};
 public static bool IsAllowedName(string name)=>Enum.GetNames(typeof(CarAction)).Contains(name)&&!Forbidden.Contains(name);
}
// ---------------------------------------------------------------------------------------------------------------------------
// CONTROLLERS: the world only ever sees intents. It cannot tell a human from an AI.
// ---------------------------------------------------------------------------------------------------------------------------
public enum IntentKind { Idle, Interact, Look, ChessAction, ChessCard, ChessCyber, CarAction }
public sealed class Intent {
 public IntentKind Kind;public int Actor;
 public string ControlId="";                                   // Interact
 public string From="",To="",Promotion="",ActionKind="move";   // ChessAction
 public string[] Steps=new string[0];                          // Overdrive: from-to-from-to
 public string Card="",Ability="",Target="",Target2="";        // ChessCard / ChessCyber
 public CarAction Car;                                         // CarAction
 public float Yaw,Pitch;                                       // Look
 public static Intent LookAt(int actor,float yaw,float pitch)=>new Intent{Kind=IntentKind.Look,Actor=actor,Yaw=yaw,Pitch=pitch};
 public static Intent Idle(int actor)=>new Intent{Kind=IntentKind.Idle,Actor=actor};
 public static Intent Touch(int actor,string id)=>new Intent{Kind=IntentKind.Interact,Actor=actor,ControlId=id};
 public override string ToString(){switch(Kind){case IntentKind.Interact:return "touch "+ControlId;case IntentKind.ChessAction:return ActionKind+" "+From+To;case IntentKind.ChessCard:return "card "+Card+" "+Target;case IntentKind.ChessCyber:return "cyber "+Ability+" "+Target;case IntentKind.CarAction:return "car "+Car;default:return "idle";}}
}
// Only what a seated passenger could perceive: the guide ring, the objective line, items in hand, notes they read, the board.
public sealed class PassengerView {
 public string Objective="",TargetControl="",Dialogue="",BeatId="",Hint="";
 // what the dashboard, climate display, radio display and glass show a seated passenger
 public int Temp=21,Fan=1,Seat=1;public bool PatternReady;
 public int Station=1;public float Balance,Fader,Volume=.5f;public bool Muted;
 public float TintAlpha,Yaw,Pitch;
 public readonly List<string> Held=new List<string>(),Notes=new List<string>();
 public string Fen="";public bool ChessActive,MyChessTurn;
 public string[] ControlsInReach=new string[0];
 public bool Locked(string id)=>LockedControls.Contains(id);
 public readonly HashSet<string> LockedControls=new HashSet<string>();
 public float Time;
}
public sealed class CarView {
 public bool PassengerOnBoard=true;public string Fen="";public bool ChessActive,MyChessTurn;public float Authority;
 public readonly List<string> Alerts=new List<string>();     // "UNAUTHORIZED ACTIVITY DETECTED: REAR RIGHT" - no detail about what was found
 public int PassengerActivityCount;
}
public interface IPassengerController {ControllerKind Kind{get;}Intent Decide(PassengerView view,float dt);}
public interface ICarController {ControllerKind Kind{get;}Intent Decide(CarView view,float dt);}
// Anything that can answer chess questions (the engine in the browser, a stub in tests).
public interface IChessOracle {
 // Returns the best intent for `actor`, or null when it is not their turn.
 Intent BestChessIntent(int actor,string fen,string level);
}
// A human: intents are queued by input events (taps, clicks, network messages) and drained one per decision.
public sealed class HumanPassengerController:IPassengerController {
 readonly Queue<Intent> queue=new Queue<Intent>();readonly int actor;
 public HumanPassengerController(int actor){this.actor=actor;}
 public ControllerKind Kind=>ControllerKind.Human;
 public void Enqueue(Intent i){i.Actor=actor;queue.Enqueue(i);}
 public Intent Decide(PassengerView view,float dt)=>queue.Count>0?queue.Dequeue():Intent.Idle(actor);
}
public sealed class HumanCarController:ICarController {
 readonly Queue<Intent> queue=new Queue<Intent>();
 public ControllerKind Kind=>ControllerKind.Human;
 public void Enqueue(Intent i){queue.Enqueue(i);}
 public Intent Decide(CarView view,float dt)=>queue.Count>0?queue.Dequeue():Intent.Idle(-1);
}
// An AI passenger gets exactly the information a human passenger gets (PassengerView): the guide ring, the objective, what it holds.
// It cannot look at flags, the engine state of the opponent, or the car's controller. Skill changes reasoning quality, not information.
public sealed class AIPassengerController:IPassengerController {
 readonly int actor;readonly CarSkill skill;readonly IChessOracle oracle;readonly Rng rng;readonly StoryAgent agent=new StoryAgent();float cooldown;readonly HashSet<string> failed=new HashSet<string>();int repeats;string lastTarget="";
 public AIPassengerController(int actor,CarSkill skill,IChessOracle oracle,int seed){this.actor=actor;this.skill=skill;this.oracle=oracle;rng=new Rng(seed);}
 public ControllerKind Kind=>ControllerKind.Ai;
 float Delay{get{switch(skill){case CarSkill.Casual:return 2.6f;case CarSkill.Smart:return 1.6f;case CarSkill.Expert:return .9f;default:return .5f;}}}
 float Mistake{get{switch(skill){case CarSkill.Casual:return .25f;case CarSkill.Smart:return .1f;case CarSkill.Expert:return .03f;default:return 0f;}}}
 public Intent Decide(PassengerView v,float dt){
  cooldown-=dt;if(cooldown>0)return Intent.Idle(actor);
  cooldown=Delay*rng.Range(.8f,1.3f);
  if(v.ChessActive){
   if(!v.MyChessTurn)return Intent.Idle(actor);
   var c=oracle?.BestChessIntent(actor,v.Fen,skill.ToString().ToLowerInvariant());
   if(c!=null)return c;return Intent.Idle(actor);
  }
  if(rng.Chance(Mistake)&&v.ControlsInReach.Length>0)return Intent.Touch(actor,v.ControlsInReach[rng.Range(0,v.ControlsInReach.Length)]);
  var step=agent.Next(v,actor);
  if(step!=null){cooldown=Math.Max(cooldown,agent.Pace);return step;}
  if(string.IsNullOrEmpty(v.TargetControl))return Intent.Idle(actor);
  // follow the same guide ring a human sees; occasionally touch something nearby instead (casual players fumble)
  if(v.TargetControl==lastTarget){repeats++;}else{repeats=0;lastTarget=v.TargetControl;}
  if(rng.Chance(Mistake)&&v.ControlsInReach.Length>0)return Intent.Touch(actor,v.ControlsInReach[rng.Range(0,v.ControlsInReach.Length)]);
  if(repeats>3){repeats=0;failed.Add(v.TargetControl);}
  return Intent.Touch(actor,v.TargetControl);
 }
}
public sealed class AICarController:ICarController {
 readonly CarSkill skill;readonly Rng rng;readonly SystemAuthority authority;float cooldown;
 public AICarController(CarSkill skill,SystemAuthority authority,int seed){this.skill=skill;this.authority=authority;rng=new Rng(seed);}
 public ControllerKind Kind=>ControllerKind.Ai;
 public Intent Decide(CarView v,float dt){
  cooldown-=dt;if(cooldown>0)return Intent.Idle(-1);
  cooldown=(skill==CarSkill.Relentless?5f:skill==CarSkill.Expert?8f:skill==CarSkill.Smart?12f:18f)*rng.Range(.8f,1.3f);
  // the AI car pays for its actions exactly like a human car: it uses the same SystemAuthority
  var options=new[]{CarAction.RadioHijack,CarAction.WindowClose,CarAction.TintUp,CarAction.SeatAdjust,CarAction.ForcedLaneChange,CarAction.HazardGenerate,CarAction.ExtremeAcceleration}.Where(a=>authority==null||authority.Why(a).Length==0).ToArray();
  if(options.Length==0)return Intent.Idle(-1);
  return new Intent{Kind=IntentKind.CarAction,Actor=-1,Car=options[rng.Range(0,options.Length)]};
 }
}
}
