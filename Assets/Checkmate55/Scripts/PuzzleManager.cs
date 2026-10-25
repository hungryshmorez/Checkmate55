using System;
using System.Collections.Generic;
namespace Checkmate55 {
public sealed class PuzzleDefinition {
 public string Id,CompletionEvent,Reward;
 public string[] Prerequisites;
 public int CurrentStep {get;internal set;}
 public bool Solved {get;internal set;}
 public Action ResetBehavior;
 public PuzzleDefinition(string id,string[] prerequisites,string reward=null){Id=id;Prerequisites=prerequisites;Reward=reward;CompletionEvent="puzzle."+id;}
 public bool Available(GameState state){foreach(var p in Prerequisites)if(!state.Has(p))return false;return true;}
}
public sealed class PuzzleManager {
 public readonly Dictionary<string,PuzzleDefinition> Definitions=new Dictionary<string,PuzzleDefinition>();
 public readonly Dictionary<string,bool> Solved=new Dictionary<string,bool>();
 public event Action<PuzzleDefinition> Completed;
 public PuzzleManager(){
  Register(new PuzzleDefinition("passenger-auth",new[]{"lockdown","tested-door","tested-window","glovebox-interest","authentication-known","clue.note","clue.service","trim-inspected"},"authentication-ready"));
  foreach(var id in new[]{"climate-pattern","seat-position","speaker-clue","usb-device","overhead-sequence","diagnostics","window-override","route-override","previous-passenger","escape-paths"})Register(new PuzzleDefinition(id,new[]{"future."+id}));
 }
 public void HandleSecurityEvent(string id,GameState state){if(id=="CHESS_CHALLENGE_COMPLETED")state.Flag("glovebox-authenticated");}
 public void Register(PuzzleDefinition puzzle){Definitions[puzzle.Id]=puzzle;}
 public bool Advance(string id,int step,GameState state){if(!Definitions.TryGetValue(id,out var p)||p.Solved||!p.Available(state))return false;p.CurrentStep=step;return true;}
 public void Resolve(string id,GameState state){if(!Definitions.TryGetValue(id,out var p)||p.Solved||!p.Available(state))return;p.Solved=true;p.CurrentStep++;Solved[id]=true;state.Flag(p.CompletionEvent);if(!string.IsNullOrEmpty(p.Reward))state.Flag(p.Reward);Completed?.Invoke(p);}
 public void Reset(string id,GameState state){if(!Definitions.TryGetValue(id,out var p))return;p.Solved=false;p.CurrentStep=0;Solved.Remove(id);state.Flags.Remove(p.CompletionEvent);if(p.Reward!=null)state.Flags.Remove(p.Reward);p.ResetBehavior?.Invoke();}
}
}
