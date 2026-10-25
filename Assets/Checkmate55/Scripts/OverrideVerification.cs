#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Checkmate55.Core;
namespace Checkmate55 {
// Editor play-mode check for AI Simulation: the AI passenger plays the whole story against the AI car at accelerated speed. Writes a
// progress log to docs/OVERRIDE_SIM.txt and stops play mode when an ending is reached (PASS) or the story clock passes the limit (FAIL).
public sealed class OverrideVerification : MonoBehaviour {
 OverrideDirector d;float nextWrite,limit=3600,finishedAt=-1;bool done;readonly List<string> header=new List<string>();
 void Start(){d=GetComponent<OverrideDirector>();System.IO.Directory.CreateDirectory("docs");}
 void Update(){
  if(d==null||d.WaitingForMode||done)return;
  if(Time.realtimeSinceStartup>nextWrite){nextWrite=Time.realtimeSinceStartup+5;Write(null);}
  if(d.Final!=null&&d.Final.ScreenReady){if(finishedAt<0)finishedAt=Time.realtimeSinceStartup;if(Time.realtimeSinceStartup-finishedAt>3){done=true;Write("PASS: reached ending "+d.Final.Ending+" at "+Mathf.RoundToInt(d.StoryClock)+"s of story time ("+Records.Clock(d.Final.StoryMs)+")");Stop();}}
  else if(d.StoryClock>limit){done=true;Write("FAIL: no ending after "+Mathf.RoundToInt(d.StoryClock)+"s of story time. Last beat: "+(d.Story.Current(d.Has)!=null?d.Story.Current(d.Has).Id:"-"));Stop();}
 }
 void Write(string result){
  var lines=new List<string>{"OVERRIDE AI SIMULATION","mode: "+d.Mode.Mode+"  speed x"+d.Roles.SimSpeed,"story clock: "+Mathf.RoundToInt(d.StoryClock)+"s"};
  var beat=d.Story.Current(d.Has);lines.Add("current beat: "+(beat!=null?beat.Id:"(none)"));lines.Add("");lines.AddRange(d.Roles.SimLog);
  if(result!=null){lines.Add("");lines.Add(result);Debug.Log("OVERRIDE SIM "+result);}
  System.IO.File.WriteAllLines("docs/OVERRIDE_SIM.txt",lines);
 }
 void Stop(){UnityEditor.EditorApplication.isPlaying=false;}
}
}
#endif
