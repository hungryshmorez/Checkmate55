using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Checkmate55.Core {
public enum GameMode { Story, Calibration, PassengerChess, RoadChess, CoopEscape, DriverlessDuel, CarMaster, AiSimulation }
public enum Difficulty { Easy, Normal, Hard, Brutal }
public enum CarSkill { Casual, Smart, Expert, Relentless }
public enum RuleSet { Standard, RoadChess, CyberRoadChess }
public enum ControllerKind { Human, Ai }
public enum CoopControl { Shared, Alternating, TacticalAssist }

public sealed class ModeConfig {
 public GameMode Mode;public string Title,Blurb;
 public bool Hostile;            // the car works against the passenger
 public bool ImpactTimer;        // Story only: the 3:00 collision course at the end
 public bool EscapePressure;     // police / threats / locks
 public bool ChessOnly;          // standalone chess in the car (no escape-room objectives)
 public ControllerKind Passenger=ControllerKind.Human,Car=ControllerKind.Ai;
 public int Humans=1;
 public RuleSet Rules=RuleSet.Standard;
 public Difficulty Difficulty=Difficulty.Normal;
 public CarSkill Skill=CarSkill.Smart;
 public CoopControl Coop=CoopControl.TacticalAssist;
 public bool AllRulesKnown;      // non-Story modes teach every public rule up front
 public bool Networked;
 public string AiLevel{get{return Passenger==ControllerKind.Ai&&Car==ControllerKind.Ai?SkillLevel(Skill):Mode==GameMode.CarMaster?SkillLevel(Skill):Difficulty.ToString().ToLowerInvariant();}}
 public static string SkillLevel(CarSkill s){switch(s){case CarSkill.Casual:return "casual";case CarSkill.Smart:return "smart";case CarSkill.Expert:return "expert";default:return "relentless";}}
 public bool Cyber=>Rules==RuleSet.CyberRoadChess;
 public bool Cards=>Rules==RuleSet.CyberRoadChess;
 public bool Road=>Rules!=RuleSet.Standard;
}
public static class ModeCatalog {
 public static readonly GameMode[] Menu={GameMode.Story,GameMode.Calibration,GameMode.PassengerChess,GameMode.RoadChess,GameMode.CoopEscape,GameMode.DriverlessDuel,GameMode.CarMaster,GameMode.AiSimulation};
 public static ModeConfig For(GameMode m){
  switch(m){
   case GameMode.Story:return new ModeConfig{Mode=m,Title="STORY: OVERRIDE",Blurb="Human passenger vs AI car. Escape in about 30 minutes.",Hostile=true,ImpactTimer=true,EscapePressure=true,Rules=RuleSet.CyberRoadChess};
   case GameMode.Calibration:return new ModeConfig{Mode=m,Title="CALIBRATION RIDE",Blurb="The 8-10 minute passenger training program. No danger.",Hostile=false,AllRulesKnown=true,Rules=RuleSet.CyberRoadChess};
   case GameMode.PassengerChess:return new ModeConfig{Mode=m,Title="PASSENGER CHESS",Blurb="A relaxed endless daytime ride. Chess in your lap.",Hostile=false,ChessOnly=true,AllRulesKnown=true,Rules=RuleSet.Standard};
   case GameMode.RoadChess:return new ModeConfig{Mode=m,Title="ROAD CHESS",Blurb="The vehicle's own rules, as an arcade game.",Hostile=false,ChessOnly=true,AllRulesKnown=true,Rules=RuleSet.CyberRoadChess};
   case GameMode.CoopEscape:return new ModeConfig{Mode=m,Title="CO-OP ESCAPE",Blurb="Two passengers, one White side.",Hostile=true,ImpactTimer=true,EscapePressure=true,Humans=2,Networked=true,Rules=RuleSet.CyberRoadChess};
   case GameMode.DriverlessDuel:return new ModeConfig{Mode=m,Title="DRIVERLESS DUEL",Blurb="Human passenger vs human car.",Hostile=true,EscapePressure=true,Humans=2,Car=ControllerKind.Human,Networked=true,Rules=RuleSet.CyberRoadChess,AllRulesKnown=true};
   case GameMode.CarMaster:return new ModeConfig{Mode=m,Title="CAR MASTER",Blurb="Be the car. An AI passenger tries to escape.",Hostile=true,EscapePressure=true,Passenger=ControllerKind.Ai,Car=ControllerKind.Human,Rules=RuleSet.CyberRoadChess,AllRulesKnown=true};
   default:return new ModeConfig{Mode=m,Title="AI SIMULATION",Blurb="AI passenger vs AI car. Watch or test.",Hostile=true,Passenger=ControllerKind.Ai,Car=ControllerKind.Ai,Humans=0,Rules=RuleSet.CyberRoadChess,AllRulesKnown=true};
  }
 }
 public static string[] RuleNames={"STANDARD","ROAD CHESS","CYBER ROAD CHESS"};
}

// Persistent progress. Plain text so it survives PlayerPrefs, files and the network equally.
public sealed class Records {
 public int BestStoryMs=-1,BestFinalMs=-1,Escapes,PreferredPreset=1;
 public int EndingsMask,SecretRulesMask;
 public bool CalibrationDone;
 public readonly Dictionary<string,int[]> Results=new Dictionary<string,int[]>();   // mode -> [wins, losses, draws]
 public bool Seen(Ending e)=>(EndingsMask&(1<<(int)e))!=0;
 public int EndingCount{get{int n=0;foreach(Ending e in Enum.GetValues(typeof(Ending)))if(e!=Ending.None&&Seen(e))n++;return n;}}
 public bool NewBestStory(int ms){if(ms<=0)return false;if(BestStoryMs<0||ms<BestStoryMs){BestStoryMs=ms;return true;}return false;}
 public bool NewBestFinal(int ms){if(ms<=0)return false;if(BestFinalMs<0||ms<BestFinalMs){BestFinalMs=ms;return true;}return false;}
 public void RecordEnding(Ending e,int storyMs,int finalMs){
  if(e==Ending.None)return;EndingsMask|=1<<(int)e;
  if(e!=Ending.Destination){Escapes++;NewBestStory(storyMs);NewBestFinal(finalMs);}
 }
 public void RecordResult(GameMode mode,string outcome){var k=mode.ToString();if(!Results.TryGetValue(k,out var r))Results[k]=r=new int[3];if(outcome=="win")r[0]++;else if(outcome=="loss")r[1]++;else r[2]++;}
 public int[] ResultsFor(GameMode m)=>Results.TryGetValue(m.ToString(),out var r)?r:new int[3];
 public override string ToString(){
  var sb=new StringBuilder();
  sb.Append("v=1;bs="+BestStoryMs+";bf="+BestFinalMs+";es="+Escapes+";pp="+PreferredPreset+";em="+EndingsMask+";sr="+SecretRulesMask+";cal="+(CalibrationDone?1:0));
  foreach(var kv in Results.OrderBy(k=>k.Key))sb.Append(";r."+kv.Key+"="+kv.Value[0]+","+kv.Value[1]+","+kv.Value[2]);
  return sb.ToString();
 }
 public static Records Parse(string text){
  var r=new Records();if(string.IsNullOrEmpty(text))return r;
  foreach(var part in text.Split(';')){
   int eq=part.IndexOf('=');if(eq<0)continue;string k=part.Substring(0,eq),v=part.Substring(eq+1);int n;
   switch(k){
    case "bs":if(int.TryParse(v,out n))r.BestStoryMs=n;break;case "bf":if(int.TryParse(v,out n))r.BestFinalMs=n;break;case "es":if(int.TryParse(v,out n))r.Escapes=n;break;
    case "pp":if(int.TryParse(v,out n)&&n>=1&&n<=RadioModel.Presets)r.PreferredPreset=n;break;case "em":if(int.TryParse(v,out n))r.EndingsMask=n;break;case "sr":if(int.TryParse(v,out n))r.SecretRulesMask=n;break;
    case "cal":r.CalibrationDone=v=="1";break;
    default:
     if(k.StartsWith("r.")){var p=v.Split(',');var a=new int[3];for(int i=0;i<3&&i<p.Length;i++)int.TryParse(p[i],out a[i]);r.Results[k.Substring(2)]=a;}
     break;
   }
  }
  return r;
 }
 public static string Clock(int ms){if(ms<0)return "--:--";int s=ms/1000;return (s/60).ToString("00")+":"+(s%60).ToString("00");}
}

public enum Ending { None, Checkmate, Cheater, PreviousPassenger, SystemEscape, Destination }
public struct EndingFacts {
 public string ChessWinner,ChessReason;          // from the final board: "w" | "b" | "" ; checkmate | timeout | ...
 public bool ImpactReached,UsedHiddenRule,EscapeSequenceComplete,PoliceForcedStop,CarCollapsed;
 public int FreedSystems;
}
public static class EndingRules {
 public const int SystemsNeeded=4;
 public static string Title(Ending e){switch(e){
  case Ending.Checkmate:return "AUTONOMOUS CONTROL OFFLINE.";case Ending.Cheater:return "SYSTEM COLLAPSE.";case Ending.PreviousPassenger:return "ROUTE INTERCEPTED.";
  case Ending.SystemEscape:return "ROUTE INTEGRITY LOST.";case Ending.Destination:return "DESTINATION REACHED.";default:return "";}}
 public static string Epilogue(Ending e){switch(e){
  case Ending.Checkmate:return "Silence. Emergency braking. CLICK. The doors unlock.";
  case Ending.Cheater:return "It panics. It repeats a rule that does not exist. The car stops by itself.";
  case Ending.PreviousPassenger:return "You finish what Passenger 04 could not. The service lane opens. Somebody is waiting.";
  case Ending.SystemEscape:return "Every system the car took, you took back. The police do the rest.";
  case Ending.Destination:return "The windscreen fills with white. No gore. Only the sound stopping.";default:return "";}}
 // Which ending the facts describe. Escapes take priority over the loss; the most specific escape wins.
 public static Ending Resolve(EndingFacts f){
  if(f.EscapeSequenceComplete)return Ending.PreviousPassenger;
  if(f.CarCollapsed||(f.ChessWinner=="w"&&f.UsedHiddenRule))return Ending.Cheater;
  if(f.ChessWinner=="w")return Ending.Checkmate;
  if(f.PoliceForcedStop&&f.FreedSystems>=SystemsNeeded)return Ending.SystemEscape;
  if(f.ChessWinner=="b"||f.ImpactReached)return Ending.Destination;
  return Ending.None;
 }
 public static bool IsEscape(Ending e)=>e!=Ending.None&&e!=Ending.Destination;
}
}
