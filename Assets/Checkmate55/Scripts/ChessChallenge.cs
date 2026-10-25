using System;
using UnityEngine;
namespace Checkmate55 {
public enum ChessObjective { DeliverCheck, Checkmate, CaptureTarget, SurviveMoves, SacrificePiece, MoveSequence, RoadMove, UseCyber, PlayCard }
[CreateAssetMenu(menuName="CHECKMATE 55/Chess security challenge")]
public sealed class ChessChallenge : ScriptableObject {
 public string id="passenger-handshake",title="PASSENGER AUTHENTICATION";
 public string FEN="4k3/pp6/8/8/8/8/6KP/R7 w - - 0 1",playerColor="w";
 public ChessObjective objectiveType=ChessObjective.DeliverCheck;
 public int maxMoves=3;
 public bool road;public string targetPiece="",hintMove="a1e1",hintText="A check forces the car to brake.",resultText="STORAGE LATCH RELEASED";
 public string successCondition="check",failCondition="move-budget-or-draw";
 [TextArea] public string guidanceText="Put the black king in check within 3 white moves.";
 public string rewardEvent="CHESS_CHALLENGE_COMPLETED";
 // ---- OVERRIDE additions. Every default reproduces the original foundation behaviour, so existing assets are unchanged.
 public bool laneFollowsMove=true;        // original Road Security: the car drifts into the lane of the passenger's last move
 public bool checkBrakes=true;            // original: any White check makes the car slow down
 public string opponent="scripted";       // scripted | easy | normal | hard | brutal  (scripted = original deterministic replies)
 public bool cyber,cards,hazards,carCyber,carCards,autoVehicle,reshuffle;
 public int startHand=1,seed=55,startCharges;
 public string startHazards="";          // comma-separated squares that start in WARNING (tutorial boards)
 public bool noRetry;                     // endgame boards (the final collision course) end the run instead of offering a retry
 public float timeLimitSeconds;           // 0 = untimed (Story final uses 180)
 [TextArea] public string pressureJson=""; // authored route pressure, e.g. [{"on":"check","do":"slow","mph":15}]
 public bool persistent;                  // the RIDE GAME: retracting the tray keeps the position and the next deploy resumes it
 [NonSerialized] public string savedFen="",savedState="";
 public string[] fenPool;                 // optional: one is chosen per run (replay variety)
 public int ChosenIndex(int runSeed)=>fenPool==null||fenPool.Length==0?-1:Mathf.Abs(runSeed*7919+seed)%fenPool.Length;
 public string FenFor(int runSeed){int i=ChosenIndex(runSeed);return i<0?FEN:fenPool[i];}
 public bool Success(ChessSnapshot board,int moves){switch(objectiveType){case ChessObjective.DeliverCheck:return board.check;case ChessObjective.Checkmate:return board.mate&&board.winner==playerColor;case ChessObjective.SurviveMoves:return moves>=maxMoves;default:return false;}}
 public bool Failure(ChessSnapshot board,int moves)=>board.draw||(maxMoves>0&&moves>=maxMoves)||(objectiveType==ChessObjective.Checkmate&&board.over&&board.winner!=playerColor);
 public string ConfigJson(){
  string pressure=string.IsNullOrWhiteSpace(pressureJson)?"[]":pressureJson.Trim();
  return "{\"road\":"+B(road)+",\"cyber\":"+B(cyber)+",\"cards\":"+B(cards)+",\"hazards\":"+B(hazards)+",\"carCyber\":"+B(carCyber)+",\"carCards\":"+B(carCards)+",\"autoVehicle\":"+B(autoVehicle)+",\"reshuffle\":"+B(reshuffle)+",\"startHand\":"+startHand+",\"startCharges\":"+startCharges+",\"pressure\":"+pressure+"}";
 }
 static string B(bool v)=>v?"true":"false";
}
}
