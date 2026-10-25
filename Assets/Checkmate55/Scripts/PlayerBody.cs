using UnityEngine;
using System.Collections.Generic;
namespace Checkmate55 {
public sealed class PlayerBody : MonoBehaviour {
 public Vector3 SeatOffset;public VehicleMotion motion;Vector3 home;float handSpread;ChessController chess;readonly Dictionary<Transform,Vector3> hands=new Dictionary<Transform,Vector3>();
 void Awake(){home=transform.localPosition;foreach(var t in GetComponentsInChildren<Transform>())if(t.name=="Resting hand"||t.name=="Relaxed finger"||t.name=="Thumb"||t.name=="Jacket forearm")hands[t]=t.localPosition;}
 void Start(){chess=FindFirstObjectByType<ChessController>();}
 // Hides one side's resting arm while the first-person arm rig reaches (side: +1 right, -1 left).
 public void SetArmVisible(int side,bool visible){foreach(var r in GetComponentsInChildren<Renderer>(true)){string n=r.name;if(n=="Resting hand"||n=="Relaxed finger"||n=="Thumb"||n=="Jacket forearm"||n=="Jacket sleeve upper"){if((r.transform.localPosition.x>=0?1:-1)==side)r.enabled=visible;}}}
 void Update(){handSpread=Mathf.MoveTowards(handSpread,chess&&chess.Active?.13f:0,Time.deltaTime*.15f);foreach(var p in hands)p.Key.localPosition=p.Value+Vector3.right*Mathf.Sign(p.Value.x)*handSpread;if(motion) {transform.localRotation=Quaternion.Euler(Mathf.Clamp(-motion.acceleration*.08f,-2,2),0,-motion.Sway*1.4f);transform.localPosition=home+SeatOffset+Vector3.up*Mathf.Sin(Time.time*1.7f)*.0015f;}}
}
}
