#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
namespace Checkmate55.EditorTools {
// Menu items and import settings for CHECKMATE 55: OVERRIDE. Editor-only (not part of the WebGL build).
public static class OverrideTools {
 const string Root="CHECKMATE 55/Override/";
 [MenuItem(Root+"Run AI Simulation (x8, writes docs/OVERRIDE_SIM.txt)")]
 static void RunAiSimulation(){
  PlayerPrefs.SetInt("cm55.autostart",(int)Checkmate55.Core.GameMode.AiSimulation+1);PlayerPrefs.SetInt("cm55.simspeed",8);PlayerPrefs.SetInt("cm55.verify",1);PlayerPrefs.Save();
  EditorApplication.isPlaying=true;
 }
 [MenuItem(Root+"Play Story (normal)")]
 static void PlayStory(){PlayerPrefs.DeleteKey("cm55.verify");PlayerPrefs.DeleteKey("cm55.simspeed");PlayerPrefs.SetInt("cm55.autostart",(int)Checkmate55.Core.GameMode.Story+1);PlayerPrefs.Save();EditorApplication.isPlaying=true;}
 [MenuItem(Root+"Play with title menu")]
 static void PlayMenu(){PlayerPrefs.DeleteKey("cm55.autostart");PlayerPrefs.DeleteKey("cm55.verify");PlayerPrefs.DeleteKey("cm55.simspeed");PlayerPrefs.Save();EditorApplication.isPlaying=true;}
 [MenuItem(Root+"Clear saved records (best times, endings, calibration)")]
 static void ClearRecords(){PlayerPrefs.DeleteKey("cm55.records");PlayerPrefs.DeleteKey("cm55.voice");PlayerPrefs.DeleteKey("cm55.server");PlayerPrefs.DeleteKey("cm55.name");PlayerPrefs.Save();Debug.Log("CHECKMATE 55: records cleared.");}
}
// Web-friendly import settings for the five user-supplied radio songs.
public sealed class RadioImportSettings:AssetPostprocessor {
 void OnPreprocessAudio(){
  if(!assetPath.Replace('\\','/').Contains("/Resources/Radio/"))return;
  var importer=(AudioImporter)assetImporter;
  var settings=new AudioImporterSampleSettings{loadType=AudioClipLoadType.CompressedInMemory,compressionFormat=AudioCompressionFormat.Vorbis,quality=.55f,sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate};
  importer.defaultSampleSettings=settings;
  importer.SetOverrideSampleSettings("WebGL",settings);
  importer.forceToMono=false;importer.loadInBackground=false;importer.preloadAudioData=true;
 }
}
}
#endif
