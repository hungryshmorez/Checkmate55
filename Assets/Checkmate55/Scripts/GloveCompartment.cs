using UnityEngine;
namespace Checkmate55 {
public enum CompartmentState { LOCKED, UNLOCKED, OPEN }
public sealed class GloveCompartment : MonoBehaviour {
 public Transform lidPivot;
 public PhysicalControl latch;
 public CompartmentState State {get;private set;}=CompartmentState.LOCKED;
 // Future authentication completion may call Unlock; phase two never grants access.
 public void Unlock(){State=CompartmentState.UNLOCKED;latch.locked=false;latch.label="Glove box · unlocked";}
 public void Pull(){if(State==CompartmentState.UNLOCKED)State=CompartmentState.OPEN;else if(State==CompartmentState.OPEN)State=CompartmentState.UNLOCKED;}
 void Update(){lidPivot.localRotation=Quaternion.Slerp(lidPivot.localRotation,Quaternion.Euler(State==CompartmentState.OPEN?65:0,0,0),Time.deltaTime*6);}
}
}
