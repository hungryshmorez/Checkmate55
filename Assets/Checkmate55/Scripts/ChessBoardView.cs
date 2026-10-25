using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
public sealed class ChessPieceHit : MonoBehaviour {public PhysicalControl target;}
// A real articulated tray; every square is a generous physical ray target.
// OVERRIDE adds: Cyber / card wings, hazard markers, firewall domes and EMP indicators - all part of the same physical tray.
public sealed class ChessBoardView : MonoBehaviour {
 public Transform tray,hinge,rail;public TextMesh status;
 public readonly Dictionary<string,Transform> Pieces=new Dictionary<string,Transform>();
 public readonly Dictionary<string,Renderer> Squares=new Dictionary<string,Renderer>();
 public readonly Dictionary<string,PhysicalControl> Controls=new Dictionary<string,PhysicalControl>();
 public readonly Dictionary<string,PhysicalControl> Tools=new Dictionary<string,PhysicalControl>();
 public Material ivory,obsidian,lightSquare,darkSquare,selected,legal,metal;
 bool deployed;float lastSeat;Transform roadPanel;TextMesh roadStatus;Renderer[] laneRails;
 // OVERRIDE
 Transform wings,cyberWing,cardWing;TextMesh chargeText,handText;TextMesh[] cardText=new TextMesh[3],toolText=new TextMesh[3];Renderer[] cardPlate=new Renderer[3],chargePip=new Renderer[6],toolPlate=new Renderer[3];
 Material hazardWarn,hazardActive,shieldMat,empMat,pending,cardMat,cardEmpty,pipOn,pipOff,toolMat,toolHot;
 readonly Dictionary<string,Transform> hazardMarks=new Dictionary<string,Transform>(),shieldMarks=new Dictionary<string,Transform>(),empMarks=new Dictionary<string,Transform>();
 readonly Dictionary<string,float> flashes=new Dictionary<string,float>();
 Renderer[] flashSquares;
 public bool ToolsVisible{get;private set;}
 void LateUpdate(){if(deployed&&tray.gameObject.activeSelf)Pose(1,lastSeat);Animate();}
 const float Cell=.065f;
 public static Vector3 Position(string square)=>new Vector3((square[0]-'a'-3.5f)*Cell,.025f,(square[1]-'1'-3.5f)*Cell);
 public static Transform Group(string n,Transform p,Vector3 pos){var t=new GameObject(n).transform;t.SetParent(p,false);t.localPosition=pos;return t;}
 public static Transform Shape(string n,Transform p,Vector3 pos,Vector3 scale,Material m,PrimitiveType type=PrimitiveType.Cube){var t=GameObject.CreatePrimitive(type).transform;t.name=n;t.SetParent(p,false);t.localPosition=pos;t.localScale=scale;t.GetComponent<Renderer>().sharedMaterial=m;Object.Destroy(t.GetComponent<Collider>());return t;}
 public static Material Material(string n,Color c,float gloss=.45f,bool glow=false){var m=new Material(Shader.Find("Standard")){name=n,color=c};m.SetFloat("_Glossiness",gloss);if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*.75f);}return m;}
 public static TextMesh Text(string n,Transform parent,Vector3 pos,string text,float size,Color color){var t=Group(n,parent,pos);t.localRotation=Quaternion.Euler(90,0,0);var tm=t.gameObject.AddComponent<TextMesh>();tm.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");tm.GetComponent<Renderer>().sharedMaterial=tm.font.material;WorldTextDepth.Apply(tm);tm.fontSize=64;tm.characterSize=size*.15625f;tm.text=text;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=color;return tm;}
 static PhysicalControl Hit(Transform t,string id,string label,Vector3 size,SceneManager game){t.gameObject.layer=8;var c=t.gameObject.AddComponent<PhysicalControl>();c.id=id;c.label=label;c.action=ControlAction.Display;c.movingPart=t;var col=t.gameObject.AddComponent<BoxCollider>();col.isTrigger=true;col.size=size;c.mobileHitProxy=col;game.Registry.Register(c);return c;}
 public void Build(SceneManager game){
  ivory=Material("Porcelain • white",new Color(.94f,.9f,.74f));obsidian=Material("Obsidian • black",new Color(.045f,.075f,.09f));metal=Material("Brushed champagne",new Color(.49f,.42f,.29f),.7f);
  lightSquare=Material("Ivory squares",new Color(.73f,.78f,.72f));darkSquare=Material("Midnight squares",new Color(.075f,.15f,.17f));selected=Material("Selected • amber",new Color(1,.62f,.12f),.5f,true);legal=Material("Legal move • mint",new Color(.15f,.94f,.68f),.5f,true);
  hazardWarn=Material("Hazard WARNING • amber",new Color(1,.55f,.05f),.2f,true);hazardActive=Material("Hazard ACTIVE • red",new Color(1,.1f,.04f),.2f,true);
  shieldMat=Material("Firewall • cyan",new Color(.2f,.85f,1f,.42f),.6f,true);empMat=Material("EMP • violet",new Color(.62f,.3f,1f),.3f,true);pending=Material("Pending tool • white",new Color(1,1,1),.4f,true);
  cardMat=Material("Tactical card",new Color(.9f,.86f,.7f),.5f);cardEmpty=Material("Empty card slot",new Color(.06f,.1f,.12f),.2f);pipOn=Material("Charge pip on",new Color(.2f,1f,.85f),.3f,true);pipOff=Material("Charge pip off",new Color(.05f,.1f,.11f),.2f);toolMat=Material("Cyber button",new Color(.07f,.13f,.15f),.5f);toolHot=Material("Cyber button ready",new Color(.1f,.35f,.4f),.5f,true);
  hinge=Group("Console • opening lid",transform,Vector3.zero);Shape("Hinged security lid",hinge,new Vector3(0,0,.13f),new Vector3(.3f,.025f,.27f),obsidian);
  rail=Group("Telescopic console support",transform,Vector3.zero);Shape("Mechanical rail",rail,Vector3.zero,new Vector3(.045f,.035f,1),metal);
  tray=Group("Lap security tray",transform,Vector3.zero);Shape("Chamfered metal surround",tray,Vector3.zero,new Vector3(.587f,.027f,.82f),metal);Shape("Inset dark surround",tray,new Vector3(0,.015f,0),new Vector3(.572f,.009f,.805f),obsidian);
  for(int y=0;y<8;y++)for(int x=0;x<8;x++){
   string s=""+(char)('a'+x)+(char)('1'+y);Vector3 pos=Position(s);
   var tile=Shape("Square "+s,tray,pos,new Vector3(Cell-.0015f,.008f,Cell-.0015f),(x+y)%2==0?darkSquare:lightSquare);Squares[s]=tile.GetComponent<Renderer>();
   var t=Group("Chess target "+s,tray,pos+Vector3.up*.012f);t.gameObject.layer=8;var c=t.gameObject.AddComponent<PhysicalControl>();c.id="chess-"+s;c.label=s;c.action=ControlAction.Display;c.movingPart=t;var col=t.gameObject.AddComponent<BoxCollider>();col.isTrigger=true;col.size=new Vector3(Cell,.018f,Cell);c.mobileHitProxy=col;Controls[s]=c;game.Registry.Register(c);
  }
  for(int i=0;i<8;i++){Text("File",tray,new Vector3((i-3.5f)*Cell,.025f,-.278f),""+(char)('a'+i),.018f,Color.white);Text("Rank",tray,new Vector3(-.278f,.025f,(i-3.5f)*Cell),""+(i+1),.018f,Color.white);}
  status=Text("Diegetic chess objective",tray,new Vector3(0,.026f,.293f),"AUTHENTICATION  /  WHITE TO MOVE",.018f,new Color(.48f,1,.84f));
  laneRails=new Renderer[8];for(int x=0;x<8;x++)laneRails[x]=Shape("Lane activity rail",tray,new Vector3((x-3.5f)*Cell,.03f,-.293f),new Vector3(Cell-.004f,.004f,.006f),metal).GetComponent<Renderer>();
  roadPanel=Group("Live vehicle bus",tray,Vector3.zero);
  roadStatus=Text("Road rules live",roadPanel,new Vector3(0,.027f,.355f),"",.017f,new Color(1,.73f,.3f));
  for(int i=0;i<2;i++){var t=Group("Tray vehicle command",roadPanel,new Vector3(i==0?-.139f:.139f,.035f,-.352f));Shape("Touch bezel",t,Vector3.zero,new Vector3(.254f,.009f,.068f),darkSquare);Text("Command label",t,Vector3.up*.007f,i==0?"LANE  /  A–C · D–E · F–H":"SPEED  /  75 · 95 · 115",.017f,Color.white);var c=Hit(t,i==0?"security-lane":"security-speed",i==0?"Request lane · changes active files":"Request cruise speed · changes rules",new Vector3(.26f,.09f,.07f),game);}
  BuildOverride(game);
  SetVisible(false);
 }
 void BuildOverride(SceneManager game){
  // Far-edge rows keep every tool on screen in both landscape and portrait: Cyber buttons, then the tactical-card hand.
  flashSquares=new Renderer[64];
  wings=Group("Cyber and card rows",tray,Vector3.zero);
  cyberWing=Group("Cyber row",wings,new Vector3(0,0,.46f));cardWing=Group("Card row",wings,new Vector3(0,0,.58f));
  Shape("Tray far extension",wings,new Vector3(0,0,.535f),new Vector3(.587f,.027f,.25f),metal);Shape("Tray far inset",wings,new Vector3(0,.0145f,.535f),new Vector3(.572f,.009f,.236f),obsidian);
  string[] names={"FIREWALL","EMP SHOCK","OVERDRIVE"},tools={"firewall","emp","overdrive"},costs={"1","2","3"};
  for(int i=0;i<3;i++){float x=(i-1)*.195f;var b=Group("Cyber button "+tools[i],cyberWing,new Vector3(x,.02f,0));toolPlate[i]=Shape("Button plate",b,Vector3.zero,new Vector3(.18f,.012f,.085f),toolMat).GetComponent<Renderer>();
   toolText[i]=Text("Button label",b,new Vector3(0,.008f,.014f),names[i],.0125f,Color.white);Text("Button cost",b,new Vector3(0,.008f,-.02f),"COST "+costs[i],.0095f,new Color(.55f,.9f,.85f));
   Tools[tools[i]]=Hit(b,"chess-tool-"+tools[i],names[i],new Vector3(.19f,.06f,.095f),game);}
  chargeText=Text("Charge readout",wings,new Vector3(-.2f,.022f,.4f),"CHARGE 0",.014f,new Color(.55f,1f,.9f));
  for(int i=0;i<6;i++)chargePip[i]=Shape("Charge pip",wings,new Vector3(-.03f+i*.034f,.022f,.4f),new Vector3(.022f,.008f,.022f),pipOff).GetComponent<Renderer>();
  for(int i=0;i<3;i++){float x=(i-1)*.195f;var c=Group("Card slot "+i,cardWing,new Vector3(x,.02f,0));cardPlate[i]=Shape("Card",c,Vector3.zero,new Vector3(.18f,.012f,.1f),cardEmpty).GetComponent<Renderer>();
   cardText[i]=Text("Card name",c,new Vector3(0,.008f,.016f),"",.0115f,new Color(.12f,.1f,.05f));Text("Card hint",c,new Vector3(0,.008f,-.024f),"",.0085f,new Color(.2f,.16f,.08f));
   Tools["card"+i]=Hit(c,"chess-tool-card"+i,"Tactical card "+(i+1),new Vector3(.19f,.06f,.11f),game);}
  handText=Text("Hand readout",wings,new Vector3(0,.022f,.515f),"HAND 0",.014f,new Color(1f,.82f,.45f));
  wings.gameObject.SetActive(false);
 }
 // The far rows only exist on trays whose challenge uses Cyber Chess or tactical cards.
 public void ShowTools(bool cyber,bool cards){ToolsVisible=cyber||cards;wings.gameObject.SetActive(cyber||cards);cyberWing.gameObject.SetActive(cyber);cardWing.gameObject.SetActive(cards);chargeText.gameObject.SetActive(cyber);foreach(var p in chargePip)p.gameObject.SetActive(cyber);handText.gameObject.SetActive(cards);}
 public void ShowRoad(RoadChessRules rules,bool show){roadPanel.gameObject.SetActive(show);for(int i=0;i<8;i++)laneRails[i].sharedMaterial=show&&RoadChessRules.Lane(""+(char)('a'+i)+"1")==rules.ActiveRoadLane?selected:metal;if(show)roadStatus.text=rules.Readout;}
 public void SetVisible(bool show){tray.gameObject.SetActive(show);rail.gameObject.SetActive(show);hinge.gameObject.SetActive(show);if(!show)ClearMarks();}
 public void Pose(float amount,float seatX){
  deployed=amount>=1;lastSeat=seatX;
  var dock=new Vector3(.62f,.73f,-1.14f);var lap=new Vector3(seatX,.81f,-.67f);float t=Mathf.SmoothStep(0,1,amount);
  transform.localPosition=Vector3.zero;tray.localPosition=Vector3.Lerp(dock,lap,t);tray.localRotation=Quaternion.Euler(Mathf.Lerp(85,Screen.width<Screen.height?-20:-10,t),0,0);tray.localScale=Vector3.one*Mathf.Lerp(.28f,Screen.width<Screen.height?1:.82f,t);
  hinge.localPosition=dock;hinge.localRotation=Quaternion.Euler(-105*t,0,0);
  var end=tray.localPosition-Vector3.up*.035f;rail.localPosition=(dock+end)/2;rail.localRotation=Quaternion.LookRotation(end-dock+Vector3.forward*.001f);rail.localScale=new Vector3(1,1,Mathf.Max(.05f,Vector3.Distance(dock,end)));
 }
 public void ShowPosition(ChessSnapshot state){foreach(var p in Pieces.Values){foreach(var filter in p.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh.name=="Chess lathe")Destroy(filter.sharedMesh);Destroy(p.gameObject);}Pieces.Clear();foreach(var p in state.pieces){Pieces[p.square]=MakePiece(p.type,p.color,tray,Position(p.square)+Vector3.up*.006f);var hit=Pieces[p.square].gameObject.AddComponent<ChessPieceHit>();hit.target=Controls[p.square];hit.gameObject.layer=8;var collider=hit.gameObject.AddComponent<CapsuleCollider>();collider.isTrigger=true;collider.radius=.025f;collider.height=.10f;collider.center=Vector3.up*.042f;Controls[p.square].label=(p.color=="w"?"White ":"Black ")+Name(p.type)+" · "+p.square;}foreach(var s in Controls.Keys)if(!Pieces.ContainsKey(s))Controls[s].label=s;Highlight(null,null);ShowMarks(state);}
 public static string Name(string p)=>p=="p"?"pawn":p=="n"?"knight":p=="b"?"bishop":p=="r"?"rook":p=="q"?"queen":"king";
 public Transform MakePiece(string type,string color,Transform parent,Vector3 pos){
  var root=Group(color+" "+Name(type),parent,pos);var mat=color=="w"?ivory:obsidian;
  float h=type=="p"?.054f:type=="r"?.066f:.086f;
  // Lathed base, collar and tapered stem create a recognizable Staunton silhouette.
  var profile=new[]{new Vector2(.023f,0),new Vector2(.025f,.005f),new Vector2(.024f,.009f),new Vector2(.018f,.013f),new Vector2(.016f,.017f),new Vector2(.009f,h*.57f),new Vector2(.016f,h*.66f),new Vector2(.016f,h*.71f),new Vector2(.01f,h*.74f)};
  int sides=20;var vertices=new List<Vector3>();var triangles=new List<int>();for(int j=0;j<profile.Length;j++)for(int i=0;i<=sides;i++){float a=i*2*Mathf.PI/sides;vertices.Add(new Vector3(Mathf.Cos(a)*profile[j].x,profile[j].y,Mathf.Sin(a)*profile[j].x));if(j>0&&i<sides){int k=j*(sides+1)+i;triangles.AddRange(new[]{k,k+1,k-sides-1,k+1,k-sides,k-sides-1});}}
  var mesh=new Mesh{name="Chess lathe"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();var stem=Group("Turned body",root,Vector3.zero);stem.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;stem.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;
  Shape("Base inlay",root,new Vector3(0,.011f,0),new Vector3(.041f,.0015f,.041f),metal,PrimitiveType.Cylinder);
  if(type=="r"){Shape("Rook tower",root,new Vector3(0,h*.85f,0),new Vector3(.034f,.012f,.034f),mat,PrimitiveType.Cylinder);for(int i=0;i<4;i++){float a=i*Mathf.PI/2;Shape("Battlement",root,new Vector3(Mathf.Sin(a)*.012f,h+.002f,Mathf.Cos(a)*.012f),new Vector3(.012f,.013f,.012f),mat);}}
  else if(type=="n"){var neck=Shape("Horse neck",root,new Vector3(0,h*.85f,0),new Vector3(.022f,.037f,.021f),mat);neck.localRotation=Quaternion.Euler(0,0,-20);Shape("Horse muzzle",root,new Vector3(.01f,h+.004f,0),new Vector3(.032f,.016f,.021f),mat);Shape("Horse ear",root,new Vector3(-.007f,h+.02f,0),new Vector3(.008f,.016f,.015f),mat);}
  else {Shape("Crown",root,new Vector3(0,h*.89f,0),new Vector3(.029f,type=="b"?.037f:.025f,.029f),mat,PrimitiveType.Sphere);
   if(type=="k"){Shape("Cross upright",root,new Vector3(0,h+.013f,0),new Vector3(.007f,.027f,.007f),mat);Shape("Cross arms",root,new Vector3(0,h+.017f,0),new Vector3(.023f,.007f,.007f),mat);}
   if(type=="q")for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;Shape("Crown point",root,new Vector3(Mathf.Sin(a)*.013f,h+.007f,Mathf.Cos(a)*.013f),Vector3.one*.009f,mat,PrimitiveType.Sphere);}
   if(type=="b"){var slit=Shape("Bishop mitre slit",root,new Vector3(0,h*.96f,-.013f),new Vector3(.003f,.022f,.002f),metal);slit.localRotation=Quaternion.Euler(0,0,-30);}
  }return root;
 }
 public void Highlight(string selectedSquare,IEnumerable<string> targets){foreach(var pair in Squares){int x=pair.Key[0]-'a',y=pair.Key[1]-'1';pair.Value.sharedMaterial=(x+y)%2==0?darkSquare:lightSquare;}foreach(var p in Pieces){var ring=p.Value.Find("Selection halo");if(ring)Destroy(ring.gameObject);}if(selectedSquare!=null){Squares[selectedSquare].sharedMaterial=selected;if(Pieces.TryGetValue(selectedSquare,out var piece))Shape("Selection halo",piece,new Vector3(0,.002f,0),new Vector3(.057f,.002f,.057f),selected,PrimitiveType.Cylinder);}if(targets!=null)foreach(var s in targets)Squares[s].sharedMaterial=legal;}
 // Squares reachable only by a second Overdrive step are drawn in amber, distinct from ordinary mint targets.
 public void HighlightSecond(IEnumerable<string> targets){if(targets!=null)foreach(var s in targets)Squares[s].sharedMaterial=selected;}
 // ------------------------------------------------------------------ OVERRIDE: live road / cyber state on the physical tray
 void ClearMarks(){foreach(var m in hazardMarks.Values)if(m)Destroy(m.gameObject);foreach(var m in shieldMarks.Values)if(m)Destroy(m.gameObject);foreach(var m in empMarks.Values)if(m)Destroy(m.gameObject);hazardMarks.Clear();shieldMarks.Clear();empMarks.Clear();}
 public void ShowMarks(ChessSnapshot snap){
  ClearMarks();if(snap==null||snap.pieces==null)return;
  if(snap.state!=null&&snap.state.hazards!=null)foreach(var h in snap.state.hazards){var m=Shape("Hazard "+h.sq,tray,Position(h.sq)+Vector3.up*.006f,new Vector3(Cell-.006f,.004f,Cell-.006f),h.phase=="ACTIVE"?hazardActive:hazardWarn);hazardMarks[h.sq]=m;}
  foreach(var p in snap.pieces){
   if(p.shield&&Pieces.TryGetValue(p.square,out var piece)){var d=Shape("Firewall dome",piece,new Vector3(0,.04f,0),new Vector3(.062f,.095f,.062f),shieldMat,PrimitiveType.Sphere);shieldMarks[p.square]=d;}
   if(p.emp&&Pieces.TryGetValue(p.square,out var pe)){var e=Shape("EMP static",pe,new Vector3(0,.098f,0),new Vector3(.03f,.006f,.03f),empMat,PrimitiveType.Cylinder);empMarks[p.square]=e;}
  }
 }
 public void Flash(string square,Color color){if(!Squares.ContainsKey(square))return;flashes[square]=.7f;Squares[square].sharedMaterial=pending;}
 void Animate(){
  float t=Time.time;
  foreach(var pair in hazardMarks){if(!pair.Value)continue;var r=pair.Value.GetComponent<Renderer>();float pulse=.55f+.45f*Mathf.Abs(Mathf.Sin(t*7));r.sharedMaterial.SetColor("_EmissionColor",(r.sharedMaterial==hazardActive?new Color(1,.1f,.04f):new Color(1,.55f,.05f))*pulse*1.4f);pair.Value.localScale=new Vector3(Cell-.006f,.004f+.002f*Mathf.Sin(t*17),Cell-.006f);}
  foreach(var pair in empMarks)if(pair.Value)pair.Value.localRotation=Quaternion.Euler(0,t*220,0);
  if(flashes.Count>0){var done=new List<string>();foreach(var k in new List<string>(flashes.Keys)){flashes[k]-=Time.deltaTime;if(flashes[k]<=0){done.Add(k);int x=k[0]-'a',y=k[1]-'1';Squares[k].sharedMaterial=(x+y)%2==0?darkSquare:lightSquare;}}foreach(var k in done)flashes.Remove(k);}
 }
 // Cyber charges, cooldowns and the card hand, mirrored from the engine's state.
 public void ShowResources(ChessSnapshot snap,string pendingTool){
  if(!ToolsVisible||snap==null||snap.state==null)return;
  var me=snap.state.w;if(me==null)return;
  chargeText.text="CHARGE "+me.charges;for(int i=0;i<6;i++)chargePip[i].sharedMaterial=i<me.charges?pipOn:pipOff;
  int[] cost={1,2,3};int[] cd={me.cd!=null?me.cd.firewall:0,me.cd!=null?me.cd.emp:0,me.cd!=null?me.cd.overdrive:0};
  for(int i=0;i<3;i++){bool ready=me.charges>=cost[i]&&cd[i]==0&&!me.cyberUsed&&snap.state.side=="w"&&!snap.over;toolPlate[i].sharedMaterial=pendingTool==new[]{"firewall","emp","overdrive"}[i]?pending:ready?toolHot:toolMat;toolText[i].color=ready?Color.white:new Color(.45f,.55f,.56f);}
  int n=me.hand==null?0:me.hand.Length;handText.text="HAND "+n+(me.cardPlayed?"  ·  PLAYED":"");
  for(int i=0;i<3;i++){bool has=i<n;cardPlate[i].sharedMaterial=has&&!me.cardPlayed?cardMat:cardEmpty;cardText[i].text=has?CardName(me.hand[i]):"";cardText[i].color=has&&!me.cardPlayed?new Color(.12f,.1f,.05f):new Color(.4f,.45f,.45f);var hint=cardText[i].transform.parent.Find("Card hint");if(hint)hint.GetComponent<TextMesh>().text=has?CardHint(me.hand[i]):"";}
 }
 public static string CardName(string id)=>id=="pawnBoost"?"PAWN BOOST":id=="glitchSwap"?"GLITCH SWAP":id=="systemReboot"?"SYSTEM REBOOT":id=="powerSurge"?"POWER SURGE":id;
 public static string CardHint(string id)=>id=="pawnBoost"?"pawn +2 forward":id=="glitchSwap"?"swap 2 pieces":id=="systemReboot"?"clear hazards":id=="powerSurge"?"+2 charge":"";
}
}
