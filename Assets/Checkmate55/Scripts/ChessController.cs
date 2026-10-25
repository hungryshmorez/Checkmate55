using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Checkmate55 {
// Presentation + input for the lap tray. All rules (standard, Road, Cyber, cards, hazards) are resolved by the shared engine;
// this class only turns taps into engine requests and engine events into motion on the physical board.
public sealed class ChessController : MonoBehaviour {
 public ChessChallenge challenge;public ChessBoardView Board {get;private set;}
 public ChessSnapshot Snapshot {get;private set;}
 public bool Available=>true;public bool Active {get;private set;}public bool Busy {get;private set;}
 public bool PromotionPending=>pendingTo!=null;
 public string Selected {get;private set;}public int PlayerMoves {get;private set;}
 public string Instruction {get;private set;}="";
 public string PendingTool {get;private set;}="";
 public float TimeLeft {get;private set;}
 public event Action<string> Completed;
 // (winner "w" | "b" | "", reason) - raised whenever the engine ends the game (checkmate, stalemate, timeout ...).
 public event Action<string,string> Ended;
 public event Action<ChessEvent> EventRaised;
 public event Action<ChessChallenge> WhiteMoved;       // one White action completed on the board (human, AI passenger or room replay)
 public readonly RoadChessRules RoadRules=new RoadChessRules();
 // Opponent think time (ms) for browser play; 0 = fixed depth only (tests).
 public int AiBudgetMs=180;
 readonly HashSet<string> cyberSeen=new HashSet<string>();bool cardSeen,roadSeen;
 public bool AutoWhite;public string WhiteAiLevel="normal";
 SceneManager game;string pendingTo,ruleKey="",stateJson="",overStep="",swapFirst="";float savedYaw,savedPitch;RideState returnState;bool retry;float poll;bool timed;
 public bool RetryNeeded=>retry;
 static readonly ChessMove[] NoMoves=new ChessMove[0];
 ChessMove[] Moves=>Snapshot!=null&&Snapshot.moves!=null?Snapshot.moves:NoMoves;
 public void Initialize(SceneManager g){game=g;var go=new GameObject("Passenger chess security mechanism");go.transform.SetParent(g.Player.transform.parent,false);Board=go.AddComponent<ChessBoardView>();Board.Build(g);}
 public void InstructionOverride(string line){Instruction=line;}
 void Update(){if(!game||game.Paused)return;PumpNet();if(!Active)return;RoadRules.Sync(game);Board.ShowRoad(RoadRules,challenge.road);
  if(timed&&TimeLeft>0&&!Snapshot.over){TimeLeft-=Time.deltaTime;if(TimeLeft<=0){TimeLeft=0;Ended?.Invoke("b","timeout");}}
  if(AiAssist&&!Busy&&!assistBusy&&!retry&&!applying&&Snapshot!=null&&!Snapshot.over&&Snapshot.turn==challenge.playerColor&&assistedFen!=Snapshot.fen&&(challenge.cards||challenge.cyber)){StartCoroutine(AssistRoutine());return;}
  if(AiWhite&&retry&&!Busy){aiWhiteWait+=Time.deltaTime;if(aiWhiteWait>2f){aiWhiteWait=0;Retry();return;}}      // an AI passenger never sits on an ACCESS DENIED screen
  if(AiWhite&&!Busy&&!retry&&!Snapshot.over&&Snapshot.turn==challenge.playerColor&&!PromotionPending){aiWhiteWait+=Time.deltaTime;if(aiWhiteWait>1.4f){aiWhiteWait=0;StartCoroutine(AiWhiteTurn());return;}}
  if(Busy||retry||!challenge.road)return;
  poll-=Time.deltaTime;if(poll>0)return;poll=.25f;
  string key=RoadRules.ActiveRoadLane+":"+RoadRules.VehicleSpeedTier+":"+RoadRules.DiscoveredRules.Count;
  if(key!=ruleKey){ruleKey=key;RefreshMoves();UpdateStatus();}
 }
 // ---- engine plumbing
 ChessSnapshot Send(string op,Action<ChessRequest> fill=null){
  var request=new ChessRequest{op=op,fen=Snapshot!=null?Snapshot.fen:null,stateJson=stateJson};RoadRules.Configure(request);if(fill!=null)fill(request);
  var reply=ChessEngine.Send(request);if(!string.IsNullOrEmpty(reply.stateJson))stateJson=reply.stateJson;return reply;
 }
 void RefreshMoves(){try{var s=Send("query");if(s.moves!=null&&s.fen==Snapshot.fen){Snapshot=s;}}catch(Exception e){Debug.LogWarning(e.Message);return;}
  Board.ShowMarks(Snapshot);Board.ShowResources(Snapshot,PendingTool);if(Selected!=null)Board.Highlight(Selected,Moves.Where(m=>m.from==Selected).Select(m=>m.to).Distinct());}
 public void Begin(){
  if(!CanBegin())return;
  if(NetMode){Net.SendChessBegin(challenge.id,BeginRequest(),challenge.persistent);Instruction="Deploying for the whole room…";return;}
  Commence(null);
 }
 bool CanBegin(){
  if(Active){if(challenge!=null&&challenge.persistent)game.Dialogue.Say("Retract the ride game tray first (RETRACT TRAY), then deploy this.");return false;}
  if(game.Paused)return false;
  if(game.Seats.Moving){game.Dialogue.Say("Let your seat transfer finish, then tap authentication.");return false;}
  if(game.Seats.Seat==3){game.Seats.Choose(1);game.Dialogue.Say("The tray serves the rear seats. Returning to rear center; tap authentication once seated.");return false;}
  if(!game.Ride.Ready&&!game.State.Has("glovebox-authenticated")&&!game.AllowChessWithoutAuth&&!challenge.persistent){game.Ride.Authenticate();return false;}
  return true;
 }
 ChessRequest BeginRequest(){
  RoadRules.Enabled=challenge.road;RoadRules.AllRulesKnown=game.AllRulesKnown;RoadRules.Sync(game);
  var request=new ChessRequest{op="begin",fen=challenge.FenFor(game.RunSeed),seed=challenge.seed+game.RunSeed,cfgJson=challenge.ConfigJson(),squares=challenge.startHazards??""};RoadRules.Configure(request);return request;
 }
 void Commence(NetMsg net){
  RoadRules.Enabled=challenge.road;RoadRules.AllRulesKnown=game.AllRulesKnown;RoadRules.Sync(game);
  try{if(net!=null)StartSessionNet(net);else StartSession();}catch(Exception e){Debug.LogException(e);game.Dialogue.Say("Authentication module unavailable. Please retry.");return;}
  cyberSeen.Clear();cardSeen=false;roadSeen=false;Active=true;Busy=true;retry=false;PlayerMoves=0;Selected=null;pendingTo=null;overStep="";PendingTool="";ruleKey="";timed=challenge.timeLimitSeconds>0;TimeLeft=challenge.timeLimitSeconds;
  returnState=game.Ride.Hostile?RideState.EXPLORATION:game.State.Current;game.State.Enter(challenge.road?RideState.ROAD_CHESS:RideState.CHESS_CHALLENGE);savedYaw=game.Player.yaw;savedPitch=game.Player.pitch;
  Board.ShowTools(challenge.cyber,challenge.cards);Board.ShowPosition(Snapshot);Board.ShowResources(Snapshot,PendingTool);Board.SetVisible(true);StartCoroutine(Deploy());
 }
 void StartSession(){
  if(challenge.persistent&&!string.IsNullOrEmpty(challenge.savedState)){
   // resume the RIDE GAME exactly where it was retracted
   stateJson=challenge.savedState;var rq=new ChessRequest{op="query",fen=challenge.savedFen,stateJson=stateJson};RoadRules.Configure(rq);
   var resumed=ChessEngine.Send(rq);if(!resumed.ok)throw new Exception(resumed.error);if(!string.IsNullOrEmpty(resumed.stateJson))stateJson=resumed.stateJson;Snapshot=resumed;return;
  }
  var request=new ChessRequest{op="begin",fen=challenge.FenFor(game.RunSeed),seed=challenge.seed+game.RunSeed,cfgJson=challenge.ConfigJson()};RoadRules.Configure(request);
  var begun=ChessEngine.Send(request);if(!begun.ok)throw new Exception(begun.error);stateJson=begun.stateJson;Snapshot=begun;
  Snapshot=Send("startTurn");
  if(!string.IsNullOrEmpty(challenge.startHazards)){var h=Send("roadEvent",q=>{q.squares=challenge.startHazards;});if(h.ok)Snapshot=h;}
 }
 IEnumerator Deploy(){game.Audio.ControlSound("rear-seat",ControlAction.Seat,false);if(!Spectating)game.Player.FocusAngles(0,61);Instruction="Security tray deploying. You can still drag to look around.";
  for(float t=0;t<1;t+=Time.deltaTime/1.6f){Board.Pose(t,game.Seats.Offset.x);yield return null;}Board.Pose(1,game.Seats.Offset.x);Busy=false;
  Instruction=game.Save.Guidance==0?challenge.guidanceText+" Tap a white piece, then a glowing square.":challenge.guidanceText;game.Dialogue.Say(challenge.guidanceText);UpdateStatus();
 }
 public bool Handle(string id){if(!id.StartsWith("chess-"))return false;if(id.StartsWith("chess-tool-")){TapTool(id.Substring(11));return true;}TapSquare(id.Substring(6));return true;}
 // ---- tools: Cyber abilities and tactical cards
 void TapTool(string tool){
  if(!Active||Busy||PromotionPending)return;if(retry){Retry();return;}
  if(Snapshot.state==null||Snapshot.over){return;}
  var me=Snapshot.state.w;
  if(tool.StartsWith("card")){
   if(!challenge.cards){Instruction="No tactical cards in this challenge.";return;}
   int i=int.Parse(tool.Substring(4));var hand=me.hand??new string[0];if(i>=hand.Length){Instruction="Empty card slot. A new card is dealt at the start of each turn.";return;}
   if(me.cardPlayed){Instruction="One tactical card per turn - already played.";return;}
   string card=hand[i];if(PendingTool=="card:"+card){CancelTool();return;}
   if(card=="systemReboot"||card=="powerSurge"){PlayCard(card,"","");return;}
   PendingTool="card:"+card;swapFirst="";Instruction=card=="pawnBoost"?"PAWN BOOST · tap one of your pawns.":"GLITCH SWAP · tap the first of two friendly pieces (not the king).";Board.ShowResources(Snapshot,PendingTool);return;
  }
  if(!challenge.cyber){Instruction="Cyber Chess is not active in this challenge.";return;}
  int cost=tool=="firewall"?1:tool=="emp"?2:3;int cd=me.cd==null?0:tool=="firewall"?me.cd.firewall:tool=="emp"?me.cd.emp:me.cd.overdrive;
  if(me.cyberUsed){Instruction="One Cyber ability per turn - already used.";return;}
  if(me.charges<cost){Instruction=ToolName(tool)+" needs "+cost+" Cyber charge ("+me.charges+" available). One charge arrives each turn.";return;}
  if(cd>0){Instruction=ToolName(tool)+" is cooling down for "+cd+" more turn"+(cd==1?"":"s")+".";return;}
  if(PendingTool==tool){CancelTool();return;}
  PendingTool=tool;Instruction=tool=="firewall"?"FIREWALL · tap a friendly piece (not the king) to shield it from the next capture.":tool=="emp"?"EMP SHOCK · tap an enemy piece next to one of yours: it loses shields and special actions.":"CYBER OVERDRIVE · tap a friendly piece: it may move twice this turn.";
  Board.ShowResources(Snapshot,PendingTool);
 }
 static string ToolName(string t)=>t=="firewall"?"FIREWALL":t=="emp"?"EMP SHOCK":"OVERDRIVE";
 void CancelTool(){PendingTool="";swapFirst="";Instruction=challenge.guidanceText;Board.ShowResources(Snapshot,PendingTool);}
 void HandleToolTarget(string square){
  string tool=PendingTool;
  if(tool.StartsWith("card:")){string card=tool.Substring(5);if(card=="glitchSwap"){if(swapFirst==""){swapFirst=square;Board.Highlight(square,null);Instruction="GLITCH SWAP · now tap the second piece.";return;}PlayCard(card,swapFirst,square);return;}PlayCard(card,square,"");return;}
  UseCyber(tool,square);
 }
 void PlayCard(string card,string target,string target2){
  if(NetMode){var req=new ChessRequest{op="card",card=card,target=target??"",target2=target2??""};RoadRules.Sync(game);RoadRules.Configure(req);PendingTool="";swapFirst="";Busy=true;netAwait=true;netWaitUntil=Time.time+6f;Net.SendChess(req);return;}
  ChessSnapshot reply;try{reply=Send("card",q=>{q.card=card;q.target=target;q.target2=target2;});}catch(Exception e){Instruction=e.Message;return;}
  PendingTool="";swapFirst="";if(!reply.ok){Instruction=Friendly(reply.error);Board.Highlight(null,null);Board.ShowResources(Snapshot,PendingTool);return;}
  cardSeen=true;Snapshot=reply;Board.ShowPosition(Snapshot);Board.ShowResources(Snapshot,PendingTool);game.Audio.Chime();Instruction=card=="powerSurge"?"POWER SURGE · +2 Cyber charge, cooldowns reduced. Now make your move.":card=="systemReboot"?"SYSTEM REBOOT · every hazard tile cleared. Now make your move.":card=="pawnBoost"?"PAWN BOOST · that pawn may advance two squares this turn.":"GLITCH SWAP · the two pieces traded places. Now make your move.";
  foreach(var e in reply.events??new ChessEvent[0])EventRaised?.Invoke(e);
 }
 void UseCyber(string ability,string square){
  if(NetMode){var req=new ChessRequest{op="cyber",ability=ability,target=square??""};RoadRules.Sync(game);RoadRules.Configure(req);PendingTool="";Busy=true;netAwait=true;netWaitUntil=Time.time+6f;Net.SendChess(req);return;}
  ChessSnapshot reply;try{reply=Send("cyber",q=>{q.ability=ability;q.target=square;});}catch(Exception e){Instruction=e.Message;return;}
  PendingTool="";if(!reply.ok){Instruction=Friendly(reply.error);Board.ShowResources(Snapshot,PendingTool);return;}
  cyberSeen.Add(ability);Snapshot=reply;Board.ShowPosition(Snapshot);Board.ShowResources(Snapshot,PendingTool);game.Audio.Chime();
  Instruction=ability=="firewall"?"FIREWALL up. The next capture of that piece bounces off.":ability=="emp"?"EMP SHOCK · that piece is disabled through its next turn.":"OVERDRIVE armed · tap the piece, then each step of its double move.";
  Board.Flash(square,Color.white);foreach(var e in reply.events??new ChessEvent[0])EventRaised?.Invoke(e);
  if(ability=="overdrive")TapSquare(square);
 }
 static string Friendly(string error){
  if(string.IsNullOrEmpty(error))return "That is not allowed.";
  if(error.Contains("one cyber"))return "One Cyber ability per turn.";if(error.Contains("one card"))return "One tactical card per turn.";if(error.Contains("not enough"))return "Not enough Cyber charge.";if(error.Contains("cooling"))return "That ability is still cooling down.";
  if(error.Contains("adjacent"))return "EMP needs an enemy piece next to one of yours.";if(error.Contains("king"))return "Kings cannot be targeted - and a swap may not leave a king in check.";if(error.Contains("EMP"))return "That piece is disabled by EMP.";if(error.Contains("already shielded"))return "Already shielded.";
  if(error.Contains("pawn cannot land"))return "A swap may not put a pawn on a back rank.";if(error.Contains("illegal position"))return "That swap would create an illegal position.";return "Not allowed: "+error;
 }
 // ---- board input
 public void TapSquare(string square){if(!Active||Busy||PromotionPending)return;if(retry){Retry();return;}
  if(PendingTool!=""){HandleToolTarget(square);return;}
  if(challenge.road){RoadRules.Sync(game);RefreshMoves();}
  var piece=Snapshot.pieces.FirstOrDefault(p=>p.square==square);
  var armed=Snapshot.state!=null&&Snapshot.state.w!=null&&!string.IsNullOrEmpty(Snapshot.state.w.overdrive);
  // Overdrive step two: a second tap on an amber square completes the piece's double move.
  if(overStep!=""&&Selected!=null){
   var seq=Moves.FirstOrDefault(m=>m.kind=="overdrive"&&m.from==Selected&&m.steps!=null&&m.steps.Length==2&&m.steps[0].to==overStep&&m.steps[1].to==square);
   if(seq!=null){RunWhite(seq);return;}
   if(square==overStep){var single=Moves.FirstOrDefault(m=>m.kind!="overdrive"&&m.from==Selected&&m.to==overStep);overStep="";if(single!=null){Execute(single);return;}}
   overStep="";
  }
  if(piece!=null&&piece.color==challenge.playerColor){Selected=square;overStep="";var targets=Moves.Where(m=>m.from==square&&m.kind!="overdrive").Select(m=>m.to).Concat(Moves.Where(m=>m.from==square&&m.kind=="overdrive"&&m.steps!=null&&m.steps.Length>0).Select(m=>m.steps[0].to)).Distinct().ToArray();Board.Highlight(square,targets);
   game.Audio.Click(850);Instruction=targets.Length==0&&challenge.road?"No move for this piece right now. Lane and speed decide the vehicle's special moves.":game.Save.Guidance==0?Explain(piece.type)+" Tap a glowing target."+(challenge.road?" Amber rails show the active lane.":"")+(armed?" OVERDRIVE: tap the first step, then the second.":""):challenge.guidanceText;UpdateStatus();return;
  }
  if(Selected==null){game.Audio.Click(300);Instruction="Select a white piece first.";return;}
  // A tap on the first step of an Overdrive double move reveals the second-step squares.
  var starts=Moves.Where(m=>m.kind=="overdrive"&&m.from==Selected&&m.steps!=null&&m.steps.Length==2&&m.steps[0].to==square).ToArray();
  if(starts.Length>0){overStep=square;Board.Highlight(Selected,Moves.Where(m=>m.kind!="overdrive"&&m.from==Selected).Select(m=>m.to));Board.HighlightSecond(starts.Select(m=>m.steps[1].to).Distinct());Instruction="OVERDRIVE · tap an amber square for the second step, or tap "+square+" again to stop there.";return;}
  var move=Moves.Where(m=>m.from==Selected&&m.to==square&&m.kind!="overdrive").OrderBy(m=>m.kind=="move"||m.kind=="castle"?0:1).FirstOrDefault();
  if(move!=null)Execute(move);
  else{
   if(Selected!=null&&Snapshot.state!=null&&!string.IsNullOrEmpty(challenge.id)){var locked=ProbeLocked(Selected,square);if(locked!=null){game.Audio.LockClick();game.Dialogue.Say(locked);Instruction=locked;return;}}
   game.Audio.Click(300);Instruction="Move unavailable at this lane or speed. Choose a glowing square, or wait for the road to change.";
  }
 }
 // The car lies about hidden rules: attempting one before it is unlocked draws a denial instead of a plain "illegal".
 string ProbeLocked(string from,string to){
  try{var r=Send("probe",q=>{q.from=from;q.to=to;});string l=r.locked??"";
   if(l.StartsWith("locked:"))return LockedLine();if(l=="speed:70")return "Road Jump needs 70+ MPH.";if(l=="speed:90")return "Lane Charge needs 90+ MPH.";if(l=="lane")return "That piece is outside the active lane.";}
  catch(Exception){}
  return null;
 }
 int lockedCount;
 string LockedLine(){lockedCount++;return lockedCount%3==1?"No.":lockedCount%3==2?"That rule does not exist.":"Do not make that move.";}
 void Execute(ChessMove move){
  if(!string.IsNullOrEmpty(move.promotion)){pendingTo=move.to;Instruction="PROMOTION · Choose your new piece.";return;}
  RunWhite(move);
 }
 // One White action: played locally, or (in a room) sent to the server, which validates it and broadcasts the result to everyone.
 void RunWhite(ChessMove move){
  if(!NetMode){StartCoroutine(PlayerTurn(move));return;}
  var req=new ChessRequest{op="act",kind=move.kind,from=move.from,to=move.to,promotion=move.promotion??"",steps=move.steps};RoadRules.Sync(game);RoadRules.Configure(req);
  Busy=true;netAwait=true;netWaitUntil=Time.time+6f;Board.Highlight(null,null);Instruction="Sending to the room…";Net.SendChess(req);
 }
 public void Promote(string type){if(!PromotionPending||Busy)return;var move=Moves.FirstOrDefault(m=>m.from==Selected&&m.to==pendingTo&&m.promotion==type&&m.kind!="overdrive");pendingTo=null;if(move!=null)RunWhite(move);}
 ChessMove ScriptedReply(ChessMove[] list)=>list.Where(m=>m.kind=="move"||m.kind=="castle").OrderBy(m=>!string.IsNullOrEmpty(m.captured)?0:m.piece=="p"?1:2).ThenBy(m=>m.from,StringComparer.Ordinal).ThenBy(m=>m.to,StringComparer.Ordinal).First();
 IEnumerator PlayerTurn(ChessMove move){Busy=true;PendingTool="";overStep="";
  ChessSnapshot reply=null;try{reply=Send("act",q=>{q.kind=move.kind;q.from=move.from;q.to=move.to;q.promotion=move.promotion??"";q.steps=move.steps;});}catch(Exception e){Debug.LogException(e);}
  if(reply==null||!reply.accepted){Instruction="Vehicle conditions changed. Select your move again.";Busy=false;if(reply!=null&&reply.moves!=null&&reply.moves.Length>0)Snapshot=reply;yield break;}
  Board.Highlight(null,null);yield return Present(reply);
  RoadRules.OnChessMove(reply.move);
  yield return AfterWhite(move,reply);
 }
 // Everything that follows White's action: the car's reply (AI or human), the objective check and the end-of-turn bookkeeping.
 // Shared by the human passenger and the AI passenger so both are judged by exactly the same rules.
 IEnumerator AfterWhite(ChessMove move,ChessSnapshot reply){
  PlayerMoves++;WhiteMoved?.Invoke(challenge);
  bool success=PreSuccess(move,reply);ChessMove replyMove=null;
  if(!Snapshot.over){
   Instruction=game.Save.Guidance==0?"THE CAR MOVES BLACK · Watch its reply.":"Vehicle response…";Board.status.text="VEHICLE RESPONSE";yield return new WaitForSeconds(.8f);
   yield return CarTurn(r=>replyMove=r);
  }
  yield return Conclude(move,reply,replyMove,success);
 }
 // Objective test on the position right after White's action (before the car answers).
 bool PreSuccess(ChessMove move,ChessSnapshot reply){
  bool success=challenge.Success(Snapshot,PlayerMoves);
  if(challenge.objectiveType==ChessObjective.CaptureTarget)success=move.captured==challenge.targetPiece||(reply.events??new ChessEvent[0]).Any(e=>e.t=="capture"&&e.side=="w"&&e.captured==challenge.targetPiece);
  return success;
 }
 // Objective bookkeeping once both sides have acted. Shared by local play, the AI passenger and room replays.
 IEnumerator Conclude(ChessMove move,ChessSnapshot reply,ChessMove replyMove,bool success){
  if(Snapshot.over){Ended?.Invoke(Snapshot.winner,Snapshot.reason);}
  if(challenge.objectiveType==ChessObjective.SurviveMoves)success=Snapshot.turn==challenge.playerColor&&!Snapshot.check&&!Snapshot.mate&&PlayerMoves>=challenge.maxMoves;
  if(challenge.objectiveType==ChessObjective.SacrificePiece)success=replyMove!=null&&replyMove.captured==challenge.targetPiece;
  if(challenge.objectiveType==ChessObjective.Checkmate)success=Snapshot.over&&Snapshot.winner==challenge.playerColor;
  if(challenge.objectiveType==ChessObjective.UseCyber)success=cyberSeen.Count>=3;
  if(move.kind!="move"&&move.kind!="castle")roadSeen=roadSeen||string.IsNullOrEmpty(challenge.targetPiece)||move.kind==challenge.targetPiece;
  if(challenge.objectiveType==ChessObjective.RoadMove)success=roadSeen;
  if(challenge.objectiveType==ChessObjective.PlayCard)success=cardSeen;
  if(success){game.State.Flag(challenge.rewardEvent);game.State.Flag("challenge."+challenge.id);Completed?.Invoke(challenge.rewardEvent);Instruction=game.Security.Result;Board.status.text=challenge.resultText;game.Save.TutorialCompleted=true;if(challenge.timeLimitSeconds<=0){yield return new WaitForSeconds(1.7f);yield return Retract();}else Busy=false;}
  else if(challenge.noRetry&&(Snapshot.over||challenge.Failure(Snapshot,PlayerMoves))){Busy=false;if(Snapshot.over){Instruction=ResultText();Board.status.text=ResultText();}}
  else if(NetMode&&(challenge.Failure(Snapshot,PlayerMoves)||Snapshot.mate)){
   // a room cannot "reset" one client's board: the tray is retracted for everyone and any passenger can deploy it again
   Instruction="The vehicle refuses authorization. The tray retracts; deploy it again to retry.";Board.status.text="ACCESS DENIED";game.Dialogue.Say("Your request lacks the required authority.");yield return new WaitForSeconds(1.6f);yield return Retract();
  }
  else if(challenge.Failure(Snapshot,PlayerMoves)||Snapshot.mate){Instruction="The vehicle refuses authorization. Tap any square to reset, or retract and explore.";Board.status.text="ACCESS DENIED / TAP A SQUARE TO RETRY";game.Dialogue.Say("Your request lacks the required authority.");Busy=false;retry=true;}
  else {Busy=false;if(challenge.road){RoadRules.Sync(game);RefreshMoves();}Instruction=challenge.guidanceText;UpdateStatus();}
 }
 IEnumerator CarTurn(Action<ChessMove> reported){if(HumanCar)yield return HumanCarTurn(reported);else yield return AiTurn(false,challenge.opponent,reported);}
 // ---- the car as a human player (Driverless Duel / Car Master): Black waits for a validated submission
 public bool HumanCar;public bool AiWhite;public string AiWhiteLevel="normal";
 public bool AwaitingCar {get;private set;}
 public float CarTurnLimit=90f;
 ChessMove carMove;ChessSnapshot lastActReply;
 public ChessMove[] CarMoves=>AwaitingCar&&Snapshot!=null&&Snapshot.moves!=null?Snapshot.moves:new ChessMove[0];
 // The car chooses one of the legal Black actions it was offered. Anything else is refused: the car cannot invent moves.
 public bool SubmitCarMove(string kind,string from,string to,string promotion)
 {
  if(!AwaitingCar||carMove!=null)return false;
  var m=CarMoves.FirstOrDefault(x=>x.from==from&&x.to==to&&(string.IsNullOrEmpty(kind)||x.kind==kind)&&(x.promotion??"")==(promotion??"")&&x.kind!="overdrive");
  if(m==null)return false;
  if(NetMode){var req=new ChessRequest{op="act",kind=m.kind,from=m.from,to=m.to,promotion=m.promotion??""};RoadRules.Sync(game);RoadRules.Configure(req);AwaitingCar=false;Net.SendChess(req);return true;}
  carMove=m;return true;
 }
 public bool SubmitCarCyber(string ability,string square){
  if(!AwaitingCar||carMove!=null||!challenge.carCyber)return false;
  if(NetMode){var req=new ChessRequest{op="cyber",ability=ability,target=square??""};RoadRules.Sync(game);RoadRules.Configure(req);Net.SendChess(req);return true;}
  try{var r=Send("cyber",q=>{q.ability=ability;q.target=square??"";});if(!r.ok)return false;Snapshot=r;Board.ShowPosition(Snapshot);Board.Flash(square,Color.cyan);foreach(var e in r.events??new ChessEvent[0])EventRaised?.Invoke(e);game.Audio.Chime();Instruction="THE CAR USES "+ToolName(ability)+" ON "+(square??"").ToUpper()+".";return true;}
  catch(Exception e){Debug.LogException(e);return false;}
 }
 IEnumerator HumanCarTurn(Action<ChessMove> reported){
  ChessSnapshot black=null;try{black=Send("startTurn");}catch(Exception e){Debug.LogException(e);yield break;}
  Snapshot=black;carMove=null;AwaitingCar=true;Instruction="THE CAR IS CHOOSING BLACK'S MOVE…";
  float t=0;while(carMove==null&&Active&&t<CarTurnLimit){if(!game.Paused)t+=Time.deltaTime;yield return null;}
  AwaitingCar=false;
  if(!Active)yield break;
  if(carMove==null){yield return AiTurn(false,"casual",reported,false);yield break;}      // an absent car never stalls the ride
  var action=new AiAction{kind=carMove.kind,from=carMove.from,to=carMove.to,promotion=carMove.promotion,steps=new ChessStep[0]};
  yield return ActAndPresent(false,action,new ChessMove{from=action.from,to=action.to,kind=action.kind,promotion=action.promotion},reported);
 }

 // ---- room play: the server is authoritative. A client never decides a result; it sends intents and replays the exact engine ops
 // the server broadcasts (same engine, same seed, same state), then checks that its position still equals the server's.
 public NetClient Net;public Func<string,ChessChallenge> ChallengeLookup;
 public bool NetMode=>Net!=null&&Net.Status==NetStatus.Started;
 public bool Spectating=>NetMode&&Net.IsCar;
 float netWaitUntil;bool netAwait,applying,retracting;readonly Queue<NetMsg> netQueue=new Queue<NetMsg>();
 public void OnNetMessage(NetMsg m){
  switch(m.t){
   case "ops":case "resync":netQueue.Enqueue(m);break;
   case "chessEnd":if(Active&&!retracting&&!applying)StartCoroutine(Retract());break;
   case "deny":
    if(m.@ref=="chess"||m.@ref=="chessBegin"){netAwait=false;if(Active&&!applying)Busy=false;Instruction=DenyText(m);if(game!=null)game.Audio.LockClick();if(Active&&Snapshot!=null&&!Snapshot.over)RefreshMoves();}
    break;
  }
 }
 static string DenyText(NetMsg m){
  switch(m.reason){
   case "not-your-move":return "It is the other passenger's move this turn.";case "turn-locked":return "Your team already moved this turn.";case "support-unavailable":return "No more support actions this turn.";
   case "not-white-turn":return "Wait for the car to answer.";case "not-your-side":return "That is not your side to move.";case "cards-not-allowed":return "The car holds no tactical cards.";
   case "illegal":return "The room refused that action: conditions changed. Select it again.";case "chess-active":return "A board is already open.";case "car-cannot-deploy":return "Only passengers can deploy the tray.";
   default:return "Refused: "+m.reason;}
 }
 void PumpNet(){
  if(netAwait&&Time.time>netWaitUntil){netAwait=false;if(Active&&!applying)Busy=false;Instruction="No answer from the room. Resynchronising…";if(Net!=null)Net.RequestResync();}
  if(applying||netQueue.Count==0)return;
  if(Active&&Busy&&!netAwait)return;                   // an animation or deploy is running
  var m=netQueue.Dequeue();
  if(m.t=="resync"){ApplyResync(m);return;}
  if(m.kind=="begin"){
   if(Active)return;
   var c=ChallengeLookup!=null?ChallengeLookup(m.challengeId):null;if(c==null){Debug.LogWarning("net: unknown challenge "+m.challengeId);return;}
   challenge=c;Commence(m);return;
  }
  if(!Active||Snapshot==null)return;
  StartCoroutine(ApplyNet(m));
 }
 void ApplyResync(NetMsg m){
  if(!Active)return;
  try{var q=new ChessRequest{op="query",fen=m.fen,stateJson=m.stateJson};var s=ChessEngine.Send(q);stateJson=string.IsNullOrEmpty(s.stateJson)?m.stateJson:s.stateJson;Snapshot=s;Board.ShowPosition(Snapshot);Board.ShowMarks(Snapshot);Board.ShowResources(Snapshot,"");Busy=false;netAwait=false;AwaitingCar=Spectating&&!Snapshot.over&&Snapshot.turn!=challenge.playerColor;}
  catch(Exception e){Debug.LogException(e);}
 }
 ChessSnapshot SendOp(ChessRequest op){
  var q=JsonUtility.FromJson<ChessRequest>(JsonUtility.ToJson(op));
  if(q.op!="begin"){q.fen=Snapshot!=null?Snapshot.fen:q.fen;q.stateJson=stateJson;}
  var reply=ChessEngine.Send(q);if(!string.IsNullOrEmpty(reply.stateJson))stateJson=reply.stateJson;return reply;
 }
 void StartSessionNet(NetMsg msg){
  if(msg.resume){
   stateJson=msg.stateJson;var rq=new ChessRequest{op="query",fen=msg.fen,stateJson=stateJson};RoadRules.Configure(rq);var back=ChessEngine.Send(rq);if(!back.ok)throw new Exception(back.error);
   if(!string.IsNullOrEmpty(back.stateJson))stateJson=back.stateJson;Snapshot=back;return;
  }
  foreach(var op in msg.ops??new ChessRequest[0]){var r=SendOp(op);if(!r.ok)throw new Exception(r.error);Snapshot=r;}
  if(!string.IsNullOrEmpty(msg.fen)&&Snapshot.fen!=msg.fen&&Net!=null)Net.RequestResync();
 }
 IEnumerator ApplyNet(NetMsg msg){
  applying=true;Busy=true;netAwait=false;
  ChessMove whiteMove=null,blackMove=null;ChessSnapshot whiteReply=null;bool pre=false,desync=false;string side=string.IsNullOrEmpty(msg.who)?"w":msg.who;
  foreach(var op in msg.ops??new ChessRequest[0]){
   ChessSnapshot r=null;try{r=SendOp(op);}catch(Exception e){Debug.LogException(e);desync=true;break;}
   if(r==null||!r.ok){desync=true;break;}
   switch(op.op){
    case "act":{
     Board.Highlight(null,null);bool mine=side==challenge.playerColor;
     if(!mine){Instruction="THE CAR MOVES BLACK · Watch its reply.";Board.status.text="VEHICLE RESPONSE";yield return new WaitForSeconds(.45f);}
     yield return Present(r);RoadRules.OnChessMove(r.move);
     var cm=new ChessMove{from=op.from,to=op.to,kind=op.kind,promotion=op.promotion,captured=r.move!=null?r.move.captured:"",steps=op.steps};
     if(mine){PlayerMoves++;WhiteMoved?.Invoke(challenge);whiteMove=cm;whiteReply=r;pre=PreSuccess(cm,r);}else blackMove=cm;
     side=side=="w"?"b":"w";break;}
    case "card":if(side==challenge.playerColor)cardSeen=true;Snapshot=r;Board.ShowPosition(Snapshot);Board.ShowResources(Snapshot,"");game.Audio.Chime();foreach(var e in r.events??new ChessEvent[0])EventRaised?.Invoke(e);yield return new WaitForSeconds(.35f);break;
    case "cyber":if(side==challenge.playerColor)cyberSeen.Add(op.ability);Snapshot=r;Board.ShowPosition(Snapshot);Board.Flash(op.target,Color.cyan);game.Audio.Chime();foreach(var e in r.events??new ChessEvent[0])EventRaised?.Invoke(e);Instruction=(side==challenge.playerColor?"THE TEAM USES ":"THE CAR USES ")+ToolName(op.ability)+" ON "+(op.target??"").ToUpper()+".";yield return new WaitForSeconds(.5f);break;
    default:Snapshot=r;Board.ShowMarks(Snapshot);Board.ShowResources(Snapshot,"");foreach(var e in r.events??new ChessEvent[0])EventRaised?.Invoke(e);break;
   }
  }
  if(desync||(!string.IsNullOrEmpty(msg.fen)&&Snapshot.fen!=msg.fen)){if(Net!=null)Net.RequestResync();applying=false;Busy=false;yield break;}
  Board.ShowPosition(Snapshot);
  if(whiteMove!=null)yield return Conclude(whiteMove,whiteReply,blackMove,pre);
  else{
   if(Snapshot.over){Ended?.Invoke(Snapshot.winner,Snapshot.reason);Instruction=ResultText();Board.status.text=ResultText();}
   else{Instruction=challenge.guidanceText;UpdateStatus();}
   Busy=false;
  }
  AwaitingCar=Spectating&&Active&&!Snapshot.over&&Snapshot.turn!=challenge.playerColor;
  if(Active&&challenge.road&&!Snapshot.over&&!AwaitingCar){RoadRules.Sync(game);RefreshMoves();}
  applying=false;
 }

 // ---- AI passenger planning that respects authored objectives, and the AI partner who supports a human in offline Co-op
 public bool AiAssist;string assistedFen="";bool assistBusy;
 AiPlanView ObjectivePlan(){
  if(challenge==null||Snapshot==null)return null;
  var o=challenge.objectiveType;
  if(o==ChessObjective.UseCyber)return CyberObjectivePlan();
  ChessMove pick=null;
  if(o==ChessObjective.CaptureTarget)pick=Moves.FirstOrDefault(m=>m.captured==challenge.targetPiece);
  else if(o==ChessObjective.DeliverCheck)pick=Moves.FirstOrDefault(m=>m.san!=null&&(m.san.Contains("+")||m.san.Contains("#")));
  if(pick==null&&(o==ChessObjective.SacrificePiece||o==ChessObjective.DeliverCheck||o==ChessObjective.CaptureTarget||o==ChessObjective.RoadMove)&&challenge.hintMove.Length==4)pick=Moves.FirstOrDefault(m=>m.from==challenge.hintMove.Substring(0,2)&&m.to==challenge.hintMove.Substring(2,2)&&m.kind!="overdrive");
  if(pick==null&&o==ChessObjective.RoadMove)pick=Moves.FirstOrDefault(m=>m.kind!="move"&&m.kind!="castle"&&(string.IsNullOrEmpty(challenge.targetPiece)||m.kind==challenge.targetPiece));
  if(pick==null)return null;
  return new AiPlanView{action=new AiAction{kind=pick.kind,from=pick.from,to=pick.to,promotion=pick.promotion??"",steps=new ChessStep[0]}};
 }
 // FIREWALL a knight, EMP an enemy piece beside one of ours, OVERDRIVE a rook: one ability per turn, as the charges allow.
 AiPlanView CyberObjectivePlan(){
  var me=Snapshot.state!=null?Snapshot.state.w:null;if(me==null||me.cyberUsed||Snapshot.pieces==null)return null;
  var cd=me.cd??new RoadCooldowns();
  var white=Snapshot.pieces.Where(p=>p.color=="w").ToArray();var black=Snapshot.pieces.Where(p=>p.color=="b").ToArray();
  AiCyber c=null;
  if(!cyberSeen.Contains("firewall")&&me.charges>=1&&cd.firewall<=0){var t=white.FirstOrDefault(p=>p.type=="n")??white.FirstOrDefault(p=>p.type!="k");if(t!=null)c=new AiCyber{ability="firewall",target=t.square};}
  else if(!cyberSeen.Contains("emp")&&me.charges>=2&&cd.emp<=0){
   var t=black.FirstOrDefault(b=>b.type!="k"&&!b.emp&&white.Any(w=>Mathf.Abs(w.square[0]-b.square[0])<=1&&Mathf.Abs(w.square[1]-b.square[1])<=1));
   if(t!=null)c=new AiCyber{ability="emp",target=t.square};
  }
  else if(!cyberSeen.Contains("overdrive")&&me.charges>=3&&cd.overdrive<=0){var t=white.FirstOrDefault(p=>p.type=="r")??white.FirstOrDefault(p=>p.type=="q")??white.FirstOrDefault(p=>p.type!="k"&&p.type!="p");if(t!=null)c=new AiCyber{ability="overdrive",target=t.square};}
  return c==null?null:new AiPlanView{cyber=c};
 }
 IEnumerator AssistRoutine(){
  assistBusy=true;Busy=true;yield return new WaitForSeconds(1.1f);
  if(Active&&Snapshot!=null&&!Snapshot.over&&Snapshot.turn==challenge.playerColor){
   ChessSnapshot plan=null;try{plan=Send("ai",q=>{q.difficulty="normal";q.budgetMs=120;});}catch(Exception e){Debug.LogException(e);}
   if(plan!=null&&plan.ok&&plan.plan!=null){
    if(plan.plan.card!=null&&!string.IsNullOrEmpty(plan.plan.card.card)){var c=plan.plan.card;try{var cr=Send("card",q=>{q.card=c.card;q.target=c.target??"";q.target2=c.target2??"";});if(cr.ok){Snapshot=cr;Board.ShowPosition(Snapshot);game.Audio.Chime();Instruction="PASSENGER B PLAYS "+c.card.ToUpper()+".";foreach(var e in cr.events??new ChessEvent[0])EventRaised?.Invoke(e);}}catch(Exception e){Debug.LogException(e);}}
    else if(plan.plan.cyber!=null&&!string.IsNullOrEmpty(plan.plan.cyber.ability)){var c=plan.plan.cyber;try{var cr=Send("cyber",q=>{q.ability=c.ability;q.target=c.target??"";});if(cr.ok){Snapshot=cr;Board.ShowPosition(Snapshot);Board.Flash(c.target,Color.cyan);game.Audio.Chime();Instruction="PASSENGER B USES "+ToolName(c.ability)+" ON "+c.target.ToUpper()+".";foreach(var e in cr.events??new ChessEvent[0])EventRaised?.Invoke(e);}}catch(Exception e){Debug.LogException(e);}}
    yield return new WaitForSeconds(.5f);
   }
  }
  if(Snapshot!=null)assistedFen=Snapshot.fen;
  Busy=false;assistBusy=false;if(Active)UpdateStatus();
 }
 // ---- the passenger as an AI (Car Master / AI Simulation in the story world)
 float aiWhiteWait;
 IEnumerator AiWhiteTurn(){
  Busy=true;ChessMove mv=null;lastActReply=null;
  if(challenge.road){RoadRules.Sync(game);RefreshMoves();}
  yield return AiTurn(true,AiWhiteLevel,m=>mv=m);
  if(lastActReply==null||!lastActReply.accepted||mv==null){Busy=false;yield break;}
  yield return AfterWhite(mv,lastActReply);
 }
 // One whole turn for an AI-controlled side: start of turn, optional card / Cyber ability, one chess action. Black's turn then hands White the move.
 // The AI plans from the same position, shields, hazards and hand a human would see and acts through the same engine operations.
 IEnumerator AiTurn(bool white,string level,Action<ChessMove> reported,bool begin=true){
  if(!white&&begin){ChessSnapshot black=null;try{black=Send("startTurn");}catch(Exception e){Debug.LogException(e);yield break;}
  Snapshot=black;}
  ChessMove chosen=null;AiAction action=null;
  if(level=="scripted"){
   var list=Moves.Where(m=>m.kind=="move"||m.kind=="castle").ToArray();
   if(list.Length==0){yield break;}
   chosen=ScriptedReply(list);action=new AiAction{kind=chosen.kind,from=chosen.from,to=chosen.to,promotion=chosen.promotion,steps=new ChessStep[0]};
  }else{
   bool scriptedCyber=white&&challenge.objectiveType==ChessObjective.UseCyber;
   ChessSnapshot plan=null;try{plan=Send("ai",q=>{q.difficulty=level;q.budgetMs=AiBudgetMs;q.noAbilities=scriptedCyber;});}catch(Exception e){Debug.LogException(e);}
   if(white&&plan!=null&&plan.ok&&plan.plan!=null){
    // authored objectives (a check, a sacrifice, a capture, "use every Cyber ability") are solved on purpose, not by search
    var authored=ObjectivePlan();
    if(authored!=null){if(authored.action!=null&&!string.IsNullOrEmpty(authored.action.from))plan.plan.action=authored.action;if(authored.cyber!=null){plan.plan.cyber=authored.cyber;if(authored.cyber.ability=="overdrive")plan.plan.action=null;}}
   }
   if(plan==null||!plan.ok||plan.plan==null||(plan.plan.action==null&&!(plan.plan.cyber!=null&&plan.plan.cyber.ability=="overdrive"))||(plan.plan.action!=null&&string.IsNullOrEmpty(plan.plan.action.from))){
    var fallback=Moves.FirstOrDefault(m=>m.kind=="move"||m.kind=="castle");if(fallback==null)yield break;action=new AiAction{kind=fallback.kind,from=fallback.from,to=fallback.to,promotion=fallback.promotion,steps=new ChessStep[0]};
   }else{
    action=plan.plan.action;
    if(plan.plan.card!=null&&!string.IsNullOrEmpty(plan.plan.card.card)){var c=plan.plan.card;try{var cr=Send("card",q=>{q.card=c.card;q.target=c.target??"";q.target2=c.target2??"";});if(cr.ok)Snapshot=cr;}catch(Exception e){Debug.LogException(e);}yield return new WaitForSeconds(.35f);}
    if(plan.plan.cyber!=null&&!string.IsNullOrEmpty(plan.plan.cyber.ability)){var c=plan.plan.cyber;try{var cr=Send("cyber",q=>{q.ability=c.ability;q.target=c.target??"";});if(cr.ok){Snapshot=cr;Board.ShowPosition(Snapshot);Board.Flash(c.target,Color.cyan);foreach(var e in cr.events??new ChessEvent[0])EventRaised?.Invoke(e);game.Audio.Chime();Instruction=(white?"THE AI PASSENGER USES ":"THE CAR USES ")+ToolName(c.ability)+" ON "+c.target.ToUpper()+".";}}catch(Exception e){Debug.LogException(e);}yield return new WaitForSeconds(.7f);}
    if(action==null||string.IsNullOrEmpty(action.from)){
     // after OVERDRIVE the double moves are on the table: take one for the armed piece, else any ordinary move
     var armedTo=plan.plan.cyber!=null?plan.plan.cyber.target:"";
     var dm=Moves.FirstOrDefault(m=>m.kind=="overdrive"&&m.from==armedTo&&m.steps!=null&&m.steps.Length==2)??Moves.FirstOrDefault(m=>m.kind=="move"||m.kind=="castle");
     if(dm==null)yield break;action=new AiAction{kind=dm.kind,from=dm.from,to=dm.to,promotion=dm.promotion,steps=dm.steps??new ChessStep[0]};
    }
   }
   chosen=new ChessMove{from=action.from,to=action.to,kind=action.kind,promotion=action.promotion};
  }
  yield return ActAndPresent(white,action,chosen,reported);
 }
 IEnumerator ActAndPresent(bool white,AiAction action,ChessMove chosen,Action<ChessMove> reported){
  ChessSnapshot blackMove=null;try{blackMove=Send("act",q=>{q.kind=action.kind;q.from=action.from;q.to=action.to;q.promotion=action.promotion??"";q.steps=action.steps;});}catch(Exception e){Debug.LogException(e);}
  lastActReply=blackMove;
  if(blackMove==null||!blackMove.accepted)yield break;
  yield return Present(blackMove);
  chosen.captured=blackMove.move!=null?blackMove.move.captured:"";reported(chosen);
  RoadRules.OnChessMove(blackMove.move);
  if(!white&&!Snapshot.over){
   ChessSnapshot wt=null;try{wt=Send("startTurn");}catch(Exception e){Debug.LogException(e);}
   if(wt!=null&&wt.ok){Snapshot=wt;Board.ShowMarks(Snapshot);Board.ShowResources(Snapshot,PendingTool);foreach(var e in wt.events??new ChessEvent[0])EventRaised?.Invoke(e);}
  }
 }
 // Animates one engine reply (moves, captures, hazards, shields) on the physical tray, then rebuilds the exact position.
 IEnumerator Present(ChessSnapshot reply){
  var events=reply.events??new ChessEvent[0];Transform moving=null;
  foreach(var e in events){
   EventRaised?.Invoke(e);
   switch(e.t){
    case "capture":{if(Board.Pieces.TryGetValue(e.b,out var gone)){game.Audio.ChessCapture();Board.Flash(e.b,Color.red);Destroy(gone.gameObject);Board.Pieces.Remove(e.b);}break;}
    case "move":{
     Transform piece=moving;if(piece==null&&!Board.Pieces.TryGetValue(e.a,out piece))break;
     // Castling slides the rook too; King Reversal slides the two pieces through each other's square.
     Transform partner=null;Vector3 partnerEnd=default;
     if(e.s=="castle"){string rank=e.a.Substring(1,1);string rf=e.b[0]=='g'?"h"+rank:"a"+rank,rt=e.b[0]=='g'?"f"+rank:"d"+rank;if(Board.Pieces.TryGetValue(rf,out partner))partnerEnd=ChessBoardView.Position(rt)+Vector3.up*.006f;}
     else if(e.s=="kingReversal"){if(Board.Pieces.TryGetValue(e.b,out partner))partnerEnd=ChessBoardView.Position(e.a)+Vector3.up*.006f;}
     Vector3 start=piece.localPosition,end=ChessBoardView.Position(e.b)+Vector3.up*.006f,pStart=partner?partner.localPosition:default;game.Audio.ChessSlide();
     for(float t=0;t<1;t+=Time.deltaTime/.52f){float k=Mathf.SmoothStep(0,1,t);piece.localPosition=Vector3.Lerp(start,end,k)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.045f);if(partner)partner.localPosition=Vector3.Lerp(pStart,partnerEnd,k)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.03f);yield return null;}
     piece.localPosition=end;if(partner)partner.localPosition=partnerEnd;moving=piece;game.Audio.ChessSettle();break;}
    case "captureBlocked":{if(Board.Pieces.TryGetValue(e.a,out var atk)){Vector3 s0=atk.localPosition,s1=Vector3.Lerp(s0,ChessBoardView.Position(e.b),.55f);for(float t=0;t<1;t+=Time.deltaTime/.2f){atk.localPosition=Vector3.Lerp(s0,s1,t);yield return null;}for(float t=0;t<1;t+=Time.deltaTime/.25f){atk.localPosition=Vector3.Lerp(s1,s0,t);yield return null;}}Board.Flash(e.b,Color.cyan);game.Audio.LockClick();break;}
    case "hazardWarning":Board.Flash(e.a,new Color(1,.6f,.1f));break;
    case "hazardActive":Board.Flash(e.a,Color.red);game.Audio.ChessCapture();yield return new WaitForSeconds(.25f);break;
    case "hazardDestroyed":{if(Board.Pieces.TryGetValue(e.a,out var lost)){Destroy(lost.gameObject);Board.Pieces.Remove(e.a);}yield return new WaitForSeconds(.2f);break;}
    case "hazardBlocked":Board.Flash(e.a,Color.cyan);game.Audio.Chime();break;
   }
  }
  Snapshot=reply;Board.ShowPosition(Snapshot);Board.ShowResources(Snapshot,PendingTool);Selected=null;
  // Non-story boards let the engine drive the road: the physical car follows its lane and speed.
  if(challenge!=null&&challenge.autoVehicle&&game.Security!=null){game.Motion.lane=Mathf.Clamp(reply.lane,0,2);game.Security.SetCruise(Mathf.Clamp(reply.speed,40,135));}
 }
 // AI Simulation: both sides are AI controllers on the physical tray.
 public IEnumerator AutoGame(string whiteLevel,string blackLevel){
  while(Active&&Snapshot!=null&&!Snapshot.over){
   while(Busy||game.Paused)yield return null;
   yield return new WaitForSeconds(.6f);ChessMove w=null;Busy=true;yield return AiTurn(true,whiteLevel,m=>w=m);
   if(Snapshot.over)break;
   yield return new WaitForSeconds(.5f);ChessMove b=null;yield return AiTurn(false,blackLevel,m=>b=m);Busy=false;
  }
  Busy=false;if(Snapshot!=null&&Snapshot.over)Ended?.Invoke(Snapshot.winner,Snapshot.reason);
 }
 public void StartAutoGame(string whiteLevel,string blackLevel){StartCoroutine(AutoGame(whiteLevel,blackLevel));}
 public void ReplacePosition(string fen){if(!Active||Busy)return;try{var s=Send("query");var q=new ChessRequest{op="query",fen=fen,stateJson=stateJson};RoadRules.Configure(q);s=ChessEngine.Send(q);if(!string.IsNullOrEmpty(s.stateJson))stateJson=s.stateJson;Snapshot=s;}catch(Exception e){Debug.LogException(e);return;}
  Selected=null;pendingTo=null;Board.ShowPosition(Snapshot);Board.status.text="CHECKSUM MISMATCH / RECORDED POSITION CHANGED";}
 public void RestoreIntegrity(){Retry();game.Audio.Chime();}
 public void Retry(){if(!Active||Busy)return;RoadRules.Sync(game);try{StartSession();}catch(Exception e){Debug.LogException(e);return;}Board.ShowPosition(Snapshot);Board.ShowResources(Snapshot,"");PlayerMoves=0;cyberSeen.Clear();Selected=null;pendingTo=null;overStep="";PendingTool="";retry=false;Instruction=challenge.guidanceText;UpdateStatus();}
 void UpdateStatus(){
  string extra="";
  if(Snapshot!=null&&Snapshot.state!=null&&(challenge.cyber||challenge.cards||challenge.hazards))extra="  /  TURN "+Snapshot.state.turnNo;
  Board.status.text=challenge.maxMoves>0?challenge.title+" / "+PlayerMoves+" OF "+challenge.maxMoves+" MOVES":challenge.title+extra;
 }
 public void Hint(){if(!Active||Busy)return;game.Player.FocusAngles(0,61);if(retry){Retry();return;}if(challenge.road){RoadRules.Sync(game);RefreshMoves();}
  ChessMove hint=null;
  if(challenge.objectiveType==ChessObjective.CaptureTarget)hint=Moves.FirstOrDefault(m=>m.captured==challenge.targetPiece);
  else if(challenge.objectiveType==ChessObjective.DeliverCheck)hint=Moves.FirstOrDefault(m=>m.san!=null&&(m.san.Contains("+")||m.san.Contains("#")));
  if(hint==null&&challenge.hintMove.Length==4)hint=Moves.FirstOrDefault(m=>m.from==challenge.hintMove.Substring(0,2)&&m.to==challenge.hintMove.Substring(2,2));
  if(hint==null&&game.Security.Corrupted){Instruction="The queen moved while you looked away. Tap the right speaker to restore the recorded position.";game.Player.FocusControl(game.Registry.Get("right-speaker").transform);game.Dialogue.Say(Instruction);return;}
  if(hint==null&&challenge.objectiveType==ChessObjective.Checkmate){try{var plan=Send("ai",q=>{q.difficulty="normal";q.budgetMs=120;q.noAbilities=true;});if(plan.ok&&plan.plan!=null&&plan.plan.action!=null&&!string.IsNullOrEmpty(plan.plan.action.from))hint=Moves.FirstOrDefault(m=>m.from==plan.plan.action.from&&m.to==plan.plan.action.to&&m.kind==plan.plan.action.kind);}catch(Exception){}}
  if(hint==null)hint=Moves.FirstOrDefault();
  if(hint==null){Instruction="No move in these files. Wait for the road to change, or retract and retry.";return;}
  TapSquare(hint.from);Instruction=ChessBoardView.Name(hint.piece)+" "+hint.from+" → "+hint.to+". "+challenge.hintText;game.Dialogue.Say(Instruction);
 }
 // The road reaches into the game: hazard warnings generated by a road event (threat director, police).
 public void ExternalHazard(int count){if(!Active||Busy||Snapshot==null||Snapshot.over||!challenge.hazards)return;try{var r=Send("roadEvent",q=>{q.count=count;q.label="road";});if(r.ok){Snapshot=r;Board.ShowMarks(Snapshot);foreach(var e in r.events??new ChessEvent[0]){EventRaised?.Invoke(e);if(e.t=="hazardWarning")Board.Flash(e.a,new Color(1,.6f,.1f));}}}catch(Exception ex){Debug.LogWarning(ex.Message);}}
 public string ResultText(){if(Snapshot==null||!Snapshot.over)return "";return Snapshot.winner=="w"?"CHECKMATE · WHITE WINS":Snapshot.winner=="b"?"CHECKMATE · THE CAR WINS":Snapshot.reason=="stalemate"?"STALEMATE · DRAW":"DRAW";}
 public void Cancel(){if(!Active||Busy||retracting||(challenge!=null&&challenge.noRetry&&!challenge.persistent&&!(Snapshot!=null&&Snapshot.over)))return;if(Spectating)return;StartCoroutine(Retract());}
 // Forcibly close the tray (endings, mode changes). Safe to call at any time.
 public void Abort(){if(!Active)return;StopAllCoroutines();Busy=false;applying=false;netAwait=false;assistBusy=false;AwaitingCar=false;retracting=false;StartCoroutine(Retract());}
 IEnumerator Retract(){retracting=true;Busy=true;if(NetMode&&!Spectating)Net.SendChessEnd();
  if(challenge!=null&&challenge.persistent){if(Snapshot!=null&&!Snapshot.over){challenge.savedFen=Snapshot.fen;challenge.savedState=stateJson;}else{challenge.savedFen="";challenge.savedState="";}}game.Audio.ControlSound("rear-seat",ControlAction.Seat,false);for(float t=1;t>0;t-=Time.deltaTime/1.4f){Board.Pose(t,game.Seats.Offset.x);yield return null;}Board.SetVisible(false);Active=false;Busy=false;retry=false;Selected=null;pendingTo=null;overStep="";PendingTool="";timed=false;RoadRules.Enabled=false;game.State.Enter(returnState);if(!Spectating)game.Player.FocusAngles(savedYaw,savedPitch);retracting=false;AwaitingCar=false;Instruction="";game.Dialogue.Say(game.State.Has(challenge.rewardEvent)?game.Security.Result:"Authentication paused. Explore the cabin or retry from its control.");}
 public static string Explain(string p){switch(p){case "p":return "Pawn: forward; captures diagonally.";case "n":return "Knight: moves in an L and jumps pieces.";case "b":return "Bishop: diagonal.";case "r":return "Rook: horizontal or vertical.";case "q":return "Queen: straight or diagonal.";default:return "King: one square. Protect it.";}}
}
}
