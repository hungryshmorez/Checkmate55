using UnityEngine;
namespace Checkmate55 {
public sealed class SeatSwitcher : MonoBehaviour {
 public Vector3 Offset {get;private set;}
 public int Seat {get;private set;}=1;
 public bool Moving=>Vector3.Distance(Offset,target)>.025f;
 public string SeatName=>Seat==0?"REAR LEFT":Seat==1?"REAR CENTER":Seat==2?"REAR RIGHT":"FRONT PASSENGER";
 SceneManager game;PlayerBody body;Vector3 target;bool aisle;
 public void Initialize(SceneManager owner){game=owner;body=FindFirstObjectByType<PlayerBody>();}
 public void Cycle(){Choose((Seat+1)%4);}
 public void Choose(int seat){if(game.Paused)return;if(game.State.Current==RideState.BOARDING){game.Dialogue.Say("The door is closing. You can change seats in a moment.");return;}if(game.Chess.Active){game.Dialogue.Say("Retract the lap tray before changing seats.");return;}if(Moving)return;
  int previous=Seat;Seat=Mathf.Clamp(seat,0,3);target=Seat==3?new Vector3(.63f,0,1.69f):new Vector3((Seat-1)*.61f,0,0);aisle=Seat==3||previous==3;
  var arm=game.Registry.Get("armrest");if(Seat==2&&!arm.Active)arm.Use(game);
  game.Audio.ControlSound("rear-seat",ControlAction.Seat,false);game.Dialogue.Say("Moving to "+SeatName.ToLower()+". Drag to look; tap to investigate.");game.State.Flag("seat-switched");}
 void Update(){if(game==null||game.Paused)return;
  Vector3 next=target;
  // Follow the aisle to avoid moving the player's head through a front headrest.
  if(aisle){if(Mathf.Abs(Offset.x)>.03f&&Mathf.Abs(Offset.z-target.z)>.04f)next=new Vector3(0,0,Offset.z);else if(Mathf.Abs(Offset.z-target.z)>.04f)next=new Vector3(0,0,target.z);else aisle=false;}
  Offset=Vector3.Lerp(Offset,next,1-Mathf.Exp(-Time.deltaTime*5));if(body)body.SeatOffset=Offset;}
}
}
