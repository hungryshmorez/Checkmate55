using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Checkmate55;
using Object=UnityEngine.Object;
// A one-time additive migration of the restored scene. Never calls FoundationBuilder.Build.
public static class FoundationTwoUpgrade {
 static Transform cabin;static Font font;static Material dark,metal,paper,blue;static Mesh rounded;
 static Transform Find(string name)=>cabin.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
 static Transform Group(string name,Transform parent,Vector3 pos){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
 static Transform Box(string name,Transform p,Vector3 pos,Vector3 size,Material mat){var t=Group(name,p,pos);t.localScale=size;t.gameObject.AddComponent<MeshFilter>().sharedMesh=rounded;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;}
 static TextMesh Text(string name,string value,Transform p,Vector3 pos,float size){var t=Group(name,p,pos);var text=t.gameObject.AddComponent<TextMesh>();text.text=value;text.font=font;text.fontSize=64;text.characterSize=size*.15625f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.65f,.88f,.92f);text.GetComponent<Renderer>().sharedMaterial=font.material;return text;}
 static PhysicalControl Control(string id,string label,Transform p,Vector3 pos,Vector3 size){var t=Group("INTERACT • "+id,p,pos);t.gameObject.layer=8;var c=t.gameObject.AddComponent<PhysicalControl>();c.id=id;c.label=label;c.action=ControlAction.Display;c.response="";c.movingPart=t;var b=t.gameObject.AddComponent<BoxCollider>();b.isTrigger=true;b.size=size;c.mobileHitProxy=b;return c;}
 static void Remove(string name){var t=cabin.GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>x.name==name);if(t)Object.DestroyImmediate(t.gameObject);}
 [MenuItem("CHECKMATE 55/Upgrade existing scene to Foundation 02")]
 public static void Upgrade(){
  var game=Object.FindFirstObjectByType<Checkmate55.SceneManager>();if(!game)throw new System.Exception("Open the restored Checkmate55 scene first.");if(game.Ride)throw new System.Exception("Foundation 02 is already installed; migration skipped.");
  cabin=Object.FindFirstObjectByType<VehicleInterior>().transform;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Display glass.mat");metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Satin aluminium.mat");paper=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Ivory light.mat");blue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Ice blue accent.mat");rounded=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Checkmate55/Materials/Rounded.mesh");
  game.Registry.Initialize();var ride=game.gameObject.AddComponent<RideDirector>();game.Ride=ride;game.name="CHECKMATE 55 • Foundation 02";
  var interior=cabin.GetComponent<VehicleInterior>();var hinge=Group("Boarding • left rear door hinge",cabin,new Vector3(-1.13f,0,.35f));interior.leftDoor.SetParent(hinge,true);ride.boardingDoor=hinge;
  var windows=cabin.GetComponentsInChildren<Transform>().Where(t=>t.name=="Side window • clear glass"&&t.localPosition.z<0).ToArray();ride.leftGlass=windows.First(t=>t.localPosition.x<0);ride.rightGlass=windows.First(t=>t.localPosition.x>0);ride.leftGlass.SetParent(interior.leftDoor,true);
  // A narrow polished upper edge makes the window's motor travel visible in daylight.
  foreach(var glass in windows){var edge=Box("Window top edge",glass,new Vector3(0,.495f,0),new Vector3(1.1f,.004f,.99f),metal);}
  ride.lockPins=new Transform[4];for(int i=0;i<4;i++){int side=i%2==0?-1:1;var parent=i==0?interior.leftDoor:i==1?interior.rightDoor:cabin;ride.lockPins[i]=Box("Door lock pin "+i,parent,new Vector3(side*1.015f,1.03f,i<2?-.28f:1.45f),new Vector3(.022f,.055f,.022f),metal);}
  ride.accents=cabin.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Ambient light ribbon").ToArray();
  // Preserve dashboard, replacing only its static screen lettering with live diegetic data.
  foreach(var name in new[]{"Lettering • AUTONOMOUS","Lettering • 72","Lettering • km/h","Lettering • NORTHBOUND  •  A55","Navigation vehicle","Lettering • 72    ◦    DRIVE"})Remove(name);
  foreach(var t in cabin.GetComponentsInChildren<Transform>().Where(t=>t.name=="Navigation lane").ToArray())Object.DestroyImmediate(t.gameObject);
  var dash=interior.dashboard;
  ride.navigation=Text("Live navigation and distance","RIVERSIDE STATION\n0 MPH / AUTONOMOUS\nA55 NORTH",dash,new Vector3(-.03f,1.065f,1.493f),.026f);
  ride.instruments=Text("Live instrument speed","0\nMPH",dash,new Vector3(-.64f,1.07f,1.535f),.035f);
  ride.climateText=cabin.GetComponentsInChildren<TextMesh>().First(t=>t.text.Contains("21°")&&!t.text.Contains("COMFORT"));
  Remove("Rear passenger display bezel");Remove("Rear passenger display glass");Remove("Lettering • COMFORT\n21°\nRIDE ACTIVE");
  var panel=Group("Rear passenger display • physical services",cabin,new Vector3(.953f,1f,-.95f));panel.localRotation=Quaternion.Euler(0,90,0);
  Box("Passenger display satin bezel",panel,Vector3.zero,new Vector3(.505f,.455f,.027f),metal);Box("Passenger display recessed glass",panel,new Vector3(0,0,-.017f),new Vector3(.48f,.43f,.01f),dark);
  Text("Display marque","C H E C K M A T E   5 5",panel,new Vector3(0,.194f,-.026f),.012f);
  ride.displayStatus=Text("Ride status • tap for policy","RIVERSIDE STATION\nCOMFORT RIDE",panel,new Vector3(0,.139f,-.026f),.023f);
  var status=game.Registry.Get("passenger-display");status.transform.SetParent(panel,false);status.transform.localPosition=new Vector3(0,.139f,-.045f);status.transform.localRotation=Quaternion.identity;((BoxCollider)status.mobileHitProxy).size=new Vector3(.46f,.083f,.05f);status.movingPart=status.transform;status.label="Rear display · storage policy";
  ride.guidanceText=Text("Guidance heading","PASSENGER GUIDANCE",panel,new Vector3(0,.079f,-.026f),.012f);
  ride.guidanceOptions=new TextMesh[3];for(int i=0;i<3;i++){float y=.037f-i*.043f;Box("Guidance row "+i,panel,new Vector3(0,y,-.025f),new Vector3(.439f,.036f,.003f),dark);ride.guidanceOptions[i]=Text("Guidance option "+i,RideDirector.GuidanceName(i),panel,new Vector3(0,y,-.03f),.024f);Control("guidance-"+i,RideDirector.GuidanceName(i),panel,new Vector3(0,y,-.049f),new Vector3(.455f,.039f,.045f));}
  ride.displayAction=Text("Authentication action","RESTRICTED STORAGE\nPASSENGER AUTHENTICATION",panel,new Vector3(0,-.112f,-.03f),.018f);Control("authenticate","Prepare passenger authentication",panel,new Vector3(0,-.112f,-.049f),new Vector3(.455f,.068f,.045f));
  Box("Stop request key",panel,new Vector3(0,-.182f,-.026f),new Vector3(.438f,.046f,.004f),dark);var stop=Text("Stop request lettering","REQUEST STOP",panel,new Vector3(0,-.182f,-.031f),.024f);stop.color=new Color(.85f,.92f,.94f);Control("request-stop","Request a safe stop",panel,new Vector3(0,-.182f,-.049f),new Vector3(.455f,.047f,.045f));
  // Physical storage: a separate hinged lid, latch, visible paper and maintenance indicator.
  var glove=game.gameObject.AddComponent<GloveCompartment>();ride.glove=glove;glove.latch=game.Registry.Get("glovebox");glove.latch.response="That compartment is not available to passengers.";
  glove.lidPivot=Group("Glove compartment lid hinge",dash,new Vector3(.61f,.525f,1.59f));Find("Glovebox lid").SetParent(glove.lidPivot,true);Find("Glovebox latch").SetParent(glove.lidPivot,true);
  var slip=Box("Paper protruding from locked glovebox seam",dash,new Vector3(.85f,.819f,1.548f),new Vector3(.16f,.065f,.004f),paper);slip.localRotation=Quaternion.Euler(0,0,-8);
  var noteText=Text("Glovebox paper hint","SERVICE",dash,new Vector3(.85f,.821f,1.543f),.013f);noteText.color=new Color(.15f,.18f,.2f);noteText.transform.localRotation=slip.localRotation;
  Box("Storage maintenance indicator",dash,new Vector3(.33f,.78f,1.558f),new Vector3(.022f,.012f,.004f),blue);
  ride.storageText=Text("Glovebox security label","LOCKED / AUTH",dash,new Vector3(.6f,.705f,1.56f),.013f);
  var pocket=game.Registry.Get("passenger-pocket").transform.parent;Box("Previous passenger folded note",pocket,new Vector3(.1f,.54f,-.347f),new Vector3(.19f,.12f,.008f),paper);var nt=Text("Passenger note visible title","STORAGE\nSERVICE",pocket,new Vector3(.1f,.552f,-.354f),.016f);nt.color=new Color(.15f,.18f,.2f);
  var armControl=game.Registry.Get("armrest");armControl.transform.localPosition=new Vector3(.40f,.64f,-1.18f);((BoxCollider)armControl.mobileHitProxy).size=new Vector3(.16f,.2f,.22f);armControl.label="Fold armrest · side release";
  Box("Armrest side release tab",armControl.transform.parent,new Vector3(.425f,.64f,-1.18f),new Vector3(.03f,.025f,.1f),metal);
  var arm=Find("Folding rear armrest pivot");ride.trimPanel=Box("Removable passenger service trim",arm,new Vector3(0,.091f,.27f),new Vector3(.3f,.018f,.13f),metal);
  var trim=Control("security-trim","Inspect armrest service trim",arm,new Vector3(0,.13f,.26f),new Vector3(.32f,.08f,.16f));trim.movingPart=trim.transform;
  var contacts=Box("Authentication service contacts",arm,new Vector3(0,.093f,.27f),new Vector3(.19f,.005f,.08f),dark);for(int i=0;i<4;i++)Box("Gold service contact",arm,new Vector3(-.06f+i*.04f,.098f,.27f),new Vector3(.018f,.005f,.05f),metal);
  ride.dockCover=Box("Security dock sliding cover",arm,new Vector3(0,.089f,.09f),new Vector3(.3f,.014f,.16f),dark);
  ride.dockText=Text("Armrest authentication indicator","PASSENGER SECURITY\nSERVICE TRIM",arm,new Vector3(0,.103f,.09f),.012f);ride.dockText.transform.localRotation=Quaternion.Euler(90,0,0);
  // Lumbar adjustment now has a visible cushion movement in addition to its rocker.
  var lumbar=Box("Rear adjustable lumbar pad",interior.rearBench,new Vector3(-.66f,.77f,-1.475f),new Vector3(.5f,.23f,.065f),AssetDatabase.LoadAssetAtPath<Material>("Assets/Checkmate55/Materials/Graphite soft touch.mat"));var rearSeat=game.Registry.Get("rear-seat");rearSeat.action=ControlAction.Seat;rearSeat.movingPart=lumbar;rearSeat.moveOffset=new Vector3(0,0,.045f);
  game.Registry.Initialize();EditorUtility.SetDirty(game);EditorUtility.SetDirty(ride);
  PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.dataCaching=false;PlayerSettings.WebGL.threadsSupport=false;
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Checkmate55.unity",true)};EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("Foundation 02 additive migration saved. "+game.Registry.Count+" interaction proxies.");
 }
}
