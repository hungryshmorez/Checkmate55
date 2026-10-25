using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
namespace Checkmate55.Core {
// The micro-protocols a passenger needs once the guide ring is not enough: tune a pattern into the climate display, tap a rhythm on the
// dome light, isolate a speaker, read a mile marker through clear glass. An AI passenger is given nothing a human passenger would not
// have: the HINT line, the notes they have read (pattern numbers are PARSED from the maintenance card text), and what the displays show.
// It returns null when the ring alone is enough, so the caller falls back to "touch the ringed control".
public sealed class StoryAgent {
 public float Pace{get;private set;}                    // minimum seconds before the next decision
 string beat="";int phase;float wait;int counter;readonly List<string> queue=new List<string>();
 static readonly string[] Explore={"climate","left-window","right-window","armrest","guidance-1","left-handle"};
 void Enter(string id){beat=id;phase=0;wait=0;counter=0;queue.Clear();}
 public static int[] ParseClimate(IEnumerable<string> notes,out int fan){
  fan=-1;foreach(var n in notes){var m=Regex.Match(n,@"(\d+)\s*[·.\-,]\s*(\d+)\s*[·.\-,]\s*(\d+)\s*(?:→|->)\s*FAN\s*(\d)",RegexOptions.IgnoreCase);if(m.Success){fan=int.Parse(m.Groups[4].Value);return new[]{int.Parse(m.Groups[1].Value),int.Parse(m.Groups[2].Value),int.Parse(m.Groups[3].Value)};}}
  return null;
 }
 static readonly string[] Words={"zero","one","two","three","four","five","six","seven","eight","nine"};
 // "LIGHT: two · pause · three"  ->  [2,3]
 public static int[] ParseLight(IEnumerable<string> notes){
  foreach(var n in notes){var m=Regex.Match(n,@"LIGHT:\s*([^\n]+)",RegexOptions.IgnoreCase);if(!m.Success)continue;
   var groups=new List<int>();foreach(var tok in m.Groups[1].Value.Split(new[]{'·','-',',','.'},StringSplitOptions.RemoveEmptyEntries)){var t=tok.Trim().ToLowerInvariant();int idx=Array.IndexOf(Words,t);if(idx>0)groups.Add(idx);else{int d;if(int.TryParse(t,out d)&&d>0)groups.Add(d);}}
   if(groups.Count>0)return groups.ToArray();}
  return null;
 }
 public static int ParsePreset(string text){var m=Regex.Match(text??"",@"preset\s*(\d)",RegexOptions.IgnoreCase);return m.Success?int.Parse(m.Groups[1].Value):0;}
 public Intent Next(PassengerView v,int actor){
  if(string.IsNullOrEmpty(v.BeatId))return null;
  if(v.BeatId!=beat)Enter(v.BeatId);
  Pace=.7f;float now=v.Time;
  if(now<wait)return Intent.Idle(actor);
  switch(v.BeatId){
   case "ride":return Ride(v,actor,now);
   case "climate":return Climate(v,actor,now);
   case "light":return Light(v,actor,now);
   case "radio":return Radio(v,actor,now);
   case "marker":return Marker(v,actor,now);
  }
  return null;
 }
 // Explore five different things, then wait for the ride to be long enough, then REQUEST STOP.
 Intent Ride(PassengerView v,int actor,float now){
  if(counter<Explore.Length&&counter<5){string id=Explore[counter++];wait=now+1.6f;return Intent.Touch(actor,id);}
  if(now<78f){wait=now+2f;return Intent.Idle(actor);}
  wait=now+8f;return Intent.Touch(actor,"request-stop");
 }
 Intent Climate(PassengerView v,int actor,float now){
  int fan;var pattern=ParseClimate(v.Notes,out fan);if(pattern==null)return null;
  if(phase<pattern.Length){
   int target=pattern[phase];
   if(v.Temp==target){phase++;wait=now+1.6f;return Intent.Idle(actor);}          // let it settle, then the next temperature
   wait=now+.35f;return Intent.Touch(actor,v.Temp<target?"climate-plus":"climate-minus");
  }
  if(v.PatternReady||phase==pattern.Length){
   if(!v.PatternReady&&phase==pattern.Length+1){phase=0;return Intent.Idle(actor);}   // the pattern expired: start again
   if(v.Fan==fan&&v.PatternReady){wait=now+.8f;return Intent.Touch(actor,"climate-fan");}  // step off the target so pressing it lands on it
   wait=now+.7f;phase=pattern.Length;return Intent.Touch(actor,"climate-fan");
  }
  return Intent.Idle(actor);
 }
 // Press the dome light in bursts: each burst's presses less than a second apart, bursts separated by a clear pause.
 Intent Light(PassengerView v,int actor,float now){
  var groups=ParseLight(v.Notes);if(groups==null)return null;
  if(phase>=groups.Length){phase=0;counter=0;wait=now+8f;return Intent.Idle(actor);}    // finished a try: give the flags time to register, then try again
  if(counter<groups[phase]){counter++;wait=now+(counter==groups[phase]?(phase==groups.Length-1?2.5f:2.0f):.55f);if(counter==groups[phase]){phase++;counter=0;}return Intent.Touch(actor,"overhead-light");}
  return Intent.Idle(actor);
 }
 // Tune the preset named by the hint, then isolate the rear-left speaker.
 Intent Radio(PassengerView v,int actor,float now){
  int preset=ParsePreset(v.Hint);if(preset==0)preset=ParsePreset(string.Join(" ",v.Notes));if(preset==0)return null;
  wait=now+.5f;
  if(v.Muted)return Intent.Touch(actor,"radio-mute");
  if(v.Volume<=.2f)return Intent.Touch(actor,"radio-vol-up");
  if(v.Station!=preset)return Intent.Touch(actor,"radio-p"+preset);
  if(v.Balance>-.7f)return Intent.Touch(actor,"radio-bal-l");
  if(v.Fader>-.7f)return Intent.Touch(actor,"radio-fade-r");
  wait=now+2f;return Intent.Idle(actor);                                                  // listen
 }
 // Read a mile marker: the glass must be clear and the marker must be in view (they pass on the right).
 Intent Marker(PassengerView v,int actor,float now){
  if(v.TintAlpha>.25f){wait=now+.6f;return Intent.Touch(actor,counter++%2==0?"left-tint":"right-tint");}
  // Markers stand 7.9 m to the right of the road: a heading of about 40 degrees right keeps every approaching marker inside the view cone.
  wait=now+2f;phase++;
  return Intent.LookAt(actor,40,0);
 }
}
}
