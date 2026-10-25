using UnityEngine;
namespace Checkmate55 {
public sealed class SaveManager {
 public bool Hints {get=>PlayerPrefs.GetInt("cm55.hints",1)==1;set{PlayerPrefs.SetInt("cm55.hints",value?1:0);PlayerPrefs.Save();}}
 public bool TutorialCompleted {get=>PlayerPrefs.GetInt("cm55.tutorial",0)==1;set{PlayerPrefs.SetInt("cm55.tutorial",value?1:0);PlayerPrefs.Save();}}
 public int Guidance {get=>PlayerPrefs.GetInt("cm55.guidance",0);set{PlayerPrefs.SetInt("cm55.guidance",Mathf.Clamp(value,0,2));PlayerPrefs.Save();}}
 public float Volume {get=>PlayerPrefs.GetFloat("cm55.volume",.6f);set{PlayerPrefs.SetFloat("cm55.volume",value);PlayerPrefs.Save();}}
 public int Quality {get=>PlayerPrefs.GetInt("cm55.quality",BrowserPlatform.Mobile?0:1);set{PlayerPrefs.SetInt("cm55.quality",value);PlayerPrefs.Save();}}
 public void SaveFlags(GameState state){PlayerPrefs.SetString("cm55.flags",string.Join("|",state.Flags));PlayerPrefs.Save();}
 public void LoadFlags(GameState state){foreach(var flag in PlayerPrefs.GetString("cm55.flags","").Split('|'))if(flag.StartsWith("challenge.")||flag.StartsWith("rule.")||flag.StartsWith("security.")||flag.StartsWith("item.")||flag=="clue.route-card"||flag=="CHESS_CHALLENGE_COMPLETED"||flag=="glovebox-authenticated")state.Flag(flag);}
 public void ClearProgress(){PlayerPrefs.DeleteKey("cm55.flags");PlayerPrefs.DeleteKey("cm55.tutorial");PlayerPrefs.Save();}
}
}
