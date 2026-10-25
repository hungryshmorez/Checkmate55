namespace Checkmate55 {
public sealed class VehicleAI {
 readonly SceneManager game;
 public bool Autonomous=>true;
 public bool SecurityActive=>game.State.Has("lockdown");
 public VehicleAI(SceneManager owner){game=owner;}
 public void React(string context,int count=1){
  string line=null;
  switch(context){
   case "boarding":line="Welcome aboard. Make yourself comfortable.";break;
   case "lockdown":line="Route reassigned. Please remain seated.";break;
   case "door":line=SecurityActive?(count>2?"Repeated requests will not alter your destination.":"The doors will remain secured."):"The door is secured during travel. Use REQUEST STOP on your rear display.";break;
   case "glovebox":line="That compartment is not available to passengers.";break;
   case "panel":line=SecurityActive?"Please return the trim panel.":"Passenger service contacts are beneath the armrest trim.";break;
   case "puzzle":line="Passenger authentication prepared. Storage remains secured pending validation.";break;
   case "chess":line="The security module is awaiting activation.";break;
   case "secret":line=SecurityActive?"You appear distracted.":"Please leave passenger documents in the vehicle.";break;
   case "exit":line="Your exit request cannot be completed. Please remain seated.";break;
  }
  if(line!=null)game.Dialogue.Say(line);
 }
}
}
