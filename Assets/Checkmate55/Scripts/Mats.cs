using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
// Runtime material helper. Clones materials that already ship in the scene (so the Standard-shader variants they need are
// guaranteed to survive WebGL shader stripping) and tints them, instead of creating blank materials with new keywords.
public static class Mats {
 static readonly Dictionary<string,Material> cache=new Dictionary<string,Material>();
 static Material[] loaded;
 public static Material Find(string name){
  if(cache.TryGetValue(name,out var m)&&m)return m;
  if(loaded==null||loaded.Length==0)loaded=Resources.FindObjectsOfTypeAll<Material>();
  foreach(var c in loaded)if(c&&c.name==name){cache[name]=c;return c;}
  return null;
 }
 static Material Base(string name){var b=Find(name);if(b)return new Material(b);var f=new Material(Shader.Find("Standard"));return f;}
 public static Material Solid(string name,Color c,float gloss=.4f,float metallic=0){var m=Base("Graphite soft touch");m.name=name;m.color=c;m.SetFloat("_Glossiness",gloss);m.SetFloat("_Metallic",metallic);return m;}
 public static Material Glow(string name,Color c,float intensity=.8f){var m=Base("Ice blue accent");m.name=name;m.color=c;m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*intensity);return m;}
 public static Material Glass(string name,Color c){var m=Base("Clear laminated glazing");m.name=name;m.color=c;return m;}
 public static Material Metal(string name,Color c){var m=Base("Satin aluminium");m.name=name;m.color=c;return m;}
 public static void SetGlow(Material m,Color c,float intensity){if(!m)return;m.color=c;m.SetColor("_EmissionColor",c*intensity);}
 public static void Reset(){cache.Clear();loaded=null;}
}
}
