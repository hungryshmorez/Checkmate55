using System.Collections.Generic;
using UnityEngine;
namespace Checkmate55 {
public sealed class ObjectRegistry : MonoBehaviour {
 readonly Dictionary<string,PhysicalControl> objects = new Dictionary<string,PhysicalControl>();
 public IEnumerable<PhysicalControl> All => objects.Values;
 public int Count => objects.Count;
 public void Register(PhysicalControl obj) { if(objects.ContainsKey(obj.id)) Debug.LogError("Duplicate control: "+obj.id); else objects.Add(obj.id,obj); }
 public PhysicalControl Get(string id) => objects.TryGetValue(id,out var v)?v:null;
 public void Initialize() { objects.Clear(); foreach(var obj in FindObjectsByType<PhysicalControl>(FindObjectsSortMode.None)) Register(obj); }
}
}
