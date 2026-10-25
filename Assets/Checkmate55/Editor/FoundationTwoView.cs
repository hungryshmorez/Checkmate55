using System;
using System.Reflection;
using UnityEditor;
public static class FoundationTwoView {
 // Transient Editor view size; deliberately never calls SaveToHDD.
 public static void Size(int width,int height){
  var a=typeof(Editor).Assembly;var t=a.GetType("UnityEditor.GameViewSizes");var inst=t.BaseType.GetProperty("instance").GetValue(null);var grp=t.GetProperty("currentGroup",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(inst);
  var size=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Foundation 02 verification"});
  grp.GetType().GetMethod("AddCustomSize").Invoke(grp,new[]{size});int index=(int)grp.GetType().GetMethod("GetTotalCount").Invoke(grp,null)-1;
  var view=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,index);view.Focus();
 }
}
