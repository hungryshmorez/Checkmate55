using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Checkmate55;
using Object=UnityEngine.Object;
public static class FoundationBuilder {
 static Material leather,darkLeather,black,metal,wood,glass,screen,cyan,white,red,fabric,skin,pants,shoe,road,grass,concrete,green;
 static Mesh rounded;static Transform cabin;static Font font;
 static readonly List<Transform> segments=new List<Transform>(),traffic=new List<Transform>();
 const string Dir="Assets/Checkmate55/Materials/";
 [MenuItem("CHECKMATE 55/Build foundation")]
 public static void Build(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);segments.Clear();traffic.Clear();
  font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
  MakeMaterials();rounded=RoundMesh();Save(rounded,"Rounded.mesh");
  var root=new GameObject("CHECKMATE 55 • Foundation 01");
  var game=root.AddComponent<Checkmate55.SceneManager>();game.Registry=root.AddComponent<ObjectRegistry>();game.Motion=root.AddComponent<VehicleMotion>();game.Audio=root.AddComponent<Checkmate55.AudioManager>();game.Performance=root.AddComponent<PerformanceManager>();game.UI=root.AddComponent<CabinUI>();game.UI.game=game;
  cabin=new GameObject("Vehicle • permanent interior").transform;cabin.SetParent(root.transform);var interior=cabin.gameObject.AddComponent<VehicleInterior>();
  BuildShell(interior);BuildGlazing();BuildDashboard();BuildSeats();BuildRearCabin();BuildRoof();BuildBody(game);
  interior.dashboard=cabin.Find("Dashboard • full width");interior.roof=cabin.Find("Roof liner • ivory woven");interior.rearBench=cabin.Find("Rear bench • stitched leather");
  var camera=new GameObject("Seated adult • head camera");camera.transform.SetParent(cabin);camera.transform.localPosition=new Vector3(0,1.46f,-1.04f);camera.transform.localRotation=Quaternion.Euler(12,0,0);camera.tag="MainCamera";var cam=camera.AddComponent<Camera>();cam.fieldOfView=71;cam.nearClipPlane=.025f;cam.farClipPlane=400;cam.backgroundColor=new Color(.63f,.79f,.9f);cam.clearFlags=CameraClearFlags.Skybox;camera.AddComponent<AudioListener>();game.Player=camera.AddComponent<PlayerCamera>();game.Player.game=game;
  game.Interactions=root.AddComponent<InteractionSystem>();game.Interactions.game=game;game.Interactions.view=cam;
  BuildWorld();game.Motion.environment=GameObject.Find("Highway • pooled moving world").transform;game.Motion.roadSegments=segments.ToArray();game.Motion.traffic=traffic.ToArray();game.Performance.traffic=traffic.ToArray();
  var sun=new GameObject("Sun • clear afternoon").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.94f,.82f);sun.intensity=1.15f;sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.shadows=LightShadows.Soft;sun.shadowStrength=.65f;sun.shadowBias=.03f;sun.shadowNormalBias=.1f;
  RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.67f,.74f,.8f);RenderSettings.ambientEquatorColor=new Color(.53f,.55f,.53f);RenderSettings.ambientGroundColor=new Color(.32f,.31f,.28f);RenderSettings.ambientIntensity=1;
  RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=100;RenderSettings.fogEndDistance=340;RenderSettings.fogColor=new Color(.68f,.8f,.85f);
  var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetColor("_SkyTint",new Color(.52f,.64f,.76f));sky.SetFloat("_AtmosphereThickness",.75f);sky.SetFloat("_Exposure",1.25f);Save(sky,"DaylightSky.mat");RenderSettings.skybox=sky;RenderSettings.sun=sun;
  Light fill=new GameObject("Cabin • soft daylight fill").AddComponent<Light>();fill.type=LightType.Point;fill.transform.position=new Vector3(0,1.63f,-.7f);fill.range=4;fill.intensity=.65f;fill.color=new Color(.84f,.9f,1);fill.shadows=LightShadows.None;
  QualitySettings.antiAliasing=2;QualitySettings.shadowDistance=35;QualitySettings.vSyncCount=0;
  PlayerSettings.WebGL.template="PROJECT:Checkmate55";PlayerSettings.productName="CHECKMATE 55";PlayerSettings.companyName="Checkmate";PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.dataCaching=false;PlayerSettings.WebGL.threadsSupport=false;PlayerSettings.defaultWebScreenWidth=1280;PlayerSettings.defaultWebScreenHeight=800;PlayerSettings.runInBackground=true;
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/Scenes/Checkmate55.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Checkmate55.unity",true)};
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Checkmate55/Materials","Assets/Tripo"})){var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(material)EditorUtility.SetDirty(material);}
  AssetDatabase.SaveAssets();Selection.activeGameObject=camera;SceneView.lastActiveSceneView?.AlignViewToObject(camera.transform);Debug.Log("CHECKMATE55 foundation saved. "+Object.FindObjectsByType<PhysicalControl>(FindObjectsSortMode.None).Length+" physical interaction proxies.");
 }
 static void Save(Object o,string n){string p=Dir+n;var old=AssetDatabase.LoadAssetAtPath<Object>(p);if(old)AssetDatabase.DeleteAsset(p);AssetDatabase.CreateAsset(o,p);}
 static Material Mat(string name,Color c,float metallic=0,float smooth=.4f){var m=new Material(Shader.Find("Standard")){name=name,color=c,enableInstancing=true};m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);Save(m,name+".mat");return m;}
 static Material Glow(string name,Color c){var m=Mat(name,c,.15f,.6f);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*.65f);return m;}
 static void MakeMaterials(){
  leather=Mat("Warm stone leather",new Color(.55f,.51f,.44f),0,.32f);darkLeather=Mat("Graphite soft touch",new Color(.15f,.17f,.18f),0,.3f);black=Mat("Recessed charcoal",new Color(.025f,.035f,.042f),.1f,.4f);metal=Mat("Satin aluminium",new Color(.58f,.65f,.69f),.75f,.5f);wood=Mat("Smoked walnut",new Color(.32f,.19f,.095f),.05f,.43f);glass=Mat("Reflective glass",new Color(.38f,.55f,.6f),.65f,.9f);screen=Mat("Display glass",new Color(.018f,.042f,.061f),.2f,.58f);cyan=Glow("Ice blue accent",new Color(.25f,.75f,.82f));white=Glow("Ivory light",new Color(.9f,.89f,.8f));red=Glow("Buckle red",new Color(.61f,.048f,.035f));fabric=Mat("Woven floor",new Color(.12f,.13f,.13f),0,.15f);skin=Mat("Skin",new Color(.57f,.36f,.23f),0,.24f);pants=Mat("Navy cotton",new Color(.1f,.15f,.22f),0,.18f);shoe=Mat("Suede shoes",new Color(.2f,.22f,.23f),0,.15f);road=Mat("Highway asphalt",new Color(.29f,.31f,.32f),0,.05f);grass=Mat("Summer meadow",new Color(.39f,.49f,.2f),0,.1f);concrete=Mat("Pale concrete",new Color(.6f,.61f,.57f),0,.2f);green=Mat("Roadsign green",new Color(.045f,.24f,.17f),0,.3f);
  var tex=new Texture2D(128,128);var rng=new System.Random(55);for(int y=0;y<128;y++)for(int x=0;x<128;x++){float v=.85f+(float)rng.NextDouble()*.15f;tex.SetPixel(x,y,new Color(v,v,v));}tex.Apply();tex.wrapMode=TextureWrapMode.Repeat;Save(tex,"LeatherGrain.asset");leather.mainTexture=tex;leather.mainTextureScale=new Vector2(7,7);fabric.mainTexture=tex;fabric.mainTextureScale=new Vector2(12,12);pants.mainTexture=tex; pants.mainTextureScale=new Vector2(8,8);
  var wt=new Texture2D(256,64);for(int y=0;y<64;y++)for(int x=0;x<256;x++){float v=.65f+.26f*Mathf.PerlinNoise(x*.018f,y*.58f)+.08f*Mathf.Sin(y*2+x*.04f);wt.SetPixel(x,y,new Color(v,v*.91f,v*.78f));}wt.Apply();Save(wt,"WalnutGrain.asset");wood.mainTexture=wt;
 }
 static Mesh RoundMesh(){var v=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};const int n=8;foreach(var normal in axes){Vector3 u=new Vector3(normal.y,normal.z,normal.x),w=Vector3.Cross(normal,u);int offset=v.Count;for(int y=0;y<=n;y++)for(int x=0;x<=n;x++){Vector3 p=normal*.5f+u*((float)x/n-.5f)+w*((float)y/n-.5f);Vector3 inner=new Vector3(Mathf.Clamp(p.x,-.39f,.39f),Mathf.Clamp(p.y,-.39f,.39f),Mathf.Clamp(p.z,-.39f,.39f));Vector3 dir=(p-inner).normalized;v.Add(inner+dir*.11f);normals.Add(dir);uv.Add(new Vector2((float)x/n,(float)y/n));}for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=offset+y*(n+1)+x;tris.Add(a);tris.Add(a+1);tris.Add(a+n+2);tris.Add(a);tris.Add(a+n+2);tris.Add(a+n+1);}}
  var mesh=new Mesh{name="Soft bevel upholstery"};mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();return mesh;}
 static Transform Group(string name,Transform parent,Vector3 p){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
 static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material mat,bool soft=true){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;var mf=go.AddComponent<MeshFilter>();if(soft)mf.sharedMesh=rounded;else {var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);mf.sharedMesh=temp.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temp);}go.AddComponent<MeshRenderer>().sharedMaterial=mat;return go;}
 static GameObject Sphere(string name,Transform parent,Vector3 p,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static GameObject Cylinder(string name,Transform parent,Vector3 p,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
 static void Bar(string name,Transform parent,Vector3 a,Vector3 b,float width,Material mat){var t=Box(name,parent,(a+b)/2,new Vector3(width,(b-a).magnitude,width),mat).transform;t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
 static void Text(string str,Transform parent,Vector3 pos,float size,Color color,Vector3 euler=default){var g=new GameObject("Lettering • "+str);g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localEulerAngles=euler;var t=g.AddComponent<TextMesh>();t.text=str;t.font=font;t.fontSize=64;t.characterSize=size*.15625f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;g.GetComponent<MeshRenderer>().sharedMaterial=font.material;}
 static Transform Model(string name,Transform parent,Vector3 pos,float height,Vector3 rotation){var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tripo/"+name+"/"+name+".obj");if(!asset)return null;var wrapper=Group(name,parent,pos);var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(wrapper,false);go.transform.localRotation=Quaternion.Euler(rotation);Bounds b=new Bounds();bool first=true;foreach(var r in go.GetComponentsInChildren<Renderer>()){foreach(var material in r.sharedMaterials)if(material)material.enableInstancing=true;if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}float s=height/b.size.y;go.transform.localScale=Vector3.one*s;Vector3 center=wrapper.InverseTransformPoint(b.center);go.transform.localPosition=new Vector3(-center.x*s,(-center.y+b.size.y*.5f)*s,-center.z*s);wrapper.gameObject.layer=9;var collider=wrapper.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,height*.5f,0);collider.size=new Vector3(b.size.x*s,height,b.size.z*s);return wrapper;}
 static PhysicalControl Control(string id,string label,Transform parent,Vector3 pos,Vector3 proxy,ControlAction action,Transform move,string response,Vector3 offset=default,Vector3 rotate=default,bool locked=false){var t=Group("INTERACT • "+id,parent,pos);t.gameObject.layer=8;var box=t.gameObject.AddComponent<BoxCollider>();box.size=proxy;box.isTrigger=true;var c=t.gameObject.AddComponent<PhysicalControl>();c.id=id;c.label=label;c.mobileHitProxy=box;c.action=action;c.movingPart=move?move:t;c.response=response;c.moveOffset=offset;c.rotateOffset=rotate;c.locked=locked;return c;}
 static void BuildShell(VehicleInterior interior){
  Box("Chassis floor",cabin,new Vector3(0,-.06f,.1f),new Vector3(2.32f,.13f,4.4f),black);
  Box("Carpet",cabin,new Vector3(0,.025f,-.35f),new Vector3(2.13f,.04f,2.8f),fabric);
  for(int side=-1;side<=1;side+=2){float x=side*1.13f;var door=Group((side<0?"Left":"Right")+" rear door",cabin,Vector3.zero);if(side<0)interior.leftDoor=door;else interior.rightDoor=door;
   Box("Door lower upholstered panel",door,new Vector3(x,.46f,-.55f),new Vector3(.16f,.82f,1.76f),leather);
   Box("Door top sill",door,new Vector3(x,.94f,-.55f),new Vector3(.18f,.15f,1.8f),darkLeather);
   Box("Walnut door inlay",door,new Vector3(x-side*.086f,.8f,-.55f),new Vector3(.018f,.16f,1.62f),wood);
   Box("Aluminium belt trim",door,new Vector3(x-side*.1f,.7f,-.55f),new Vector3(.02f,.016f,1.62f),metal);
   Box("Ambient light ribbon",door,new Vector3(x-side*.112f,.686f,-.55f),new Vector3(.011f,.006f,1.55f),cyan,false);
   var arm=Box("Sculpted door arm pad",door,new Vector3(x-side*.075f,.51f,-.6f),new Vector3(.27f,.13f,.86f),darkLeather);
   Box("Handle recess",door,new Vector3(x-side*.1f,.78f,-.17f),new Vector3(.04f,.135f,.28f),black);
   var handle=Box("Satin pull handle",door,new Vector3(x-side*.135f,.785f,-.17f),new Vector3(.045f,.035f,.21f),metal);
   Control(side<0?"left-handle":"right-handle",(side<0?"Left":"Right")+" door handle",door,new Vector3(x-side*.17f,.78f,-.17f),new Vector3(.17f,.24f,.36f),ControlAction.Handle,handle.transform,"The handle pulls. The door stays secured while the car is moving.",new Vector3(-side*.03f,0,0),new Vector3(0,side*14,0));
   var sw=Box("Window rocker",door,new Vector3(x-side*.12f,.585f,-.53f),new Vector3(.085f,.025f,.12f),black);
   Box("Window switch symbol",sw.transform,new Vector3(0,.52f,0),new Vector3(.46f,.05f,.4f),metal);
   Control(side<0?"left-window":"right-window","Window switch",door,new Vector3(x-side*.13f,.59f,-.53f),new Vector3(.2f,.12f,.23f),ControlAction.Switch,sw.transform,"A quiet click. The windows are controlled by the vehicle.",new Vector3(0,-.008f,0),new Vector3(7,0,0));
   Box("Door storage pocket",door,new Vector3(x-side*.095f,.22f,-.64f),new Vector3(.06f,.17f,.55f),black);
   Box("Speaker bezel",door,new Vector3(x-side*.09f,.29f,-.03f),new Vector3(.035f,.32f,.34f),metal);
   Box("Speaker mesh",door,new Vector3(x-side*.112f,.29f,-.03f),new Vector3(.018f,.285f,.305f),darkLeather);
   for(int j=0;j<13;j++)Box("Speaker perforation",door,new Vector3(x-side*.125f,.17f+j*.02f,-.03f),new Vector3(.006f,.004f,.265f),black,false);
   Control(side<0?"left-speaker":"right-speaker","Cabin speaker",door,new Vector3(x-side*.14f,.29f,-.03f),new Vector3(.15f,.35f,.36f),ControlAction.Speaker,null,"Only the low sound of the road. No one answers.");
   // Open daylight glazing is bounded by real pillars, rubber seals and door sills.
   Box("B pillar",cabin,new Vector3(x,1.39f,.35f),new Vector3(.13f,.93f,.17f),leather);
   Box("B pillar black seal",cabin,new Vector3(x-side*.075f,1.4f,.4f),new Vector3(.035f,.85f,.035f),black);
   Bar("A pillar",cabin,new Vector3(x,1.02f,2.12f),new Vector3(side*.96f,1.91f,1.45f),.14f,leather);
   Bar("C pillar",cabin,new Vector3(x,1.02f,-1.59f),new Vector3(side*.96f,1.9f,-1.34f),.15f,leather);
   Bar("Roof window rail",cabin,new Vector3(side*1.04f,1.87f,-1.4f),new Vector3(side*1.04f,1.87f,1.51f),.13f,leather);
   Box("Front door card",cabin,new Vector3(x,.55f,1.22f),new Vector3(.13f,.86f,1.5f),darkLeather);
   Box("Front wood strip",cabin,new Vector3(x-side*.072f,.9f,1.16f),new Vector3(.025f,.13f,1.41f),wood);
   Bar("Seat belt webbing",cabin,new Vector3(x-side*.07f,1.62f,.3f),new Vector3(x-side*.09f,.28f,.21f),.035f,black);
   Box("Belt upper anchor",cabin,new Vector3(x-side*.08f,1.57f,.3f),new Vector3(.07f,.1f,.08f),darkLeather);
   Box("Door weatherstrip",cabin,new Vector3(x-side*.05f,1.045f,-.55f),new Vector3(.035f,.035f,1.72f),black);
   // Exterior side mirrors visible past front pillars.
   Box("Mirror housing",cabin,new Vector3(side*1.3f,1.04f,1.53f),new Vector3(.32f,.18f,.25f),darkLeather);
   Box("Mirror glass",cabin,new Vector3(side*1.3f,1.05f,1.398f),new Vector3(.26f,.12f,.015f),glass);
  }
  Box("Rear parcel shelf",cabin,new Vector3(0,1.04f,-1.96f),new Vector3(2.15f,.1f,.62f),darkLeather);
  Box("Rear window lower frame",cabin,new Vector3(0,1.13f,-2.22f),new Vector3(2.1f,.12f,.12f),leather);
 }

 static void BuildGlazing(){
  var glazing=Mat("Clear laminated glazing",new Color(.62f,.8f,.86f,.045f),.15f,.8f);
  glazing.SetFloat("_Mode",2);glazing.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);glazing.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);glazing.SetInt("_ZWrite",0);glazing.EnableKeyword("_ALPHABLEND_ON");glazing.renderQueue=3000;
  var windshield=Box("Laminated windshield",cabin,new Vector3(0,1.485f,1.81f),new Vector3(2.08f,.008f,1.06f),glazing,false);windshield.transform.localEulerAngles=new Vector3(50,0,0);windshield.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  for(int side=-1;side<=1;side+=2){foreach(float z in new[]{-.57f,1.2f}){var g=Box("Side window • clear glass",cabin,new Vector3(side*1.14f,1.43f,z),new Vector3(.008f,.81f,z<0?1.69f:1.55f),glazing,false);g.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;}}
 }
 static void Vent(Transform parent,Vector3 p,float width,float height){Box("Vent • satin frame",parent,p,new Vector3(width+.025f,height+.025f,.055f),metal);Box("Vent • dark cavity",parent,p+Vector3.back*.034f,new Vector3(width,height,.025f),black);for(int i=0;i<5;i++)Box("Vent louver",parent,p+new Vector3(0,-height*.37f+i*height*.185f,-.055f),new Vector3(width*.95f,.009f,.028f),darkLeather,false);Box("Vent adjuster",parent,p+new Vector3(0,0,-.073f),new Vector3(.03f,height*.55f,.02f),metal);}
 static void BuildDashboard(){
  var d=Group("Dashboard • full width",cabin,Vector3.zero);
  Box("Lower dashboard and firewall",d,new Vector3(0,.52f,1.98f),new Vector3(2.1f,.93f,.15f),darkLeather);
  Box("Driver knee bolster",d,new Vector3(-.65f,.69f,1.65f),new Vector3(.83f,.31f,.22f),darkLeather);
  Box("Sculpted dash cap",d,new Vector3(0,1.02f,1.91f),new Vector3(2.14f,.22f,.7f),darkLeather);
  Box("Passenger walnut fascia",d,new Vector3(.56f,.91f,1.61f),new Vector3(.96f,.18f,.055f),wood);
  Box("Dashboard bright trim",d,new Vector3(0,.807f,1.58f),new Vector3(2.05f,.018f,.03f),metal);
  Box("Glovebox surrounding",d,new Vector3(.61f,.65f,1.77f),new Vector3(.92f,.4f,.3f),darkLeather);
  Box("Glovebox lid",d,new Vector3(.61f,.67f,1.59f),new Vector3(.83f,.29f,.045f),leather);
  var latch=Box("Glovebox latch",d,new Vector3(.6f,.76f,1.555f),new Vector3(.16f,.025f,.03f),metal);
  Control("glovebox","Glove box · locked",d,new Vector3(.61f,.73f,1.51f),new Vector3(.32f,.16f,.14f),ControlAction.Locked,latch.transform,"The glove box latch resists. Electronically locked.",new Vector3(0,0,-.008f),default,true);
  Text("C H E C K M A T E   /   5 5",d,new Vector3(.61f,.92f,1.574f),.018f,new Color(.78f,.8f,.77f));
  // Wide integrated navigation, kept inside the dashboard.
  Box("Center display silver edge",d,new Vector3(-.03f,1.065f,1.546f),new Vector3(.66f,.3f,.052f),metal);
  Box("Center navigation glass",d,new Vector3(-.03f,1.065f,1.513f),new Vector3(.63f,.272f,.018f),screen);
  Text("AUTONOMOUS",d,new Vector3(-.03f,1.155f,1.5f),.022f,new Color(.63f,.84f,.86f));
  Text("72",d,new Vector3(-.21f,1.06f,1.499f),.065f,Color.white);Text("km/h",d,new Vector3(-.21f,1.007f,1.499f),.018f,Color.gray);
  for(int i=0;i<2;i++){var line=Box("Navigation lane",d,new Vector3(.015f+i*.12f,1.055f,1.498f),new Vector3(.005f,.12f,.003f),cyan,false);line.transform.localEulerAngles=new Vector3(0,0,i==0?-13:13);}
  Box("Navigation vehicle",d,new Vector3(.075f,1.032f,1.49f),new Vector3(.032f,.051f,.004f),white);
  Text("NORTHBOUND  •  A55",d,new Vector3(.1f,.975f,1.5f),.014f,new Color(.6f,.72f,.75f));
  Vent(d,new Vector3(-.23f,.81f,1.53f),.32f,.1f);Vent(d,new Vector3(.22f,.81f,1.53f),.32f,.1f);Text("△",d,new Vector3(0,.815f,1.47f),.035f,new Color(.8f,.24f,.12f));
  Vent(d,new Vector3(.92f,.965f,1.57f),.18f,.11f);Vent(d,new Vector3(-.92f,.965f,1.57f),.18f,.11f);
  Box("Instrument cowl",d,new Vector3(-.64f,1.06f,1.7f),new Vector3(.55f,.2f,.28f),black);
  Text("72    ◦    DRIVE",d,new Vector3(-.64f,1.07f,1.54f),.03f,new Color(.7f,.9f,.9f));
  var wheel=Model("steering-wheel",d,new Vector3(-.64f,.87f,1.19f),.36f,new Vector3(0,90,0));
  if(!wheel){var g=Group("Steering wheel",d,new Vector3(-.64f,1.04f,1.3f));Ring(g,Vector3.zero,.18f,.022f,darkLeather,Quaternion.Euler(90,0,0));Box("Wheel hub",g,Vector3.zero,new Vector3(.17f,.11f,.09f),black);}
  Box("Center tunnel",d,new Vector3(0,.34f,.72f),new Vector3(.4f,.55f,1.5f),darkLeather);
  Box("Console walnut",d,new Vector3(0,.61f,.91f),new Vector3(.37f,.035f,.63f),wood);
  Box("Drive selector",d,new Vector3(0,.68f,1.02f),new Vector3(.1f,.12f,.14f),metal);
  Box("Padded front center armrest",d,new Vector3(0,.64f,.4f),new Vector3(.36f,.13f,.53f),leather);
 }
 static void BuildSeats(){
  for(int side=-1;side<=1;side+=2){float x=side*.63f;var seat=Group(side<0?"Driver seat":"Front passenger seat",cabin,new Vector3(x,0,.47f));
   var model=Model("luxury-seat",seat,new Vector3(0,.12f,0),1.28f,new Vector3(0,90,0));
   if(model) model.localScale=new Vector3(.9f,1,1);
   if(!model){Box("Seat cushion",seat,new Vector3(0,.37f,0),new Vector3(.66f,.2f,.65f),leather);var back=Box("Contoured seat back",seat,new Vector3(0,.85f,-.16f),new Vector3(.66f,.85f,.22f),leather);back.transform.localEulerAngles=new Vector3(-8,0,0);Box("Headrest",seat,new Vector3(0,1.4f,-.17f),new Vector3(.44f,.3f,.22f),leather);}
   // Defined rear shell, piping and pocket stay readable from the passenger's view.
   var shell=Box("Seat back soft shell",seat,new Vector3(0,.74f,-.255f),new Vector3(.6f,.7f,.07f),darkLeather);shell.transform.localEulerAngles=new Vector3(-8,0,0);
   Box("Seat pocket rolled lip",seat,new Vector3(0,.52f,-.324f),new Vector3(.48f,.035f,.032f),leather);
   var pocket=Box("Stitched seat pocket",seat,new Vector3(0,.39f,-.31f),new Vector3(.48f,.24f,.035f),leather);
   for(int j=0;j<20;j++)Box("Pocket stitching",seat,new Vector3(-.22f+j*.023f,.292f,-.333f),new Vector3(.009f,.003f,.004f),metal,false);
   Control(side<0?"driver-pocket":"passenger-pocket","Seat pocket",seat,new Vector3(0,.44f,-.36f),new Vector3(.54f,.3f,.17f),ControlAction.Pocket,pocket.transform,"Empty. Your fingers find only the stitched lining.",new Vector3(0,0,-.015f));
   var adjust=Box("Seat adjustment rocker",seat,new Vector3(-side*.31f,.29f,-.15f),new Vector3(.06f,.04f,.15f),metal);
   Control(side<0?"driver-seat":"passenger-seat",side<0?"Driver seat adjustment":"Front passenger seat control",seat,new Vector3(-side*.32f,.3f,-.16f),new Vector3(.17f,.16f,.26f),ControlAction.Seat,seat,"The motor hums. The seat slides along its rails.",new Vector3(0,0,.17f));
   Box("Seat rail",seat,new Vector3(-.2f,.1f,0),new Vector3(.04f,.08f,.68f),black);Box("Seat rail",seat,new Vector3(.2f,.1f,0),new Vector3(.04f,.08f,.68f),black);
  }
  var rc=Group("Rear console",cabin,new Vector3(0,0,.04f));
  Box("Rear center console housing",rc,new Vector3(0,.4f,0),new Vector3(.39f,.64f,.3f),darkLeather);
  Vent(rc,new Vector3(-.094f,.56f,-.16f),.145f,.14f);Vent(rc,new Vector3(.094f,.56f,-.16f),.145f,.14f);
  var clim=Box("Rear climate glass",rc,new Vector3(0,.398f,-.18f),new Vector3(.33f,.105f,.025f),screen);
  Text("−     21°     +",rc,new Vector3(0,.405f,-.198f),.034f,new Color(.68f,.87f,.9f));
  Control("climate","Rear climate controls",rc,new Vector3(0,.4f,-.22f),new Vector3(.36f,.15f,.15f),ControlAction.Climate,clim.transform,"Cool air flows through the rear vents.");
  Box("Charging inset",rc,new Vector3(0,.23f,-.17f),new Vector3(.3f,.11f,.035f),black);
  foreach(float x in new[]{-.075f,.075f}){Box("USB-C chrome rim",rc,new Vector3(x,.23f,-.193f),new Vector3(.058f,.025f,.012f),metal);Box("USB-C socket",rc,new Vector3(x,.23f,-.201f),new Vector3(.043f,.012f,.006f),black);}
  Control("charging","USB-C charging ports",rc,new Vector3(0,.235f,-.235f),new Vector3(.33f,.115f,.14f),ControlAction.USB,null,"Two powered USB-C ports. Nothing is connected.");
  Text("USB-C",rc,new Vector3(0,.17f,-.197f),.012f,new Color(.65f,.69f,.7f));
 }
 static void Ring(Transform p,Vector3 center,float radius,float thick,Material mat,Quaternion rotation){for(int i=0;i<32;i++){float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;Bar("Circular rim",p,center+rotation*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),center+rotation*new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),thick,mat);}}
 static void BuildRearCabin(){
  var bench=Group("Rear bench • stitched leather",cabin,Vector3.zero);Box("Rear bench cushion",bench,new Vector3(0,.35f,-1.25f),new Vector3(2.05f,.25f,.86f),leather);Box("Rear backrest",bench,new Vector3(0,.85f,-1.63f),new Vector3(2.05f,.91f,.2f),leather);
  for(int i=-1;i<=1;i++){float x=i*.68f;Box("Rear sculpted seat insert",bench,new Vector3(x,.485f,-1.21f),new Vector3(.56f,.024f,.65f),darkLeather);Box("Rear backrest insert",bench,new Vector3(x,.86f,-1.505f),new Vector3(.55f,.64f,.035f),darkLeather);Box("Rear headrest",bench,new Vector3(x,1.38f,-1.59f),new Vector3(.4f,.26f,.18f),leather);for(int j=0;j<6;j++)Box("Rear seat transverse seams",bench,new Vector3(x,.502f,-1.43f+j*.085f),new Vector3(.49f,.006f,.006f),leather,false);}
  var arm=Group("Folding rear armrest pivot",bench,new Vector3(.62f,.61f,-1.42f));Box("Rear armrest pad",arm,new Vector3(0,0,.33f),new Vector3(.37f,.16f,.71f),leather);
  Box("Cupholder inset",arm,new Vector3(0,.084f,.49f),new Vector3(.31f,.02f,.28f),metal);
  foreach(float x in new[]{-.083f,.083f}){Cylinder("Cupholder well",arm,new Vector3(x,.099f,.49f),new Vector3(.133f,.009f,.133f),black);Ring(arm,new Vector3(x,.112f,.49f),.064f,.009f,darkLeather,Quaternion.identity);}
  Control("armrest","Fold rear armrest",bench,new Vector3(.62f,.62f,-1.18f),new Vector3(.39f,.2f,.21f),ControlAction.Armrest,arm,"The padded armrest folds on its hinge.",default,new Vector3(-88,0,0));
  Control("cupholders","Cupholders",arm,new Vector3(0,.14f,.49f),new Vector3(.35f,.15f,.29f),ControlAction.Cupholder,null,"Two empty cupholders, lined with soft rubber.");
  var buckle=Box("Seat belt buckle",bench,new Vector3(-.35f,.5f,-1.03f),new Vector3(.075f,.13f,.095f),black);buckle.transform.localEulerAngles=new Vector3(15,0,-12);
  Box("Red release button",bench,new Vector3(-.35f,.56f,-1.02f),new Vector3(.06f,.018f,.063f),red);
  Control("belt","Seat belt buckle",bench,new Vector3(-.35f,.55f,-1.03f),new Vector3(.19f,.2f,.2f),ControlAction.Buckle,buckle.transform,"The buckle clicks. The belt rests against your lap.",new Vector3(0,-.025f,.02f));
  var seatSwitch=Box("Rear seat comfort switch",bench,new Vector3(-.86f,.53f,-.84f),new Vector3(.15f,.035f,.07f),metal);
  Control("rear-seat","Rear seat comfort",bench,new Vector3(-.86f,.55f,-.84f),new Vector3(.24f,.13f,.17f),ControlAction.Switch,seatSwitch.transform,"The lumbar support adjusts slightly behind you.",new Vector3(0,-.01f,0));
  // Passenger display is a physical panel on the door, never a floating HUD.
  Box("Rear passenger display bezel",cabin,new Vector3(.99f,.83f,-.95f),new Vector3(.035f,.23f,.31f),metal);
  var disp=Box("Rear passenger display glass",cabin,new Vector3(.968f,.83f,-.95f),new Vector3(.012f,.205f,.285f),screen);
  Text("COMFORT\n21°\nRIDE ACTIVE",cabin,new Vector3(.955f,.83f,-.95f),.026f,new Color(.68f,.86f,.89f),new Vector3(0,90,0));
  Control("passenger-display","Rear passenger display",cabin,new Vector3(.93f,.83f,-.95f),new Vector3(.15f,.25f,.33f),ControlAction.Display,disp.transform,"Ride active. Destination access is restricted.");
  for(int side=-1;side<=1;side+=2){float x=side*.56f;Box("Rear floor mat bound edge",cabin,new Vector3(x,.052f,-.38f),new Vector3(.77f,.025f,1.02f),black);Box("Woven floor mat",cabin,new Vector3(x,.067f,-.38f),new Vector3(.72f,.013f,.96f),fabric);for(int j=0;j<12;j++)Box("Mat rib",cabin,new Vector3(x-.32f+j*.058f,.078f,-.38f),new Vector3(.011f,.005f,.87f),darkLeather,false);}
 }
 static void BuildRoof(){
  var roof=Group("Roof liner • ivory woven",cabin,Vector3.zero);Box("Headliner",roof,new Vector3(0,1.99f,-.02f),new Vector3(2.14f,.15f,3.22f),leather);
  Box("Panoramic shade surround",roof,new Vector3(0,1.9f,-.3f),new Vector3(1.5f,.025f,1.6f),darkLeather);Box("Panoramic closed shade",roof,new Vector3(0,1.875f,-.3f),new Vector3(1.43f,.018f,1.52f),leather);
  Box("Windshield header",roof,new Vector3(0,1.86f,1.5f),new Vector3(2.02f,.17f,.27f),leather);
  for(int side=-1;side<=1;side+=2){Box("Sun visor",roof,new Vector3(side*.59f,1.775f,1.43f),new Vector3(.58f,.065f,.27f),leather);
   var g=Group("Grab handle",roof,new Vector3(side*.97f,1.77f,-.64f));Bar("Handle grip",g,new Vector3(0,0,-.12f),new Vector3(0,0,.12f),.037f,leather);Bar("Handle mount",g,new Vector3(0,0,-.12f),new Vector3(side*.06f,.07f,-.12f),.038f,leather);Bar("Handle mount",g,new Vector3(0,0,.12f),new Vector3(side*.06f,.07f,.12f),.038f,leather);
  }
  Box("Overhead light cluster",roof,new Vector3(0,1.82f,.68f),new Vector3(.39f,.065f,.32f),darkLeather);var lamp=Box("Overhead lamp lens",roof,new Vector3(0,1.779f,.68f),new Vector3(.25f,.02f,.17f),white);
  var light=new GameObject("Reading light").AddComponent<Light>();light.transform.SetParent(roof);light.transform.localPosition=new Vector3(0,1.71f,.62f);light.type=LightType.Point;light.color=new Color(1,.82f,.58f);light.intensity=.75f;light.range=2.5f;light.enabled=false;
  var ctrl=Control("overhead-light","Overhead reading light",roof,new Vector3(0,1.73f,.68f),new Vector3(.44f,.17f,.37f),ControlAction.Lamp,lamp.transform,"The reading light clicks softly.");ctrl.cabinLight=light;
  Bar("Mirror stalk",roof,new Vector3(0,1.8f,1.58f),new Vector3(0,1.57f,1.54f),.035f,black);
  Box("Rear view mirror housing",roof,new Vector3(0,1.57f,1.5f),new Vector3(.41f,.16f,.07f),black);Box("Rear view mirror surface",roof,new Vector3(0,1.57f,1.459f),new Vector3(.37f,.125f,.015f),glass);
  // Readable reflected horizon hint, inexpensive on mobile.
  Box("Mirror horizon",roof,new Vector3(0,1.556f,1.448f),new Vector3(.34f,.018f,.002f),concrete);Text("•",roof,new Vector3(.16f,1.58f,1.443f),.022f,new Color(.23f,.8f,.5f));
 }
 static void Limb(string name,Transform p,Vector3 a,Vector3 b,float width,float depth,Material mat){var g=Sphere(name,p,(a+b)/2,new Vector3(width,(b-a).magnitude+width*.4f,depth),mat);g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);}
 static void BuildBody(Checkmate55.SceneManager game){var b=Group("Player • seated body, no ray colliders",cabin,Vector3.zero);var body=b.gameObject.AddComponent<PlayerBody>();body.motion=game.Motion;
  Box("Torso jacket edge",b,new Vector3(0,.86f,-1.3f),new Vector3(.47f,.52f,.25f),pants);
  for(int side=-1;side<=1;side+=2){float x=side*.18f;Limb("Trouser thigh",b,new Vector3(x,.58f,-1.17f),new Vector3(x*1.26f,.5f,-.53f),.26f,.25f,pants);Sphere("Knee",b,new Vector3(x*1.26f,.49f,-.49f),new Vector3(.25f,.24f,.25f),pants);Limb("Trouser shin",b,new Vector3(x*1.26f,.46f,-.48f),new Vector3(side*.31f,.17f,-.08f),.19f,.18f,pants);
   Box("Shoe sole",b,new Vector3(side*.31f,.095f,.01f),new Vector3(.2f,.055f,.37f),black);Box("Suede shoe",b,new Vector3(side*.31f,.145f,0),new Vector3(.185f,.11f,.34f),shoe);for(int j=0;j<4;j++)Box("Shoe lace",b,new Vector3(side*.31f,.205f,-.08f+j*.028f),new Vector3(.1f,.007f,.009f),metal,false);
   Limb("Jacket sleeve upper",b,new Vector3(side*.25f,1.05f,-1.29f),new Vector3(side*.36f,.76f,-1.0f),.17f,.18f,pants);Limb("Jacket forearm",b,new Vector3(side*.36f,.76f,-1),new Vector3(side*.24f,.69f,-.73f),.14f,.14f,pants);
   var hand=Box("Resting hand",b,new Vector3(side*.23f,.655f,-.67f),new Vector3(.11f,.055f,.14f),skin);hand.transform.localEulerAngles=new Vector3(7,side*12,side*5);
   for(int j=0;j<4;j++)Limb("Relaxed finger",b,new Vector3(side*.23f-.037f+j*.024f,.646f,-.62f),new Vector3(side*.23f-.037f+j*.024f,.63f,-.54f+(j==0?.016f:0)),.023f,.024f,skin);
   Limb("Thumb",b,new Vector3(side*.18f,.65f,-.68f),new Vector3(side*.15f,.635f,-.61f),.033f,.034f,skin);
  }
  Bar("Lap belt",b,new Vector3(-.34f,.67f,-1.07f),new Vector3(.33f,.67f,-1.05f),.041f,black);
 }
 static void BuildWorld(){var world=Group("Highway • pooled moving world",null,Vector3.zero);Box("Distant meadow",world,new Vector3(0,-.69f,100),new Vector3(650,.3f,650),grass,false);
  var hillMat=Mat("Distant green hills",new Color(.36f,.46f,.3f),0,.05f);for(int i=0;i<15;i++){Sphere("Rolling countryside",world,new Vector3((i%2==0?-1:1)*(65+i*3),3,50+i*24),new Vector3(65,12+i%4*6,70),hillMat);}
  for(int i=0;i<10;i++){var seg=Group("Road pool "+i,world,new Vector3(0,0,i*40-40));segments.Add(seg);
   Box("Asphalt carriageway",seg,new Vector3(0,-.48f,0),new Vector3(13,.12f,40.05f),road,false);
   for(int side=-1;side<=1;side+=2){Box("Shoulder line",seg,new Vector3(side*5.5f,-.409f,0),new Vector3(.12f,.008f,40),white,false);Box("Road shoulder",seg,new Vector3(side*6.4f,-.46f,0),new Vector3(1.1f,.08f,40),concrete,false);Box("Guardrail",seg,new Vector3(side*7.15f,.16f,0),new Vector3(.13f,.22f,40),metal,false);for(int j=0;j<5;j++)Box("Guardrail post",seg,new Vector3(side*7.15f,-.08f,-17+j*8),new Vector3(.13f,.7f,.13f),concrete,false);
    for(int j=0;j<2;j++){var tree=Model("roadside-tree",seg,new Vector3(side*(11+j*8+i%3),-.4f,-12+j*22),5.5f+i%3,new Vector3(0,i*37,0));if(!tree){Cylinder("Tree trunk",seg,new Vector3(side*13,1,-10+j*20),new Vector3(.4f,1.5f,.4f),wood);Sphere("Tree crown",seg,new Vector3(side*13,4,-10+j*20),new Vector3(5,5,5),grass);}}
   }
   foreach(float x in new[]{-1.75f,1.75f})for(int j=0;j<5;j++)Box("Lane dash",seg,new Vector3(x,-.408f,-17+j*8),new Vector3(.13f,.009f,3.8f),white,false);
   if(i%3==1){Box("Sign post",seg,new Vector3(8.2f,1.4f,3),new Vector3(.12f,3.7f,.12f),metal);Box("Highway sign",seg,new Vector3(8.2f,3.1f,3),new Vector3(3.2f,1.35f,.12f),green);Text("A55   NORTH\nRIVERSIDE   ↗\nEXIT  07",seg,new Vector3(8.2f,3.1f,2.925f),.24f,Color.white);Box("Exit slip road",seg,new Vector3(9,-.49f,9),new Vector3(5,.08f,28),road,false);}
   if(i==5){Box("Overpass deck",seg,new Vector3(0,5.1f,0),new Vector3(35,.7f,6),concrete);Box("Overpass parapet",seg,new Vector3(0,5.75f,-2.7f),new Vector3(35,.75f,.25f),concrete);for(int side=-1;side<=1;side+=2)Box("Bridge support",seg,new Vector3(side*9.5f,2.25f,0),new Vector3(1.2f,5.5f,2.1f),concrete);for(int j=0;j<18;j++)Box("Bridge railing",seg,new Vector3(-17+j*2,6.25f,-2.7f),new Vector3(.06f,.55f,.06f),metal,false);Box("Bridge top rail",seg,new Vector3(0,6.52f,-2.7f),new Vector3(35,.06f,.06f),metal,false);}
   if(i%3==2){for(int j=0;j<3;j++){float x=24+j*8;Box("Distant office",seg,new Vector3(x,3+j*1.5f,0),new Vector3(6,7+j*3,9),concrete);for(int k=0;k<5;k++)Box("Office glazing band",seg,new Vector3(x,1+k*1.4f,-4.55f),new Vector3(5,.7f,.05f),glass,false);}}
  }
  for(int i=0;i<10;i++){float x=(i%3-1)*3.5f;float z=24+i*19;var car=Model("highway-sedan",world,new Vector3(x,-.38f,z),1.5f,new Vector3(0,90,0));if(!car){car=Group("Traffic sedan",world,new Vector3(x,-.38f,z));Box("Sedan body",car,new Vector3(0,.6f,0),new Vector3(1.8f,.8f,4.2f),metal);Box("Sedan cabin",car,new Vector3(0,1.2f,-.1f),new Vector3(1.5f,.65f,2.2f),glass);}traffic.Add(car);}
 }
}
