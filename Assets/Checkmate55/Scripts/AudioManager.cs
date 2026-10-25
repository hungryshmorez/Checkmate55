using UnityEngine;
namespace Checkmate55 {
public sealed class AudioManager : MonoBehaviour {
 AudioSource road,click,fan,traffic;AudioClip tick,motor,latch,lockClick,chime;bool started;float ventilation;float nextTraffic;
 AudioClip Make(string name,float seconds,int kind){int rate=22050;float[] s=new float[(int)(rate*seconds)];var rng=new System.Random(55+kind);float low=0;for(int i=0;i<s.Length;i++){float t=(float)i/rate;float noise=(float)rng.NextDouble()*2-1;low=Mathf.Lerp(low,noise,.04f);float envelope=Mathf.Min(1,t*25)*Mathf.Min(1,(seconds-t)*15);
  switch(kind){case 0:s[i]=(low*.6f+Mathf.Sin(t*302)*.025f);break;case 1:s[i]=Mathf.Sin(t*3770)*Mathf.Exp(-t*50)*.3f;break;case 2:s[i]=(Mathf.Sin(t*480)*.07f+low*.3f)*envelope;break;case 3:s[i]=(noise*.5f+Mathf.Sin(t*760)*.4f)*Mathf.Exp(-t*45);break;case 4:float a=t% .065f;s[i]=(noise*.65f+Mathf.Sin(t*520)*.3f)*Mathf.Exp(-a*100)*Mathf.Exp(-t*9);break;case 5:s[i]=(Mathf.Sin(t*4147)+Mathf.Sin(t*5529))*.1f*Mathf.Exp(-t*3)*envelope;break;case 6:s[i]=low*.6f*envelope;break;}
  }var clip=AudioClip.Create(name,s.Length,1,rate,false);clip.SetData(s,0);return clip;}
 AudioSource Source(bool loop,float volume){var a=gameObject.AddComponent<AudioSource>();a.loop=loop;a.volume=volume;a.playOnAwake=false;return a;}
 void Awake(){road=Source(true,.13f);road.clip=Make("Tire rumble",3,0);fan=Source(true,0);fan.clip=Make("Climate air",3,6);traffic=Source(false,.17f);traffic.clip=Make("Passing traffic",2.6f,6);click=Source(false,.6f);tick=Make("Button",.12f,1);motor=Make("Electric motor",1.3f,2);latch=Make("Compartment latch",.18f,3);lockClick=Make("Four door locks CLICK",.3f,4);chime=Make("Dashboard two tone chime",1.3f,5);}
 public void Begin(){if(started)return;started=true;road.Play();fan.Play();nextTraffic=Time.time+8;}
 public void Click(float frequency){Begin();click.pitch=frequency/600;click.PlayOneShot(tick);}
 public void Latch(){Begin();click.pitch=1;click.PlayOneShot(latch);}
 public void LockClick(){Begin();click.pitch=1;click.PlayOneShot(lockClick,1.2f);}
 public void Chime(){Begin();click.pitch=1;click.PlayOneShot(chime);}
 public void ControlSound(string id,ControlAction action,bool locked){Begin();click.pitch=1;if(locked||action==ControlAction.Handle){click.PlayOneShot(latch);return;}if(id.EndsWith("-window")||action==ControlAction.Seat||id=="rear-seat")click.PlayOneShot(motor,.5f);else click.PlayOneShot(tick);}
 public void ChessSlide(){Begin();click.pitch=1.6f;click.PlayOneShot(motor,.25f);}
 public void ChessCapture(){Begin();click.pitch=.65f;click.PlayOneShot(latch,.75f);}
 public void ChessSettle(){Begin();click.pitch=1.5f;click.PlayOneShot(lockClick,.35f);}
 public void SetVent(bool on){ventilation=on?.18f:.035f;}
 void Update(){fan.volume=Mathf.Lerp(fan.volume,ventilation,Time.deltaTime*2);if(started&&Time.time>nextTraffic){traffic.panStereo=Random.value>.5f?.6f:-.6f;traffic.Play();nextTraffic=Time.time+Random.Range(10,18);}}
}
}
