using System;
using System.Collections.Generic;
using System.Linq;
namespace Checkmate55.Core {
// Everything the passenger has read, in the order they found it. A human reads it in the pause menu; an AI passenger gets the same text.
public sealed class ClueBook {
 public sealed class Clue {public string Id,Title,Text;}
 readonly List<Clue> clues=new List<Clue>();
 public IReadOnlyList<Clue> All=>clues;
 public event Action<Clue> Added;
 public bool Add(string id,string title,string text){if(clues.Any(c=>c.Id==id))return false;var c=new Clue{Id=id,Title=title,Text=text};clues.Add(c);Added?.Invoke(c);return true;}
 public bool Has(string id)=>clues.Any(c=>c.Id==id);
 public void Clear()=>clues.Clear();
}
// Passenger 04's unfinished escape. Four physical steps, done during the collision course, in any order. The fourth (REQUEST STOP x3 then hold)
// is the one they never got to try. Completing all four reroutes the car into the service lane.
public sealed class EscapeSequence {
 public static readonly string[] Steps={"escape.card","escape.fan","escape.lights","escape.stop"};
 public static readonly string[] Names={"MAINTENANCE CARD IN THE DOCK","FAN TO MAXIMUM","OVERHEAD LIGHT: TWO · THREE","REQUEST STOP ×3, THEN HOLD"};
 readonly HashSet<string> done=new HashSet<string>();
 public bool Active;
 public event Action<string> StepDone;
 public event Action Complete;
 public int Count=>done.Count;
 public bool Finished=>done.Count==Steps.Length;
 public bool Has(string step)=>done.Contains(step);
 public bool Mark(string step){if(!Active||!Steps.Contains(step)||!done.Add(step))return false;StepDone?.Invoke(step);if(Finished)Complete?.Invoke();return true;}
 // "REQUEST STOP" three taps inside four seconds, then a deliberate wait (the hold)
 readonly List<float> stops=new List<float>();
 public bool StopTap(float now){stops.Add(now);stops.RemoveAll(t=>now-t>4f);return stops.Count>=3;}
 public void Reset(){done.Clear();stops.Clear();Active=false;}
 public string Checklist(){return string.Join("\n",Steps.Select((s,i)=>(done.Contains(s)?"✔ ":"□ ")+Names[i]));}
}
// Authored midgame positions for the collision course. White (the passenger) always starts to move with real chances, never a lost game.
public static class FinalPositions {
 public static readonly string[] Fens={
  "r1bq1rk1/ppp2ppp/2n2n2/3pp3/1bB1P3/2NP1N2/PPP2PPP/R1BQ1RK1 w - - 0 7",
  "r1bq1rk1/pp2bppp/2n1pn2/2pp4/3P1B2/2PBPN2/PP1N1PPP/R2QK2R w KQ - 0 9",
  "r1bqk2r/pp2bppp/2n1pn2/2pp4/3P1B2/2P1PN2/PP1NBPPP/R2QK2R w KQkq - 0 8",
  "2rq1rk1/pp1bbppp/2n1pn2/3p4/2PP4/2N1PN2/PPQBBPPP/R4RK1 w - - 0 11",
  "r1b2rk1/pp2qppp/2nbpn2/3p4/3P1B2/2NBPN2/PPQ2PPP/R4RK1 w - - 0 11",
  "r1bqr1k1/ppp2ppp/2n2n2/3pp1B1/1b1PP3/2N2N2/PPPQ1PPP/2KR1B1R w - - 0 9",
  "r1bq1rk1/pp3ppp/2nbpn2/2pp4/3P1B2/2PBPN2/PP1N1PPP/R2QK2R w KQ - 0 9",
  "r3k2r/pp1bqppp/2nbpn2/3p4/3P1B2/2NBPN2/PPQ2PPP/R3K2R w KQkq - 0 10",
 };
 public static string Pick(int seed)=>Fens[Math.Abs(seed)%Fens.Length];
 // Hidden-rule tests: the secret rules only matter when the matching piece is on the board.
 public const string Calibration1="6k1/5ppp/8/8/8/8/1Q6/6K1 w - - 0 1";     // standard: queen mates on b8
 public const string CalibrationRoad="4k3/5ppp/8/8/8/2P5/1N6/4K3 w - - 0 1";   // Road Jump to f3 area, Lane Shift pawn
 public const string CalibrationCyber="4k3/ppp5/8/8/8/8/PPPB4/R3K3 w - - 0 1";  // Firewall / EMP / Overdrive demonstration
 public const string CalibrationCards="4k3/8/8/8/8/3P4/8/4K3 w - - 0 1";
}
}
