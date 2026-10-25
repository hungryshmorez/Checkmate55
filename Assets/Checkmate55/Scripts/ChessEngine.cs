using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace Checkmate55 {
[Serializable] public class ChessStep {public string from,to,promotion;}
// kind: move | castle | laneShift | roadJump | laneCharge | ghostPawn | kingReversal | pawnBoost | overdrive
[Serializable] public class ChessMove {public string color,from,to,flags,piece,san,captured,promotion,kind;public bool blocked;public ChessStep[] steps;}
[Serializable] public class ChessPiece {public string square,type,color,hazard;public bool shield,emp;}
[Serializable] public class ChessEvent {public string t,side,a,b,piece,captured,s;public int n;}
// Subset of the engine's plain-JSON state that the presentation layer reads. The full state always travels as ChessSnapshot.stateJson.
[Serializable] public class RoadSideView {public int charges;public bool cardPlayed,cyberUsed;public string[] hand;public string boost,overdrive;public RoadCooldowns cd;}
[Serializable] public class RoadCooldowns {public int firewall,emp,overdrive;}
[Serializable] public class RoadHazardView {public string sq,phase;public int resolveTurn;}
[Serializable] public class RoadShieldView {public string sq;public int ttl;}
[Serializable] public class RoadUsedView {public bool highwayCastle,ghostPawn,kingReversal;}
[Serializable] public class RoadStateView {public int turnNo,lane;public float speed;public string side,phase;public bool over;public string winner,reason;public RoadSideView w,b;public RoadHazardView[] hazards;public RoadShieldView[] shields;public RoadUsedView used;}
[Serializable] public class ChessSnapshot {
 public bool ok=true;public string fen,turn,error,locked,winner,reason,stateJson;
 public bool check,mate,draw,stalemate,over,accepted;public int tier,lane;public float speed;
 public ChessMove move;public ChessMove[] moves;public ChessPiece[] pieces;public ChessEvent[] events;public RoadStateView state;
 // Planning replies (op "ai") share the type so one parser serves every operation.
 public AiPlanView plan;public int score,depth,nodes,ms;
}
[Serializable] public class AiCard {public string card,target,target2;}
[Serializable] public class AiCyber {public string ability,target;}
[Serializable] public class AiAction {public string kind,from,to,promotion;public ChessStep[] steps;}
[Serializable] public class AiPlanView {public AiCard card;public AiCyber cyber;public AiAction action;}
[Serializable] public class ChessRequest {
 public string op="",fen,stateJson="",from,to,promotion="q",kind="";
 public bool roadEnabled,jumpKnown,chargeKnown;public float speedMph=-1;public int lane=-1,overclockLane;
 public int knownMask=-1,seed,count;public string cfgJson="",label="";
 public string card="",target="",target2="",ability="",difficulty="normal";public int budgetMs,depth;public bool noAbilities;public string squares="";public ChessStep[] steps;
}
// All ordinary chess rules are delegated to the same vendored chess.js in browser and Editor; Road / Cyber rules live in
// Tools/Chess/road-engine.cjs, which wraps chess.js (WebGL: generated into CheckmateChess.jslib).
public static class ChessEngine {
#if UNITY_WEBGL && !UNITY_EDITOR
 [DllImport("__Internal")] static extern IntPtr CM55_Chess(string request);
 [DllImport("__Internal")] static extern void CM55_ChessFree(IntPtr pointer);
#endif
 public static string Raw(string input){
  string output;
#if UNITY_WEBGL && !UNITY_EDITOR
  IntPtr p=CM55_Chess(input);try{output=Marshal.PtrToStringAnsi(p);}finally{CM55_ChessFree(p);}
#elif UNITY_EDITOR
  var info=new System.Diagnostics.ProcessStartInfo("node","Tools/Chess/bridge.cjs"){WorkingDirectory=System.IO.Directory.GetParent(Application.dataPath).FullName,UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
  using(var process=System.Diagnostics.Process.Start(info)){process.StandardInput.Write(input);process.StandardInput.Close();output=process.StandardOutput.ReadToEnd();string error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception("chess.js: "+error);}
#else
  throw new PlatformNotSupportedException("This project targets WebGL; chess.js Editor adapter requires Node.");
#endif
  return output;
 }
 // Marshalling through a WebGL plugin returns UTF-8; Marshal.PtrToStringAnsi on WebGL is UTF-8 aware.
 public static ChessSnapshot Send(ChessRequest request){
  var result=JsonUtility.FromJson<ChessSnapshot>(Raw(JsonUtility.ToJson(request)));
  if(result==null)throw new Exception("chess engine returned nothing");
  if(!string.IsNullOrEmpty(result.error)&&!result.accepted&&string.IsNullOrEmpty(result.locked)&&(result.moves==null||result.moves.Length==0)&&string.IsNullOrEmpty(result.fen))throw new Exception(result.error);
  return result;
 }
 // Original helper retained for callers that only need legal moves of a FEN (stateless, road optional).
 public static ChessSnapshot Query(string fen,string from=null,string to=null,string promotion="q",RoadChessRules road=null){
  var request=new ChessRequest{fen=fen,from=from,to=to,promotion=promotion};if(road!=null)road.Configure(request);
  var result=Send(request);if(!string.IsNullOrEmpty(result.error)&&!result.ok)throw new Exception(result.error);return result;
 }
}
}
