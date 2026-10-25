using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Checkmate55;
public static class RoadSecurityUpgrade {
 static ChessChallenge Challenge(string id,string title,string fen,ChessObjective objective,string guidance,string hint,string hintText,string result,int moves=3,string target="",bool road=false){
  string path="Assets/Checkmate55/"+id+".asset";var c=AssetDatabase.LoadAssetAtPath<ChessChallenge>(path);if(!c){c=ScriptableObject.CreateInstance<ChessChallenge>();AssetDatabase.CreateAsset(c,path);}c.id=id;c.title=title;c.FEN=fen;c.objectiveType=objective;c.guidanceText=guidance;c.hintMove=hint;c.hintText=hintText;c.resultText=result;c.maxMoves=moves;c.targetPiece=target;c.road=road;c.rewardEvent="security."+id;EditorUtility.SetDirty(c);return c;
 }
 static PhysicalControl Control(string id,string label,Transform p,Vector3 pos,Vector3 size){var t=ChessBoardView.Group(id,p,pos);t.gameObject.layer=8;var c=t.gameObject.AddComponent<PhysicalControl>();c.id=id;c.label=label;c.action=ControlAction.Display;c.movingPart=t;var b=t.gameObject.AddComponent<BoxCollider>();b.isTrigger=true;b.size=size;c.mobileHitProxy=b;return c;}
 static TextMesh Text(Transform parent,Vector3 pos,string value,float size){var text=ChessBoardView.Text(value,parent,pos,value,size,new Color(.1f,.2f,.22f));return text;}
 [MenuItem("CHECKMATE 55/Install Road Security")]
 public static void Upgrade(){
  var g=Object.FindFirstObjectByType<Checkmate55.SceneManager>();if(!g||!g.Chess)throw new System.Exception("Restored Checkmate55 scene required");
  if(!g.Security)g.Security=g.gameObject.AddComponent<VehicleSecurity>();var security=g.Security;g.Registry.Initialize();
  security.diagnostics=Challenge("diagnostics","DIAGNOSTICS","4k3/q7/8/8/8/8/6KP/R7 w - - 0 1",ChessObjective.CaptureTarget,"Capture the black queen to connect diagnostics and release seat controls.","a1a7","The queen carries the diagnostic key.","DIAGNOSTICS CONNECTED / SEAT LOCK RELEASED",3,"q");
  security.window=Challenge("window","WINDOW RELEASE","4k1r1/p7/8/8/8/8/R7/6K1 w - - 0 1",ChessObjective.SurviveMoves,"Your king is in check. Get it safe through one vehicle reply to release the windows for 15 seconds.","g1h2","Step off the rook's file. Survive its reply.","WINDOW LATCH RELEASED / 15 SECONDS",1);
  security.diversion=Challenge("diversion","LANE DIVERSION","r6k/8/8/8/8/8/1R5P/6K1 w - - 0 1",ChessObjective.SacrificePiece,"Offer your rook to the black rook. Its capture forces braking and a left-lane diversion.","b2a2","The car values material over its route. Let it take your rook.","SACRIFICE ACCEPTED / LEFT-LANE DIVERSION",2,"r");
  security.route=Challenge("route","NAVIGATION OVERRIDE","8/pp6/8/8/7k/RP6/2N5/6K1 w - - 0 1",ChessObjective.DeliverCheck,"Check the king to redirect navigation. Active files follow the lane. Normal chess or a discovered road command both work.","a3a4","A normal rook check works too. Hidden alternative: c2 → f3 at 70+ MPH.","NAVIGATION RELEASED / SERVICE LAY-BY",3,"",true);
  if(!GameObject.Find("Road security clues")){
   var cabin=g.Player.transform.parent;var root=ChessBoardView.Group("Road security clues",cabin,Vector3.zero);
   var paper=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Ivory light.mat");var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Satin aluminium.mat");
   var card=Control("route-card","Passenger 04 · route card",root,new Vector3(.61f,.64f,1.64f),new Vector3(.38f,.16f,.18f));security.storageClue=card.transform;
   var shape=GameObject.CreatePrimitive(PrimitiveType.Cube);shape.name="Passenger route card";shape.transform.SetParent(card.transform,false);shape.transform.localScale=new Vector3(.32f,.008f,.18f);shape.GetComponent<Renderer>().sharedMaterial=paper;Object.DestroyImmediate(shape.GetComponent<Collider>());
   Text(card.transform,new Vector3(0,.007f,0),"PASSENGER 04\nCHECK = BRAKE\nSURRENDER = DIVERSION\nUSB → DIAGNOSTICS",.016f);
   var pocket=g.Registry.Get("passenger-pocket");var piece=Control("lost-knight","Loose white knight · recover",root,root.InverseTransformPoint(pocket.transform.position)+new Vector3(.06f,.08f,-.09f),new Vector3(.18f,.2f,.16f));security.lostKnight=piece.transform;
   // A small recognisable horse silhouette pokes out of the existing seat pocket.
   foreach(var spec in new[]{new Vector4(0,0,0,.05f),new Vector4(0,.032f,0,.03f),new Vector4(.018f,.06f,0,.04f)}){var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name="Recovered knight porcelain";p.transform.SetParent(piece.transform,false);p.transform.localPosition=new Vector3(spec.x,spec.y,spec.z);p.transform.localScale=new Vector3(spec.w,spec.w*.8f,.035f);p.GetComponent<Renderer>().sharedMaterial=paper;Object.DestroyImmediate(p.GetComponent<Collider>());}
   var scratch=Control("seat-scratch","Scratched notation · under rear seat",root,new Vector3(-.37f,.39f,-.8f),new Vector3(.3f,.15f,.2f));
   var plate=GameObject.CreatePrimitive(PrimitiveType.Cube);plate.name="Scratched aluminium seat plate";plate.transform.SetParent(scratch.transform,false);plate.transform.localScale=new Vector3(.3f,.006f,.13f);plate.GetComponent<Renderer>().sharedMaterial=metal;Object.DestroyImmediate(plate.GetComponent<Collider>());
   Text(scratch.transform,new Vector3(0,.005f,0),"70+  N : 3 × 1\nc2 → f3\nIT LIES.",.017f);
   var plaque=Control("route-legend","Passenger route legend",root,new Vector3(.42f,.76f,-.82f),new Vector3(.25f,.06f,.2f));plaque.available=false;
   security.rulePlaque=Text(plaque.transform,Vector3.up*.005f,"PASSENGER 04",.011f);security.rulePlaque.color=new Color(.64f,.92f,.86f);
  }
  g.name="CHECKMATE 55 • Road Security";
  PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.dataCaching=false;PlayerSettings.WebGL.threadsSupport=false;
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Checkmate55.unity",true)};
  EditorUtility.SetDirty(g);EditorUtility.SetDirty(security);EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();Debug.Log("Road Security saved; restored cabin retained.");
 }
}
