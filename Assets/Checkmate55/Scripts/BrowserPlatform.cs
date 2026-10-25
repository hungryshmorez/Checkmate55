using UnityEngine;
using System.Runtime.InteropServices;
namespace Checkmate55 {
public static class BrowserPlatform {
#if UNITY_WEBGL && !UNITY_EDITOR
 [DllImport("__Internal")] static extern int CM55_IsCoarsePointer();
 [DllImport("__Internal")] static extern void CM55_Speak(string text,float pitch,float rate,float volume);
 [DllImport("__Internal")] static extern void CM55_SpeakStop();
 [DllImport("__Internal")] static extern void CM55_Vibrate(int ms);
 [DllImport("__Internal")] static extern System.IntPtr CM55_Prompt(string message,string current);
 [DllImport("__Internal")] static extern void CM55_PromptFree(System.IntPtr p);
#endif
 public static bool Mobile {
  get {
#if UNITY_WEBGL && !UNITY_EDITOR
   return CM55_IsCoarsePointer()!=0;
#else
   return Application.isMobilePlatform;
#endif
  }
 }
 // Text entry on touch devices (the browser's own prompt). Elsewhere the caller keeps using IMGUI text fields.
 public static string Prompt(string message,string current){
#if UNITY_WEBGL && !UNITY_EDITOR
  try{var p=CM55_Prompt(message,current??"");if(p==System.IntPtr.Zero)return current;try{return Marshal.PtrToStringAnsi(p);}finally{CM55_PromptFree(p);}}catch(System.Exception){return current;}
#else
  return current;
#endif
 }
 // The car's voice. Uses the browser's speech synthesis; silently does nothing where it is unavailable (Editor, desktop builds).
 public static void Speak(string text,float pitch,float rate,float volume){
#if UNITY_WEBGL && !UNITY_EDITOR
  try{CM55_Speak(text,pitch,rate,volume);}catch(System.Exception){}
#endif
 }
 public static void SpeakStop(){
#if UNITY_WEBGL && !UNITY_EDITOR
  try{CM55_SpeakStop();}catch(System.Exception){}
#endif
 }
 public static void Vibrate(int ms){
#if UNITY_WEBGL && !UNITY_EDITOR
  try{CM55_Vibrate(ms);}catch(System.Exception){}
#elif UNITY_ANDROID || UNITY_IOS
  if(ms>0)Handheld.Vibrate();
#endif
 }
}
}
