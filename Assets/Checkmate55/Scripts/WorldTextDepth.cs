using UnityEngine;
namespace Checkmate55 {
public sealed class WorldTextDepth : MonoBehaviour {
 public Shader shader;Material material;Font font;public static WorldTextDepth Current{get;private set;}
 public Material TextMaterial=>material;
 // Text created after startup must use the depth-tested material too, or it would draw through the dashboard.
 public static void Apply(TextMesh text){if(Current!=null&&Current.material&&text){text.GetComponent<Renderer>().sharedMaterial=Current.material;}}
 public void Initialize(){Current=this;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");material=new Material(shader);Refresh(font);foreach(var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))text.GetComponent<Renderer>().sharedMaterial=material;Font.textureRebuilt+=Refresh;}
 void Refresh(Font f){if(f==font&&material)material.mainTexture=font.material.mainTexture;}
 void OnDestroy(){if(Current==this)Current=null;Font.textureRebuilt-=Refresh;if(material)Destroy(material);}
}
}
