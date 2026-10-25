using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Checkmate55.Core;
namespace Checkmate55.Tests {
// The room server (Tools/Server/room.cjs) and the C# Core must agree on every rule number: both read test/vectors.json.
[TestFixture] public sealed class NetParityTests {
 static JsonElement Load(){
  var dir=AppContext.BaseDirectory;
  for(int i=0;i<10&&dir!=null;i++,dir=Path.GetDirectoryName(dir)){var p=Path.Combine(dir,"Tools","Server","test","vectors.json");if(File.Exists(p))return JsonDocument.Parse(File.ReadAllText(p)).RootElement;var q=Path.Combine(dir,"..","Server","test","vectors.json");if(File.Exists(q))return JsonDocument.Parse(File.ReadAllText(q)).RootElement;}
  throw new Exception("vectors.json not found");
 }
 [Test] public void AuthorityCostsAndCooldownsMatchTheServer(){
  var a=Load().GetProperty("authority");int count=0;
  foreach(var prop in a.GetProperty("actions").EnumerateObject()){
   var act=(CarAction)Enum.Parse(typeof(CarAction),prop.Name);
   Assert.Eq((float)prop.Value[0].GetDouble(),SystemAuthority.Cost(act),prop.Name+" cost");Assert.Eq((float)prop.Value[1].GetDouble(),SystemAuthority.Cooldown(act),prop.Name+" cooldown");count++;
  }
  Assert.Eq(Enum.GetValues(typeof(CarAction)).Length,count,"every CarAction is in the vectors");
  var s=new SystemAuthority();Assert.Eq((float)a.GetProperty("start").GetDouble(),s.Value);Assert.Eq((float)a.GetProperty("max").GetDouble(),SystemAuthority.Max);Assert.Eq((float)a.GetProperty("regenPerSecond").GetDouble(),SystemAuthority.RegenPerSecond);
 }
 [Test] public void CoopOperatorTableMatchesTheServer(){
  foreach(var v in Load().GetProperty("coop").EnumerateArray()){
   var mode=v.GetProperty("mode").GetString()=="shared"?CoopControl.Shared:v.GetProperty("mode").GetString()=="alternating"?CoopControl.Alternating:CoopControl.TacticalAssist;
   var t=new CoopTurn(mode,v.GetProperty("players").GetInt32());for(int i=0;i<v.GetProperty("turn").GetInt32();i++)t.NextTurn();
   Assert.Eq(v.GetProperty("operator").GetInt32(),t.ChessOperator,v.ToString());Assert.Eq(v.GetProperty("supportOperator").GetInt32(),t.SupportOperator,v.ToString());
   foreach(var c in v.GetProperty("confirm").EnumerateObject())Assert.Eq(c.Value.GetBoolean(),t.CanConfirmChess(int.Parse(c.Name)),v.ToString()+" p"+c.Name);
  }
 }
}
}
