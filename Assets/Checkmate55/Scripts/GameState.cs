using System;
using System.Collections.Generic;
namespace Checkmate55 {
public enum RideState { BOOT, BOARDING, NORMAL_RIDE, LOCKDOWN, EXPLORATION, CHESS_CHALLENGE, ROAD_CHESS, ENDGAME, ENDING }
public sealed class GameState {
 public RideState Current { get; private set; } = RideState.BOOT;
 public readonly HashSet<string> Flags = new HashSet<string>();
 public event Action<RideState> Changed;
 public event Action<string> Discovered;
 public void Enter(RideState state) { Current=state; Changed?.Invoke(state); }
 public void Flag(string id) { if(Flags.Add(id)) Discovered?.Invoke(id); }
 public bool Has(string flag) => string.IsNullOrEmpty(flag)||Flags.Contains(flag);
}
}
