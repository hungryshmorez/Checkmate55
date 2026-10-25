using System;
namespace Checkmate55.Core {
// The RIDE GAME: one full game of chess against the car, played across the whole ride (not just at security checkpoints).
// It lives on the console tray: open it whenever you like, make a move, retract it and go back to the puzzles. The car does not wait -
// once the lockdown starts a move clock runs, and if you leave it too long the car plays a weak move FOR you. The route will not end
// (the collision course will not start) until you have played a minimum number of your own moves. Win the game and the car loses
// control (CHECKMATE ending); lose it and the ride ends (DESTINATION).
public sealed class RideGameModel {
 public const int Quota=16;
 public const string StartFen="rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
 public enum Nag { None, Reminder, Warning, Forced }
 public int PlayerMoves{get;private set;}
 public int AutoMoves{get;private set;}
 public bool Over{get;private set;}
 public string Winner{get;private set;}="";
 public float Waiting{get;private set;}
 int stage;
 public bool QuotaMet=>Over||PlayerMoves>=Quota;
 public int Remaining=>Math.Max(0,Quota-PlayerMoves);
 // Seconds the passenger may leave the move alone, by story phase. 0 = the clock is stopped (opening ride, the final, the ending).
 public static float Interval(StoryPhase p){switch(p){
  case StoryPhase.Lockdown:return 240;case StoryPhase.Exploration:return 200;case StoryPhase.RoadChess:return 170;case StoryPhase.Contact:return 150;case StoryPhase.Pursuit:return 130;
  default:return 0;}}
 public float TimeLeft(StoryPhase p){float iv=Interval(p);return iv<=0?0:Math.Max(0,iv-Waiting);}
 public Nag Tick(float dt,StoryPhase phase,bool clockRuns){
  float iv=Interval(phase);
  if(Over||!clockRuns||iv<=0)return Nag.None;
  Waiting+=dt;
  if(Waiting>=iv){Waiting=0;stage=0;return Nag.Forced;}
  if(Waiting>=iv*.9f&&stage<2){stage=2;return Nag.Warning;}
  if(Waiting>=iv*.6f&&stage<1){stage=1;return Nag.Reminder;}
  return Nag.None;
 }
 public void OnPlayerMove(){PlayerMoves++;Waiting=0;stage=0;}
 public void OnAutoMove(){AutoMoves++;Waiting=0;stage=0;}
 public void OnOver(string winner){Over=true;Winner=winner??"";}
 public void Reset(){PlayerMoves=0;AutoMoves=0;Over=false;Winner="";Waiting=0;stage=0;}
 // ---- the finale rewards a good ride game
 public static int MaterialDiff(string fen){
  if(string.IsNullOrEmpty(fen))return 0;int diff=0;
  foreach(char c in fen){if(c==' ')break;int v=Value(char.ToLowerInvariant(c));if(v==0)continue;diff+=char.IsUpper(c)?v:-v;}
  return diff;
 }
 static int Value(char c){switch(c){case 'p':return 1;case 'n':case 'b':return 3;case 'r':return 5;case 'q':return 9;default:return 0;}}
 // Cyber charges added to the collision-course board: +1 for a clear pawn-and-a-bit lead, up to +3 for a crushing one.
 public static int BonusCharges(int materialDiff)=>materialDiff>=9?3:materialDiff>=6?2:materialDiff>=3?1:0;
 public static string Clock(float seconds){int s=(int)Math.Ceiling(Math.Max(0,seconds));return (s/60)+":"+(s%60).ToString("00");}
}
}
