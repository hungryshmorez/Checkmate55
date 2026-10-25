using System;
using System.Collections.Generic;
namespace Checkmate55.Core {
// The car takes a system away (closes a window, creeps a seat, kills the climate, takes over the radio ...).
// The passenger wins it back by pressing the same physical control repeatedly: a short physical tug-of-war.
public sealed class ContestBoard {
 public sealed class Contest {public string System;public int Required=3,Count;public float Last;public float Opened;}
 readonly Dictionary<string,Contest> open=new Dictionary<string,Contest>();
 public readonly HashSet<string> Freed=new HashSet<string>();
 public float Window=4f;
 public int Open=>open.Count;
 public bool IsOpen(string system)=>open.ContainsKey(system);
 public event Action<string> Won;
 public void Begin(string system,float now,int required=3){if(open.ContainsKey(system))return;open[system]=new Contest{System=system,Required=required,Opened=now,Last=now};}
 // Returns true when this press won the contest. Presses further apart than Window do not accumulate.
 public bool Press(string system,float now){
  if(!open.TryGetValue(system,out var c))return false;
  if(c.Count>0&&now-c.Last>Window)c.Count=0;
  c.Count++;c.Last=now;
  if(c.Count>=c.Required){open.Remove(system);Freed.Add(system);Won?.Invoke(system);return true;}
  return false;
 }
 public int Progress(string system)=>open.TryGetValue(system,out var c)?c.Count:0;
 public void Cancel(string system)=>open.Remove(system);
 public float Age(string system,float now)=>open.TryGetValue(system,out var c)?now-c.Opened:0;
 public IEnumerable<string> OpenSystems=>open.Keys;
}
}
