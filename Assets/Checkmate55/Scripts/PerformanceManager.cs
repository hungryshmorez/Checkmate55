using UnityEngine;
namespace Checkmate55 {
public sealed class PerformanceManager : MonoBehaviour {
 public enum Profile { LOW,MEDIUM,HIGH }
 public Profile Current {get;private set;}
 public Transform[] traffic;
 public void Apply(int profile){Current=(Profile)profile;QualitySettings.antiAliasing=profile==0?0:2;QualitySettings.shadows=profile==0?ShadowQuality.Disable:ShadowQuality.HardOnly;QualitySettings.shadowResolution=profile==2?ShadowResolution.High:ShadowResolution.Medium;QualitySettings.shadowDistance=profile==2?65:30;Application.targetFrameRate=60;for(int i=0;i<traffic.Length;i++)traffic[i].gameObject.SetActive(i<(profile==0?4:profile==1?7:10));}
}
}
