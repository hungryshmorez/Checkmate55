using System;
using UnityEngine;
namespace Checkmate55 {
public enum ControlAction { Handle, Switch, Lamp, Seat, Armrest, Buckle, Locked, Display, Climate, Cupholder, USB, Pocket, Speaker }
public sealed class PhysicalControl : MonoBehaviour {
 public string id,label,prerequisite,response;
 public bool available=true,locked;
 public ControlAction action;
 public Transform movingPart;
 public Vector3 moveOffset,rotateOffset;
 public Light cabinLight;
 public Renderer indicator;
 public Collider mobileHitProxy;
 public bool highlight=true;
 public float sound=650;
 public event Action<PhysicalControl> Callback;
 public bool Active {get;private set;}
 public Vector3 bias;                       // OVERRIDE: the car can push a part (e.g. creep the seat) without disturbing the control's own state
 public void SetActive(bool value){Active=value;}
 public int UseCount {get;private set;}
 Vector3 origin; Quaternion rotation; float value, pulse;
 void Awake() { if(movingPart==null) movingPart=transform; origin=movingPart.localPosition;rotation=movingPart.localRotation; }
 public void Use(SceneManager game) {
  if(!available||!enabled) return;
  if(!game.State.Has(prerequisite)) { game.Dialogue.Say("That control is not available yet.");return; }
  UseCount++;
  if(game.Override!=null&&game.Override.Intercept(this)){pulse=.28f;Callback?.Invoke(this);return;}
  if(game.Ride && game.Ride.BeforeUse(this)){game.Audio.Click(500);pulse=.32f;game.Override?.AfterControl(this);Callback?.Invoke(this);return;}
  game.Audio.ControlSound(id,action,locked);
  if(locked) {pulse=.24f;game.Dialogue.Say(response);game.Ride?.AfterUse(this);game.Override?.AfterControl(this);Callback?.Invoke(this);return;}
  Active=!Active;
  if(action==ControlAction.Handle||action==ControlAction.Switch||action==ControlAction.USB||action==ControlAction.Pocket||action==ControlAction.Cupholder) pulse=.32f;
  if(action==ControlAction.Lamp&&cabinLight) cabinLight.enabled=Active;
  if(action==ControlAction.Climate) game.Audio.SetVent(Active);
  game.State.Flag("used."+id); game.Dialogue.Say(response); game.Ride?.AfterUse(this); game.Override?.AfterControl(this); Callback?.Invoke(this);
 }
 void Update() {
  bool momentary=action==ControlAction.Handle||action==ControlAction.Switch||action==ControlAction.USB||action==ControlAction.Pocket||action==ControlAction.Cupholder||locked;
  pulse=Mathf.Max(0,pulse-Time.deltaTime);
  value=Mathf.MoveTowards(value,momentary?(pulse>0?1:0):(Active?1:0),Time.deltaTime*5);
  movingPart.localPosition=origin+moveOffset*value+bias;movingPart.localRotation=rotation*Quaternion.Euler(rotateOffset*value);
  if(indicator) indicator.material.SetColor("_EmissionColor",(Active?new Color(.15f,.65f,.7f):Color.black)*.5f);
 }
}
}
