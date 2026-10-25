using UnityEngine;
namespace Checkmate55 {
public sealed class VehicleMotion : MonoBehaviour {
 public float speed=24f;public int lane=1;public float acceleration;
 public Transform[] roadSegments,traffic;
 public Transform environment;float lanePosition,laneVelocity;
 public float segmentLength=40;
 public float Sway {get;private set;}
 float previous;
 void Update(){
  float dt=Time.deltaTime; acceleration=(speed-previous)/Mathf.Max(.001f,dt);previous=speed;
  lanePosition=Mathf.SmoothDamp(lanePosition,(Mathf.Clamp(lane,0,2)-1)*3.5f,ref laneVelocity,2.5f);
  if(environment)environment.localPosition=Vector3.left*lanePosition;
  Sway=Mathf.Clamp(-laneVelocity*.6f,-1.5f,1.5f)+Mathf.Sin(Time.time*.37f)*.35f+Mathf.Sin(Time.time*.91f)*.08f;
  foreach(var s in roadSegments){s.position+=Vector3.back*speed*dt;if(s.position.z< -60)s.position+=Vector3.forward*roadSegments.Length*segmentLength;}
  for(int i=0;i<traffic.Length;i++){var t=traffic[i];t.position+=Vector3.back*(i%2==0?3f:7f)*dt;if(t.localPosition.z<(i%3==1?18f:-45f))t.localPosition=new Vector3((i%3-1)*3.5f,-.38f,170+i*12);}
 }
}
}
