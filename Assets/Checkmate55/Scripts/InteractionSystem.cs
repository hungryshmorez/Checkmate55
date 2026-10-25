using UnityEngine;
namespace Checkmate55 {
public sealed class InteractionSystem : MonoBehaviour {
 public SceneManager game;public Camera view;public PhysicalControl Focus {get;private set;}
 public HandRig Hands;                                // set by OverrideDirector; null keeps the original instant behaviour
 const int Mask=1<<8;
 public PhysicalControl Pick(Vector2 position){var ray=view.ScreenPointToRay(position);var c=Physics.Raycast(ray,out var hit,4.5f,Mask,QueryTriggerInteraction.Collide)?(hit.collider.GetComponent<ChessPieceHit>()?hit.collider.GetComponent<ChessPieceHit>().target:hit.collider.GetComponentInParent<PhysicalControl>()):null;return c&&c.isActiveAndEnabled&&c.available?c:null;}
 void Update(){if(!game.Paused)Focus=Pick(new Vector2(Screen.width/2f,Screen.height/2f));}
 // A touch plays out physically: the hand reaches, and the control responds at the moment of contact.
 public void UseControl(PhysicalControl control){
  if(game.Paused||control==null||!control.isActiveAndEnabled||!control.available)return;
  if(Hands!=null&&!control.id.StartsWith("chess-")&&!(game.Chess&&game.Chess.Active))Hands.Perform(control,()=>Apply(control));else Apply(control);
 }
 void Apply(PhysicalControl control){if(game.Paused||control==null||!control.isActiveAndEnabled||!control.available)return;control.Use(game);game.Tutorial.Touched=true;}
 public void UseScreen(Vector2 position){UseControl(Pick(position));}
 public void UseCenter(){UseScreen(new Vector2(Screen.width/2f,Screen.height/2f));}
}
}
