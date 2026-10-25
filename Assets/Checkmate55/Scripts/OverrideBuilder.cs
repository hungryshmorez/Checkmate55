using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Checkmate55 {
// Builds everything CHECKMATE 55: OVERRIDE adds to the cabin and the road. It runs once, at startup, from the existing scene objects,
// so the authored foundation scene needs no manual changes. Units are metres in the permanent cabin space (+Z forward, Y up).
public sealed class OverrideBuilder {
 readonly SceneManager game;readonly Transform cabin;readonly Font font;
 public Material dark,metal,glass,screen,paper,led,ledRed,tintMat;
 // ---- radio deck
 public TextMesh radioDisplay,radioTitle;public Renderer[] presetLed=new Renderer[6];public Transform padDot;public readonly Dictionary<string,Transform> deckButtons=new Dictionary<string,Transform>();
 // ---- climate / dock / ports / phone
 public TextMesh climateReadout;public Transform dockCover,dockCard,dockRoot;public Renderer dockLed;public TextMesh dockLabel;
 public PhysicalControl portService,portPhone,phoneScreen,dock,climateMinus,climatePlus,climateFan,tintLeft,tintRight;
 public Transform phoneBody,phoneCable;public TextMesh phoneText;public Renderer phoneGlow;
 // ---- glove box items
 public Transform itemsRoot;public readonly Dictionary<string,Transform> items=new Dictionary<string,Transform>();public readonly Dictionary<string,PhysicalControl> itemControls=new Dictionary<string,PhysicalControl>();
 // ---- glass, visors, markers
 public readonly List<Renderer> tint=new List<Renderer>();public readonly List<Transform> visors=new List<Transform>();public readonly List<Vector3> visorHome=new List<Vector3>();public readonly List<Quaternion> visorRot=new List<Quaternion>();
 public Transform[] markers=new Transform[3];public TextMesh[] markerText=new TextMesh[3];
 // ---- extra cabin text
 public TextMesh warningText,destinationBanner;
 public Transform rideControlRoot;public TextMesh rideLabel;public Renderer rideLamp;
 public OverrideBuilder(SceneManager g){game=g;cabin=g.Player.transform.parent;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");}
 // ---------------------------------------------------------------------------------------------------------------- helpers
 Transform Find(string name)=>cabin.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
 static Transform Group(string n,Transform p,Vector3 pos){var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=pos;return t;}
 static Transform Box(string n,Transform p,Vector3 pos,Vector3 size,Material m,PrimitiveType type=PrimitiveType.Cube){var t=GameObject.CreatePrimitive(type).transform;t.name=n;t.SetParent(p,false);t.localPosition=pos;t.localScale=size;var r=t.GetComponent<Renderer>();r.sharedMaterial=m;Object.Destroy(t.GetComponent<Collider>());return t;}
 public TextMesh Text(string n,Transform p,Vector3 pos,string text,float size,Color color,Vector3 euler,TextAnchor anchor=TextAnchor.MiddleCenter){
  var t=Group("Lettering • "+n,p,pos);t.localEulerAngles=euler;var tm=t.gameObject.AddComponent<TextMesh>();tm.font=font;tm.GetComponent<Renderer>().sharedMaterial=font.material;WorldTextDepth.Apply(tm);tm.fontSize=64;tm.characterSize=size*.15625f;tm.text=text;tm.anchor=anchor;tm.alignment=anchor==TextAnchor.MiddleLeft?TextAlignment.Left:TextAlignment.Center;tm.color=color;return tm;
 }
 PhysicalControl Make(string id,string label,Transform parent,Vector3 pos,Vector3 proxy,ControlAction action,Transform moving,string response,Vector3 offset=default,Vector3 rotate=default){
  var t=Group("INTERACT • "+id,parent,pos);t.gameObject.layer=8;var box=t.gameObject.AddComponent<BoxCollider>();box.size=proxy;box.isTrigger=true;
  var c=t.gameObject.AddComponent<PhysicalControl>();c.id=id;c.label=label;c.mobileHitProxy=box;c.action=action;c.movingPart=moving?moving:t;c.response=response;c.moveOffset=offset;c.rotateOffset=rotate;
  game.Registry.Register(c);return c;
 }
 Material M(string name,Color c,float gloss=.4f,float metallic=0)=>Mats.Solid(name,c,gloss,metallic);
 // ---------------------------------------------------------------------------------------------------------------- build
 public void Build(){
  dark=Mats.Find("Graphite soft touch")??M("dark",new Color(.15f,.17f,.18f));metal=Mats.Find("Satin aluminium")??Mats.Metal("metal",new Color(.58f,.65f,.69f));screen=Mats.Find("Display glass")??M("screen",new Color(.02f,.04f,.06f));
  paper=Mats.Find("Ivory light")??M("paper",new Color(.9f,.89f,.8f));led=Mats.Glow("LED cyan",new Color(.25f,.9f,.95f),1.1f);ledRed=Mats.Glow("LED red",new Color(1f,.12f,.06f),1.2f);
  glass=Mats.Find("Clear laminated glazing");tintMat=Mats.Glass("Privacy tint",new Color(0,0,0,0));
  BuildRadioDeck();BuildClimate();BuildPortsDockPhone();BuildGloveItems();BuildGlassAndVisors();BuildMarkers();BuildCabinText();
 }
 // ---- RADIO ------------------------------------------------------------------------------------------------------------------------
 void BuildRadioDeck(){
  // Centre console, forward of the armrest, tilted toward the rear seats so it can be read and reached from the back.
  var deck=Group("Radio deck • five presets",cabin,new Vector3(0,.636f,.86f));deck.localRotation=Quaternion.Euler(-24,0,0);
  Box("Deck plate",deck,new Vector3(0,.01f,0),new Vector3(.345f,.02f,.27f),dark);Box("Deck bezel",deck,new Vector3(0,.0205f,0),new Vector3(.352f,.003f,.277f),metal);
  Box("Display glass",deck,new Vector3(0,.023f,.087f),new Vector3(.31f,.006f,.072f),screen);
  radioTitle=Text("Radio station",deck,new Vector3(0,.0275f,.103f),"PRESET 1",.0145f,new Color(.7f,.95f,.95f),new Vector3(90,0,0));
  radioDisplay=Text("Radio readout",deck,new Vector3(0,.0275f,.071f),"",.0105f,new Color(.45f,.8f,.85f),new Vector3(90,0,0));
  for(int i=1;i<=5;i++){
   float x=-.124f+(i-1)*.062f;var b=Box("Preset "+i,deck,new Vector3(x,.026f,.012f),new Vector3(.052f,.012f,.04f),dark);
   Box("Preset rim",b,new Vector3(0,.55f,0),new Vector3(1.04f,.14f,1.06f),metal);
   var l=Box("Preset LED",deck,new Vector3(x,.0335f,.036f),new Vector3(.03f,.002f,.004f),Mats.Glow("Preset LED "+i,new Color(.18f,.35f,.38f),.15f));presetLed[i]=l.GetComponent<Renderer>();
   Text("Preset "+i,deck,new Vector3(x,.0285f,.004f),""+i,.02f,Color.white,new Vector3(90,0,0));
   var c=Make("radio-p"+i,"Radio preset "+i,deck,new Vector3(x,.032f,.012f),new Vector3(.058f,.05f,.046f),ControlAction.Switch,b,"",new Vector3(0,-.004f,0));deckButtons["p"+i]=b;
  }
  string[] ids={"radio-vol-down","radio-vol-up","radio-mute"},labels={"VOL −","VOL +","MUTE"},names={"Volume down","Volume up","Mute"};
  for(int i=0;i<3;i++){
   float x=-.135f+i*.062f;var b=Box(labels[i],deck,new Vector3(x,.026f,-.075f),new Vector3(.052f,.012f,.04f),dark);Box("Rim",b,new Vector3(0,.55f,0),new Vector3(1.04f,.14f,1.06f),metal);
   Text(labels[i],deck,new Vector3(x,.0285f,-.075f),labels[i],.0105f,Color.white,new Vector3(90,0,0));
   Make(ids[i],names[i],deck,new Vector3(x,.032f,-.075f),new Vector3(.058f,.05f,.046f),ControlAction.Switch,b,"",new Vector3(0,-.004f,0));
  }
  // Balance / fader cross: one physical joystick pad.
  var pad=Group("Balance and fader pad",deck,new Vector3(.108f,.026f,-.07f));
  Box("Pad well",pad,new Vector3(0,-.002f,0),new Vector3(.105f,.006f,.1f),screen);Text("Pad caption",pad,new Vector3(0,.0065f,.051f),"BAL / FADE",.0085f,new Color(.5f,.85f,.9f),new Vector3(90,0,0));
  string[] pid={"radio-bal-l","radio-bal-r","radio-fade-f","radio-fade-r"},plab={"Balance left","Balance right","Fader front","Fader rear"},glyph={"<",">","^","v"};Vector3[] pp={new Vector3(-.036f,0,0),new Vector3(.036f,0,0),new Vector3(0,0,.034f),new Vector3(0,0,-.034f)};
  for(int i=0;i<4;i++){
   var b=Box(plab[i],pad,pp[i]+new Vector3(0,.003f,0),new Vector3(.032f,.01f,.028f),dark);Text(plab[i],pad,pp[i]+new Vector3(0,.0105f,0),glyph[i],.016f,Color.white,new Vector3(90,0,0));
   Make(pid[i],plab[i],pad,pp[i]+new Vector3(0,.008f,0),new Vector3(.04f,.05f,.036f),ControlAction.Switch,b,"",new Vector3(0,-.004f,0));
  }
  // RIDE GAME: a wide button on the front edge of the deck with a status lamp and a live counter / move clock
  var rideRoot=Group("Ride game button",deck,new Vector3(-.06f,.0f,-.119f));
  var rideBtn=Box("Ride game key",rideRoot,new Vector3(0,.032f,0),new Vector3(.2f,.012f,.032f),dark);
  var rideLed=Box("Ride game lamp",rideRoot,new Vector3(-.088f,.039f,0),new Vector3(.012f,.004f,.012f),Mats.Glow("Ride game lamp",new Color(.25f,.9f,.95f),1f),PrimitiveType.Sphere);
  var rideText=Text("Ride game",rideRoot,new Vector3(.006f,.0395f,0),"RIDE GAME",.0075f,Color.white,new Vector3(90,0,0));
  Make("ridegame","Ride game: your full game of chess against the car",rideRoot,new Vector3(0,.04f,0),new Vector3(.22f,.05f,.046f),ControlAction.Switch,rideBtn,"",new Vector3(0,-.004f,0));
  rideControlRoot=rideRoot;rideLabel=rideText;rideLamp=rideLed.GetComponent<Renderer>();
  padDot=Box("Mix position",pad,new Vector3(0,.0075f,0),new Vector3(.013f,.005f,.013f),led,PrimitiveType.Sphere);
 }
 // ---- CLIMATE ------------------------------------------------------------------------------------------------------------------------
 void BuildClimate(){
  var glassT=Find("Rear climate glass");if(glassT==null)return;
  var rc=glassT.parent;Vector3 g=glassT.localPosition;
  // The foundation drew "−   21°   +" as one lettering; take it over and add real buttons and a fan knob.
  var old=cabin.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t=>t.text.Contains("°")&&!t.text.Contains("COMFORT"));
  if(old!=null){climateReadout=old;}
  else climateReadout=Text("Climate",rc,new Vector3(0,g.y+.007f,g.z-.018f),"21°",.034f,new Color(.68f,.87f,.9f),Vector3.zero);
  var minus=Box("Climate minus",rc,new Vector3(-.118f,g.y,g.z-.021f),new Vector3(.062f,.062f,.012f),dark);Box("Rim",minus,new Vector3(0,0,.4f),new Vector3(1.1f,1.1f,.1f),metal);Text("minus",minus,new Vector3(0,0,-.7f),"−",.55f,Color.white,Vector3.zero);
  var plus=Box("Climate plus",rc,new Vector3(.118f,g.y,g.z-.021f),new Vector3(.062f,.062f,.012f),dark);Box("Rim",plus,new Vector3(0,0,.4f),new Vector3(1.1f,1.1f,.1f),metal);Text("plus",plus,new Vector3(0,0,-.7f),"+",.55f,Color.white,Vector3.zero);
  var fan=Box("Fan knob",rc,new Vector3(0,g.y-.072f,g.z-.026f),new Vector3(.05f,.012f,.05f),metal,PrimitiveType.Cylinder);fan.localRotation=Quaternion.Euler(90,0,0);
  Text("fan",rc,new Vector3(0,g.y-.072f,g.z-.044f),"FAN",.011f,new Color(.7f,.88f,.9f),Vector3.zero);
  climateMinus=Make("climate-minus","Cooler",rc,new Vector3(-.118f,g.y,g.z-.07f),new Vector3(.085f,.085f,.05f),ControlAction.Switch,minus,"",new Vector3(0,0,.006f));
  climatePlus=Make("climate-plus","Warmer",rc,new Vector3(.118f,g.y,g.z-.07f),new Vector3(.085f,.085f,.05f),ControlAction.Switch,plus,"",new Vector3(0,0,.006f));
  climateFan=Make("climate-fan","Fan speed",rc,new Vector3(0,g.y-.072f,g.z-.07f),new Vector3(.075f,.06f,.05f),ControlAction.Switch,fan,"");
 }
 // ---- USB PORTS, SERVICE DOCK, PHONE -----------------------------------------------------------------------------------------------------
 void BuildPortsDockPhone(){
  var charging=game.Registry.Get("charging");var rc=charging!=null?charging.transform.parent:cabin;
  // Port controls sit just in front of the existing group control so they are hit first.
  Vector3 basePos=charging!=null?charging.transform.localPosition:new Vector3(0,.235f,-.235f);
  portPhone=Make("usb-port-phone","USB-C port · charge",rc,basePos+new Vector3(-.075f,0,-.05f),new Vector3(.07f,.07f,.07f),ControlAction.USB,null,"");
  portService=Make("usb-port-service","USB-C port · service",rc,basePos+new Vector3(.075f,0,-.05f),new Vector3(.07f,.07f,.07f),ControlAction.USB,null,"");
  Text("charge port",rc,basePos+new Vector3(-.075f,-.05f,.04f),"CHG",.011f,new Color(.7f,.8f,.82f),new Vector3(0,0,0));
  Text("service port",rc,basePos+new Vector3(.075f,-.05f,.04f),"SVC ⚙",.011f,new Color(1f,.75f,.3f),new Vector3(0,0,0));
  // Service dock on the rear console top: sealed until the passenger seat interlock releases it.
  dockRoot=Group("Service dock",cabin,new Vector3(-.085f,.725f,.015f));
  Box("Dock well",dockRoot,Vector3.zero,new Vector3(.115f,.006f,.075f),screen);dockCard=Box("Maintenance card (inserted)",dockRoot,new Vector3(0,.004f,0),new Vector3(.085f,.003f,.054f),paper);dockCard.gameObject.SetActive(false);
  dockCover=Box("Dock cover",dockRoot,new Vector3(0,.007f,0),new Vector3(.12f,.01f,.08f),metal);
  dockLed=Box("Dock LED",dockRoot,new Vector3(.045f,.008f,.045f),new Vector3(.01f,.004f,.01f),ledRed,PrimitiveType.Sphere).GetComponent<Renderer>();
  dockLabel=Text("dock",dockRoot,new Vector3(0,.0075f,-.05f),"SERVICE DOCK",.0085f,new Color(.75f,.8f,.8f),new Vector3(90,0,0));
  dock=Make("service-dock","Service dock",dockRoot,new Vector3(0,.03f,0),new Vector3(.15f,.06f,.11f),ControlAction.Display,null,"");
  // Phone on the console top, tethered by a thin cable to the service-adjacent charge port.
  phoneBody=Group("Previous passenger's phone",cabin,new Vector3(.1f,.728f,.02f));phoneBody.localRotation=Quaternion.Euler(0,-12,0);
  var shell=Box("Phone body",phoneBody,Vector3.zero,new Vector3(.072f,.009f,.145f),dark);Box("Phone glass",phoneBody,new Vector3(0,.0052f,0),new Vector3(.064f,.002f,.13f),screen);
  phoneGlow=Box("Phone glow",phoneBody,new Vector3(0,.0066f,.056f),new Vector3(.03f,.001f,.004f),Mats.Glow("Phone glow",new Color(.25f,.9f,.95f),.1f)).GetComponent<Renderer>();
  phoneText=Text("phone",phoneBody,new Vector3(0,.0075f,0),"",.0085f,new Color(.7f,1f,.95f),new Vector3(90,0,0));
  phoneScreen=Make("phone-screen","Phone",phoneBody,new Vector3(0,.03f,0),new Vector3(.09f,.06f,.16f),ControlAction.Display,null,"");
  phoneCable=Group("Phone cable",cabin,Vector3.zero);
  Box("Cable top",phoneCable,new Vector3(.0f,.7235f,-.085f),new Vector3(.006f,.004f,.12f),dark);
  Box("Cable drop",phoneCable,new Vector3(-.075f,.47f,-.162f),new Vector3(.006f,.5f,.006f),dark);
  phoneBody.gameObject.SetActive(false);phoneCable.gameObject.SetActive(false);
 }
 // ---- GLOVE BOX CONTENTS: a USB stick, the previous passenger's phone and a maintenance card --------------------------------------------
 void BuildGloveItems(){
  itemsRoot=Group("Glove box contents",cabin,Vector3.zero);
  // route card already exists (VehicleSecurity.storageClue); these sit on either side of it.
  var usb=Group("USB stick",itemsRoot,new Vector3(.37f,.615f,1.66f));Box("Body",usb,Vector3.zero,new Vector3(.032f,.012f,.075f),dark);Box("Connector",usb,new Vector3(0,0,.052f),new Vector3(.02f,.008f,.03f),metal);Box("Spanner mark",usb,new Vector3(0,.0065f,-.01f),new Vector3(.012f,.001f,.012f),Mats.Glow("Spanner",new Color(1f,.7f,.2f),.8f));
  usb.localRotation=Quaternion.Euler(0,-24,0);items["usb-device"]=usb;itemControls["usb-device"]=Make("usb-device","USB stick · take",usb,new Vector3(0,.04f,.02f),new Vector3(.14f,.1f,.2f),ControlAction.Pocket,null,"");
  var phone=Group("Phone (glove box)",itemsRoot,new Vector3(.86f,.612f,1.67f));Box("Body",phone,Vector3.zero,new Vector3(.07f,.009f,.14f),dark);Box("Glass",phone,new Vector3(0,.0052f,0),new Vector3(.062f,.002f,.126f),screen);phone.localRotation=Quaternion.Euler(0,16,0);
  items["phone-device"]=phone;itemControls["phone-device"]=Make("phone-device","Phone · take",phone,new Vector3(0,.04f,0),new Vector3(.14f,.1f,.2f),ControlAction.Pocket,null,"");
  var card=Group("Maintenance card",itemsRoot,new Vector3(.62f,.61f,1.78f));Box("Card",card,Vector3.zero,new Vector3(.085f,.003f,.054f),paper);Box("Strip",card,new Vector3(0,.0018f,.018f),new Vector3(.08f,.001f,.01f),Mats.Glow("Card strip",new Color(.2f,.6f,.7f),.5f));Text("card",card,new Vector3(0,.0025f,-.004f),"SERVICE",.007f,new Color(.12f,.18f,.2f),new Vector3(90,0,0));
  items["maint-card"]=card;itemControls["maint-card"]=Make("maint-card","Maintenance card · take",card,new Vector3(0,.04f,0),new Vector3(.16f,.1f,.14f),ControlAction.Pocket,null,"");
  itemsRoot.gameObject.SetActive(false);
 }
 // ---- GLASS TINT + SUN VISORS ---------------------------------------------------------------------------------------------------------------
 void BuildGlassAndVisors(){
  foreach(var t in cabin.GetComponentsInChildren<Transform>(true)){
   if(t.name=="Side window • clear glass"||t.name=="Laminated windshield"){
    var mf=t.GetComponent<MeshFilter>();if(mf==null)continue;
    var o=new GameObject("Privacy tint").transform;o.SetParent(t,false);o.localPosition=Vector3.zero;o.localRotation=Quaternion.identity;o.localScale=Vector3.one*1.002f;
    var f=o.gameObject.AddComponent<MeshFilter>();f.sharedMesh=mf.sharedMesh;var r=o.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=new Material(tintMat);r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    r.sharedMaterial.color=new Color(0,0,0,0);r.enabled=false;tint.Add(r);
    if(t.name=="Laminated windshield")windshieldTint=r;
   }
   if(t.name=="Sun visor"){visors.Add(t);visorHome.Add(t.localPosition);visorRot.Add(t.localRotation);}
  }
  // A dedicated tint button beside each window switch: the car can take it over, the passenger can win it back.
  foreach(int side in new[]{-1,1}){
   var door=side<0?Find("Left rear door"):Find("Right rear door");
   var parent=door!=null?door:cabin;float x=side*1.13f;
   var b=Box("Tint button",parent,new Vector3(x-side*.12f,.585f,-.40f),new Vector3(.07f,.025f,.07f),dark);Box("Tint rim",b,new Vector3(0,.55f,0),new Vector3(1.06f,.12f,1.06f),metal);
   Box("Tint LED",b,new Vector3(0,.7f,0),new Vector3(.25f,.1f,.25f),led,PrimitiveType.Sphere);
   var c=Make(side<0?"left-tint":"right-tint","Privacy glass",parent,new Vector3(x-side*.13f,.59f,-.40f),new Vector3(.12f,.1f,.12f),ControlAction.Switch,b,"",new Vector3(0,-.006f,0));
   if(side<0)tintLeft=c;else tintRight=c;
  }
 }
 public Renderer windshieldTint;
 // ---- MILE MARKERS (the window puzzle: read one through clear glass) ---------------------------------------------------------------------------
 void BuildMarkers(){
  var env=game.Motion.environment;if(env==null)return;
  var greenMat=Mats.Find("Roadsign green")??M("green",new Color(.045f,.24f,.17f));
  for(int i=0;i<3;i++){
   var m=Group("Mile marker "+i,env,new Vector3(7.9f,0,40+i*70));
   Box("Post",m,new Vector3(0,1.1f,0),new Vector3(.14f,2.3f,.14f),metal);
   Box("Board",m,new Vector3(0,2.4f,0),new Vector3(2.3f,1.45f,.1f),greenMat);Box("Border",m,new Vector3(0,2.4f,-.052f),new Vector3(2.18f,1.33f,.01f),paper);Box("Inner",m,new Vector3(0,2.4f,-.058f),new Vector3(2.1f,1.25f,.01f),greenMat);
   markerText[i]=Text("marker",m,new Vector3(0,2.4f,-.075f),"A55\nMILE 0.0",.4f,Color.white,Vector3.zero);
   markers[i]=m;
  }
 }
 // ---- CABIN TEXT: warnings and the destination banner ---------------------------------------------------------------------------------------------
 void BuildCabinText(){
  var nav=game.Ride.navigation;
  if(nav!=null){warningText=Text("warning",nav.transform.parent,nav.transform.localPosition+new Vector3(0,-.1f,-.004f),"",.016f,new Color(1f,.35f,.22f),nav.transform.localEulerAngles);}
 }
 // ----------------------------------------------------------------------------------------------------------------------------------------------------------
}
}
