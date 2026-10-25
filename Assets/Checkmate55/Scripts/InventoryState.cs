using System;
using System.Collections.Generic;
namespace Checkmate55 {
public sealed class InventoryState {
 public readonly HashSet<string> Items=new HashSet<string>();
 public readonly HashSet<string> Discovered=new HashSet<string>();
 public event Action<string> Acquired;
 public bool Contains(string id)=>Items.Contains(id);
 public void Discover(string id){Discovered.Add(id);}
 public void Acquire(string id){Discover(id);if(Items.Add(id))Acquired?.Invoke(id);}
 public bool Remove(string id)=>Items.Remove(id);
}
}
