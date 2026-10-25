using UnityEngine;
namespace Checkmate55 {
public sealed class CabinUI : MonoBehaviour {
 public SceneManager game;GUIStyle small,label,subtitle,title,button;Texture2D dark,ring;float scale;bool styles;
 void Setup(){var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");small=new GUIStyle{font=font,fontSize=12,alignment=TextAnchor.MiddleCenter,wordWrap=true,normal={textColor=new Color(.85f,.9f,.91f)}};label=new GUIStyle(small){fontSize=16};subtitle=new GUIStyle(small){fontSize=15,wordWrap=true};title=new GUIStyle(small){fontSize=32,alignment=TextAnchor.MiddleLeft};button=new GUIStyle(GUI.skin.button){font=font,fontSize=13};dark=new Texture2D(1,1);dark.SetPixel(0,0,new Color(.025f,.045f,.052f,.88f));dark.Apply();ring=new Texture2D(64,64);for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));ring.SetPixel(x,y,new Color(1,1,1,d>24&&d<29?1:0));}ring.Apply();styles=true;}
 public bool OverButton(Vector2 screen){float s=Mathf.Max(.8f,Screen.height/800f);return game.Paused||(game.Override!=null&&game.Override.UI!=null&&game.Override.UI.Blocking)||(screen.x<110*s&&screen.y>Screen.height-125*s)||(screen.x>Screen.width-80*s&&screen.y>Screen.height-68*s)||screen.y<58*s||(game.Chess&&game.Chess.PromotionPending&&screen.y<165*s);}
 void OnGUI(){if(!styles)Setup();if(game.Chess==null)return;var ov=game.Override;if(ov!=null&&ov.UI!=null&&ov.UI.Blocking)return;bool car=ov!=null&&ov.Roles!=null&&ov.Roles.LocalIsCar;scale=Mathf.Max(.8f,Screen.height/800f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
  if(GUI.Button(new Rect(w-64,18,44,36),game.Paused?"×":"Ⅱ",button))game.TogglePause();
  if(game.Paused){GUI.DrawTexture(new Rect(0,0,w,h),dark);if(ov!=null&&ov.UI!=null&&ov.UI.NotesOpen){ov.UI.DrawNotes(w,h);return;}float pw=Mathf.Min(430,w-36),x=(w-pw)/2;GUI.Label(new Rect(x,h/2-235,pw,55),"CHECKMATE 55",title);GUI.Label(new Rect(x,h/2-184,pw,32),"ROAD CHESS / VEHICLE SECURITY",small);
   GUI.Label(new Rect(x,h/2-148,pw,64),"Drag to look · Click / tap to interact\nWASD / arrows look · Space interact\nTab switch seat · H hint · R recenter\nQ / E lean · Esc pause",subtitle);
   GUI.Label(new Rect(x,h/2-70,pw,24),"CABIN AUDIO",small);float volume=GUI.HorizontalSlider(new Rect(x+30,h/2-40,pw-60,20),AudioListener.volume,0,1);if(volume!=AudioListener.volume){AudioListener.volume=volume;game.Save.Volume=volume;}
   GUI.Label(new Rect(x,h/2-12,pw,24),"QUALITY",small);for(int i=0;i<3;i++)if(GUI.Button(new Rect(x+i*pw/3,h/2+17,pw/3-5,36),(game.Save.Quality==i?"• ":"")+((PerformanceManager.Profile)i).ToString(),button)){game.Save.Quality=i;game.Performance.Apply(i);}
   if(GUI.Button(new Rect(x,h/2+66,pw,36),"GUIDE LIGHTS & HINTS: "+(game.Save.Hints?"ON":"OFF"),button))game.Save.Hints=!game.Save.Hints;
   if(GUI.Button(new Rect(x,h/2+116,pw,42),"RESUME RIDE",button))game.TogglePause();if(GUI.Button(new Rect(x,h/2+170,pw/2-3,36),"RESTART RIDE",button))game.Restart();
   if(ov!=null&&ov.UI!=null){if(!car&&GUI.Button(new Rect(x+pw/2+3,h/2+170,pw/2-3,36),"NOTES ("+ov.Clues.All.Count+")",button))ov.UI.OpenNotes();
    if(GUI.Button(new Rect(x,h/2+212,pw/2-3,36),"CAR VOICE: "+(ov.VoiceOn?"ON":"OFF"),button)){ov.VoiceOn=!ov.VoiceOn;PlayerPrefs.SetInt("cm55.voice",ov.VoiceOn?1:0);PlayerPrefs.Save();if(!ov.VoiceOn)BrowserPlatform.SpeakStop();}
    if(GUI.Button(new Rect(x+pw/2+3,h/2+212,pw/2-3,36),"MAIN MENU",button)){PlayerPrefs.DeleteKey("cm55.autostart");game.Restart();}}
   return;}
  var focus=car?null:game.Interactions.Focus;GUI.color=focus!=null&&focus.highlight?new Color(.56f,.91f,.9f):new Color(1,1,1,.7f);GUI.DrawTexture(new Rect(w/2-2,h/2-2,4,4),Texture2D.whiteTexture);GUI.color=Color.white;
  if(focus!=null){float fw=Mathf.Min(330,w-150);GUI.DrawTexture(new Rect((w-fw)/2,18,fw,44),dark);GUI.Label(new Rect((w-fw)/2,19,fw,25),focus.label,label);GUI.Label(new Rect((w-fw)/2,42,fw,18),"TAP / SPACE",small);}
  if(game.Save.Hints&&!game.Chess.Active&&!car)DrawGuide(w,h);
  string prompt=car?"":game.Tutorial.Prompt;
  if(prompt.Length>0){float tw=Mathf.Min(650,w-24),ph=Mathf.Max(44,subtitle.CalcHeight(new GUIContent(prompt),tw-24)+16);GUI.DrawTexture(new Rect((w-tw)/2,h-58-ph,tw,ph),dark);GUI.Label(new Rect((w-tw)/2+12,h-58-ph+6,tw-24,ph-12),prompt,subtitle);}
  string text=car?"":game.Dialogue.Visible;if(text.Length>0){float tw=Mathf.Min(630,w-32);float sh=Mathf.Max(48,subtitle.CalcHeight(new GUIContent(text),tw-24)+18);float baseY=game.Chess.Active?80+sh:h-(prompt.Length>0?145:68);GUI.DrawTexture(new Rect((w-tw)/2,baseY-sh,tw,sh),dark);GUI.Label(new Rect((w-tw)/2+12,baseY-sh+7,tw-24,sh-14),text,subtitle);}
  float bw=Mathf.Min(165,(w-36)*.44f),left=(w-(bw+172))/2;
  if(!car){
  if(GUI.Button(new Rect(left,h-48,bw,36),game.Chess.Active?"RETRACT TRAY":game.Seats.SeatName+" ›",button)){if(game.Chess.Active)game.Chess.Cancel();else game.Seats.Cycle();}
  if(GUI.Button(new Rect(left+bw+6,h-48,68,36),"HINT",button))game.Tutorial.Hint();
  if(GUI.Button(new Rect(left+bw+80,h-48,92,36),game.Save.Hints?"GUIDE ON":"GUIDE OFF",button))game.Save.Hints=!game.Save.Hints;
  }
  if(game.Chess.PromotionPending){float pw=Mathf.Min(360,w-24);GUI.DrawTexture(new Rect((w-pw)/2,h-160,pw,64),dark);string[] names={"QUEEN","ROOK","BISHOP","KNIGHT"},types={"q","r","b","n"};for(int i=0;i<4;i++)if(GUI.Button(new Rect((w-pw)/2+i*pw/4,h-150,pw/4-3,44),names[i],button))game.Chess.Promote(types[i]);}
 }
 void DrawGuide(float w,float h){string id=game.Tutorial.TargetId;if(id==null)return;var c=game.Registry.Get(id);if(!c)return;Vector3 p=game.Player.view.WorldToScreenPoint(c.transform.position);Vector2 point=new Vector2(p.x/scale,h-p.y/scale);bool off=p.z<0||point.x<35||point.x>w-35||point.y<88||point.y>h-220;
  if(off){Vector3 local=game.Player.view.transform.InverseTransformPoint(c.transform.position);Vector2 dir=new Vector2(local.x,-local.y);if(dir.sqrMagnitude<.01f)dir=Vector2.right;dir.Normalize();point=new Vector2(w/2,h/2)+new Vector2(dir.x*(w/2-45),dir.y*(h/2-210));point.x=Mathf.Clamp(point.x,40,w-40);point.y=Mathf.Clamp(point.y,95,h-220);}
  float size=38+Mathf.Sin(Time.unscaledTime*3)*4;GUI.color=new Color(.42f,1,.77f,.9f);GUI.DrawTexture(new Rect(point.x-size/2,point.y-size/2,size,size),ring);GUI.Label(new Rect(point.x-16,point.y-16,32,32),off?(point.x<w/2?"‹":"›"):"+",label);GUI.color=Color.white;
  float x=Mathf.Clamp(point.x-78,8,w-164);GUI.DrawTexture(new Rect(x,point.y+24,156,36),dark);GUI.Label(new Rect(x+4,point.y+24,148,36),off?"NEXT CONTROL · HINT to look":"NEXT · "+c.label,small);
 }
}
}
