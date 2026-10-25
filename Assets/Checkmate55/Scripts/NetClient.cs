using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
#if !(UNITY_WEBGL && !UNITY_EDITOR)
using System.Net.WebSockets;
using System.Text;
using System.Threading;
#endif
namespace Checkmate55 {
// ---- wire DTOs. One superset message: JsonUtility ignores fields a message does not carry.
[Serializable] public class NetMember {public string slot="",name="";public bool host;}
[Serializable] public class NetTeam {public int turn,@operator,support,players;public bool locked,card,cyber;public string mode="";}
[Serializable] public class NetThreat {public string kind="",arg="",line="";public float strength=1;}
[Serializable] public class NetMsg {
 public string t="",id="",code="",slot="",mode="",coop="",skill="",flag="",by="",reason="",@ref="",action="",kind="",who="",fen="",turn="",winner="",challengeId="",stateJson="",ending="",takeover="",why="",error="";
 public int proto,seq,@operator,wait;public float authority;public bool host,over,closed,timeout,resume;public uint seed;
 public NetMember[] roster;public NetThreat threat;public NetTeam team;public ChessRequest[] ops;
}
[Serializable] public class NetOut {public string t="";public string name="",code="",mode="",coop="",skill="",role="",flag="",id="",action="",challengeId="",ending="";public int seed;public bool persist;public ChessRequest req;public NetThreat threat;public double at;}
public enum NetStatus { Offline, Connecting, Connected, InRoom, Started, Failed }
// Talks to Tools/Server (room.cjs). WebGL uses the browser WebSocket through CheckmateBrowser.jslib; the Editor and desktop use ClientWebSocket.
public sealed class NetClient : MonoBehaviour {
#if UNITY_WEBGL && !UNITY_EDITOR
 [DllImport("__Internal")] static extern void CM55_NetOpen(string url);
 [DllImport("__Internal")] static extern int CM55_NetState();
 [DllImport("__Internal")] static extern IntPtr CM55_NetError();
 [DllImport("__Internal")] static extern int CM55_NetSend(string text);
 [DllImport("__Internal")] static extern IntPtr CM55_NetPoll();
 [DllImport("__Internal")] static extern void CM55_NetFree(IntPtr p);
 [DllImport("__Internal")] static extern void CM55_NetClose();
#else
 ClientWebSocket socket;CancellationTokenSource cancel;readonly ConcurrentQueue<string> inbound=new ConcurrentQueue<string>();readonly ConcurrentQueue<string> outbound=new ConcurrentQueue<string>();volatile int nativeState;
#endif
 public const string DefaultUrl="ws://localhost:8080";
 // Put your room server here if the project importer drops Assets/Resources/server.txt, e.g. "wss://checkmate55-rooms.onrender.com". Empty = use server.txt, then the default.
 public const string BuiltInServer="";
 public NetStatus Status{get;private set;}=NetStatus.Offline;
 public string Error{get;private set;}="";
 public string Code{get;private set;}="";public string Slot{get;private set;}="";public bool Host{get;private set;}
 public string Mode="",Coop="";public string Skill="smart";public uint Seed;public float Authority=60;
 public NetMember[] Roster=new NetMember[0];
 public readonly Queue<NetMsg> Inbox=new Queue<NetMsg>();           // everything the game has not yet consumed (chess ops are drained by ChessController)
 public event Action<NetMsg> Received;
 float pingAt,lastSeen;
 public static string ServerUrl{get{
  string url=PlayerPrefs.GetString("cm55.server","");
#if UNITY_WEBGL && !UNITY_EDITOR
  if(string.IsNullOrEmpty(url)){var q=Application.absoluteURL??"";int i=q.IndexOf("server=",StringComparison.Ordinal);if(i>=0){string rest=q.Substring(i+7);int e=rest.IndexOfAny(new[]{'&','#'});url=Uri.UnescapeDataString(e>=0?rest.Substring(0,e):rest);}}
#endif
  if(string.IsNullOrEmpty(url)){var cfg=Resources.Load<TextAsset>("server");if(cfg!=null)foreach(var line in cfg.text.Split('\n')){var t=line.Trim();if(t.StartsWith("ws://")||t.StartsWith("wss://")){url=t;break;}}}
  if(string.IsNullOrEmpty(url)&&BuiltInServer.Length>0)url=BuiltInServer;
  return string.IsNullOrEmpty(url)?DefaultUrl:url;}}
 public static NetClient Create(GameObject owner)=>owner.GetComponent<NetClient>()??owner.AddComponent<NetClient>();
 public bool Connected=>Status==NetStatus.Connected||Status==NetStatus.InRoom||Status==NetStatus.Started;
 public bool IsPassenger=>Slot=="A"||Slot=="B"||Slot=="P";
 public bool IsCar=>Slot=="C";
 // ---------------------------------------------------------------------------------------------------------------- connect
 public void Connect(string url=null,string playerName="Passenger"){
  Disconnect();Error="";Status=NetStatus.Connecting;lastSeen=Time.unscaledTime;
  try{
#if UNITY_WEBGL && !UNITY_EDITOR
   CM55_NetOpen(url??ServerUrl);
#else
   cancel=new CancellationTokenSource();socket=new ClientWebSocket();var token=cancel.Token;var target=new Uri(url??ServerUrl);nativeState=1;
   var s=socket;
   System.Threading.Tasks.Task.Run(async()=>{
    try{
     await s.ConnectAsync(target,token);nativeState=2;
     var buffer=new byte[16384];var sb=new StringBuilder();
     var sender=System.Threading.Tasks.Task.Run(async()=>{while(!token.IsCancellationRequested&&s.State==WebSocketState.Open){if(outbound.TryDequeue(out var o)){var bytes=Encoding.UTF8.GetBytes(o);await s.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,token);}else await System.Threading.Tasks.Task.Delay(8);}});
     while(!token.IsCancellationRequested&&s.State==WebSocketState.Open){
      sb.Length=0;WebSocketReceiveResult r;
      do{r=await s.ReceiveAsync(new ArraySegment<byte>(buffer),token);if(r.MessageType==WebSocketMessageType.Close){nativeState=0;return;}sb.Append(Encoding.UTF8.GetString(buffer,0,r.Count));}while(!r.EndOfMessage);
      inbound.Enqueue(sb.ToString());
     }
    }catch(Exception){}
    nativeState=0;
   });
#endif
  }catch(Exception e){Fail(e.Message);return;}
  hello=playerName;
 }
 string hello="Passenger";
 public void Disconnect(){
  try{
#if UNITY_WEBGL && !UNITY_EDITOR
   CM55_NetClose();
#else
   if(cancel!=null){cancel.Cancel();}try{socket?.Dispose();}catch(Exception){}socket=null;cancel=null;nativeState=0;
   string dump;while(inbound.TryDequeue(out dump)){}while(outbound.TryDequeue(out dump)){}
#endif
  }catch(Exception){}
  Status=NetStatus.Offline;Code="";Slot="";Host=false;Inbox.Clear();
 }
 void OnDestroy(){Disconnect();}
 void Fail(string why){
  string detail="";
#if UNITY_WEBGL && !UNITY_EDITOR
  try{IntPtr p=CM55_NetError();if(p!=IntPtr.Zero){try{detail=Marshal.PtrToStringAnsi(p);}finally{CM55_NetFree(p);}}}catch(Exception){}
#endif
  Status=NetStatus.Failed;Error=string.IsNullOrEmpty(detail)?why:why+" - "+detail;
 }
 // ---------------------------------------------------------------------------------------------------------------- pump
 void Update(){
  if(Status==NetStatus.Offline||Status==NetStatus.Failed)return;
  int s;
#if UNITY_WEBGL && !UNITY_EDITOR
  s=CM55_NetState();
#else
  s=nativeState;
#endif
  if(Status==NetStatus.Connecting){
   if(s==2){Status=NetStatus.Connected;Send(new NetOut{t="hello",name=hello});lastSeen=Time.unscaledTime;}
   else if(s==0&&Time.unscaledTime-lastSeen>8f){Fail("could not reach "+ServerUrl);return;}
  }else if(s==0){Fail("disconnected");return;}
  // drain a bounded number of frames per update
  for(int i=0;i<64;i++){
   string text=null;
#if UNITY_WEBGL && !UNITY_EDITOR
   IntPtr p=CM55_NetPoll();if(p==IntPtr.Zero)break;try{text=Marshal.PtrToStringAnsi(p);}finally{CM55_NetFree(p);}
#else
   if(!inbound.TryDequeue(out text))break;
#endif
   NetMsg m=null;try{m=JsonUtility.FromJson<NetMsg>(text);}catch(Exception){}
   if(m==null||string.IsNullOrEmpty(m.t))continue;
   lastSeen=Time.unscaledTime;Absorb(m);
  }
  if(Time.unscaledTime-pingAt>10f){pingAt=Time.unscaledTime;Send(new NetOut{t="ping"});}
 }
 void Absorb(NetMsg m){
  switch(m.t){
   case "room":Code=m.code;Slot=m.slot;Mode=m.mode;Coop=m.coop;Skill=m.skill;Roster=m.roster??Roster;Status=NetStatus.InRoom;Host=Roster!=null&&Array.Exists(Roster,r=>r.slot==Slot&&r.host);break;
   case "roster":Roster=m.roster??Roster;Host=Array.Exists(Roster,r=>r.slot==Slot&&r.host);break;
   case "start":Seed=m.seed;Slot=m.slot;Host=m.host;Mode=m.mode;Coop=m.coop;Skill=m.skill;Authority=m.authority;Roster=m.roster??Roster;Status=NetStatus.Started;break;
   case "carAction":Authority=m.authority;break;
   case "left":if(m.host)Host=true;break;
   case "deny":if(m.reason=="rate-limited"||m.reason=="idle-timeout")Error=m.reason;break;
  }
  Inbox.Enqueue(m);Received?.Invoke(m);
 }
 public bool Send(NetOut o){
  string json=JsonUtility.ToJson(o);
#if UNITY_WEBGL && !UNITY_EDITOR
  return CM55_NetSend(json)!=0;
#else
  if(nativeState!=2)return false;outbound.Enqueue(json);return true;
#endif
 }
 // ---------------------------------------------------------------------------------------------------------------- verbs
 public void CreateRoom(string mode,string coop,string role,string skill){Send(new NetOut{t="create",mode=mode,coop=coop,role=role,skill=skill});}
 public void JoinRoom(string code){Send(new NetOut{t="join",code=code});}
 public void StartMatch(){Send(new NetOut{t="start",seed=(int)(DateTime.UtcNow.Ticks&0x3fffffff)});}
 public void SendFlag(string flag){Send(new NetOut{t="flag",flag=flag});}
 public void SendControl(string id){Send(new NetOut{t="ctl",id=id});}
 public void SendThreat(NetThreat threat){Send(new NetOut{t="threat",threat=threat});}
 public void SendCarAction(string action){Send(new NetOut{t="carAction",action=action});}
 public void SendEnding(string ending){Send(new NetOut{t="ending",ending=ending});}
 public void SendChessBegin(string challengeId,ChessRequest begin,bool persist=false){Send(new NetOut{t="chessBegin",challengeId=challengeId,req=begin,persist=persist});}
 public void SendChess(ChessRequest req){Send(new NetOut{t="chess",req=req});}
 public void SendChessEnd(){Send(new NetOut{t="chessEnd"});}
 public void RequestResync(){Send(new NetOut{t="resync"});}
}
}
