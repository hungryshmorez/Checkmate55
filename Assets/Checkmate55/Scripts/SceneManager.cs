using UnityEngine;
namespace Checkmate55 {
public sealed class SceneManager : MonoBehaviour {
 public GameState State {get;private set;}=new GameState();
 public DialogueManager Dialogue {get;private set;}=new DialogueManager();
 public TutorialManager Tutorial {get;private set;}=new TutorialManager();
 public SaveManager Save {get;private set;}=new SaveManager();
 public ObjectRegistry Registry;public InteractionSystem Interactions;public PlayerCamera Player;public VehicleMotion Motion;public AudioManager Audio;public CabinUI UI;public PerformanceManager Performance;
 public VehicleSecurity Security;public RideDirector Ride;public ChessController Chess;public SeatSwitcher Seats;
 public PuzzleManager Puzzles {get;private set;}=new PuzzleManager();
 public InventoryState Inventory {get;private set;}=new InventoryState();
 public VehicleAI AI {get;private set;}
 public bool Paused {get;private set;}
 // OVERRIDE: run-level knobs consumed by the chess layer and the mode director.
 public int RunSeed {get;set;}
 public bool AllRulesKnown {get;set;}
 public bool AllowChessWithoutAuth {get;set;}
 public OverrideDirector Override {get;private set;}
 void Start(){Registry.Initialize();Save.ClearProgress();AI=new VehicleAI(this);State.Discovered+=OnDiscovered;Performance.Apply(Save.Quality);AudioListener.volume=Save.Volume;Ride.Initialize(this);Seats.Initialize(this);Chess.Initialize(this);Security.Initialize(this);GetComponent<WorldTextDepth>().Initialize();Tutorial.Initialize(this);Chess.Completed+=OnChessCompleted;
  RunSeed=(int)(System.DateTime.UtcNow.Ticks%9973);
  // Everything added by CHECKMATE 55: OVERRIDE is optional layering: if it ever fails, the foundation game keeps running.
  try{Override=gameObject.AddComponent<OverrideDirector>();Override.Initialize(this);}catch(System.Exception e){Debug.LogException(e);}
 }
 void OnChessCompleted(string id){Puzzles.HandleSecurityEvent(id,State);}
 void OnDiscovered(string id){Save.SaveFlags(State);}
 public void TogglePause(){Paused=!Paused;bool shared=Override!=null&&Override.Roles!=null&&Override.Roles.Online;Time.timeScale=Paused&&!shared?0:(Override!=null&&Override.Roles!=null?Override.Roles.SimSpeed:1);}
 public void Restart(){Time.timeScale=1;Save.ClearProgress();UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);}
 void OnDestroy(){Time.timeScale=1;State.Discovered-=OnDiscovered;if(Chess)Chess.Completed-=OnChessCompleted;}
}
}
