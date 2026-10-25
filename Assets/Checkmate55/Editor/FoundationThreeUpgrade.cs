using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Checkmate55;
public static class FoundationThreeUpgrade {
 [MenuItem("CHECKMATE 55/Upgrade existing scene to Foundation 03")]
 public static void Upgrade(){
  var game=Object.FindFirstObjectByType<Checkmate55.SceneManager>();if(!game||!game.Ride)throw new System.Exception("Restored Foundation 02 scene required.");
  if(!game.Chess)game.Chess=game.gameObject.AddComponent<ChessController>();if(!game.Seats)game.Seats=game.gameObject.AddComponent<SeatSwitcher>();
  const string path="Assets/Checkmate55/PassengerAuthentication.asset";var challenge=AssetDatabase.LoadAssetAtPath<ChessChallenge>(path);if(!challenge){challenge=ScriptableObject.CreateInstance<ChessChallenge>();AssetDatabase.CreateAsset(challenge,path);}game.Chess.challenge=challenge;
  var depth=game.GetComponent<WorldTextDepth>();if(!depth)depth=game.gameObject.AddComponent<WorldTextDepth>();depth.shader=Shader.Find("Checkmate55/WorldText");
  game.name="CHECKMATE 55 • Foundation 03";
  // Permanent chess insignia on the physical center console, never a permanent playable board.
  if(!GameObject.Find("Chess security • console insignia")){
   var root=new GameObject("Chess security • console insignia").transform;root.SetParent(game.Player.transform.parent,false);root.localPosition=new Vector3(0,.737f,.30f);
   var gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Satin aluminium.mat");var dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Display glass.mat");
   for(int y=0;y<4;y++)for(int x=0;x<4;x++){var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);tile.name="Inlaid chess square";tile.transform.SetParent(root,false);tile.transform.localPosition=new Vector3((x-1.5f)*.028f,0,(y-1.5f)*.028f);tile.transform.localScale=new Vector3(.027f,.002f,.027f);tile.GetComponent<Renderer>().sharedMaterial=(x+y)%2==0?gold:dark;Object.DestroyImmediate(tile.GetComponent<Collider>());}
   var text=new GameObject("Chess security lettering").AddComponent<TextMesh>();text.transform.SetParent(root,false);text.transform.localPosition=new Vector3(0,.002f,-.085f);text.transform.localRotation=Quaternion.Euler(90,0,0);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.GetComponent<Renderer>().sharedMaterial=text.font.material;text.fontSize=64;text.characterSize=.011f*.15625f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.text="CHESS SECURITY\nPASSENGER AUTH";text.color=new Color(.63f,.95f,.9f);
  }
  GameObject.Find("Chess security lettering").GetComponent<TextMesh>().characterSize=.011f*.15625f;
  game.Registry.Initialize();game.Registry.Get("authenticate").label="Chess security · authenticate";
  PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.dataCaching=false;PlayerSettings.WebGL.threadsSupport=false;
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Checkmate55.unity",true)};
  EditorUtility.SetDirty(game);EditorUtility.SetDirty(game.Chess);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();Debug.Log("Foundation 03 saved; original cabin preserved.");
 }
}
