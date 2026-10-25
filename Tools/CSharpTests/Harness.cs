using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace Checkmate55.Tests {
[AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute:Attribute{}
[AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute:Attribute{}
public static class Assert {
 public static void True(bool c,string msg="assertion failed"){if(!c)throw new Exception(msg);}
 public static void False(bool c,string msg="expected false"){if(c)throw new Exception(msg);}
 public static void Eq<T>(T expected,T actual,string msg=""){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception((msg.Length>0?msg+": ":"")+"expected "+expected+" but was "+actual);}
 public static void InRange(float v,float lo,float hi,string msg=""){if(v<lo||v>hi)throw new Exception((msg.Length>0?msg+": ":"")+v+" not in ["+lo+","+hi+"]");}
 public static void Throws(Action a,string msg="expected an exception"){try{a();}catch(Exception){return;}throw new Exception(msg);}
}
public static class Program {
 public static int Main(string[] args){
  int pass=0,fail=0;string filter=args.Length>0?args[0]:"";
  foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.GetCustomAttributes(typeof(TestFixtureAttribute),false).Length>0).OrderBy(t=>t.Name)){
   foreach(var m in type.GetMethods().Where(m=>m.GetCustomAttributes(typeof(TestAttribute),false).Length>0)){
    if(filter.Length>0&&!(type.Name+"."+m.Name).Contains(filter))continue;
    try{var inst=Activator.CreateInstance(type);m.Invoke(inst,null);pass++;Console.WriteLine("ok   "+type.Name+"."+m.Name);}
    catch(TargetInvocationException e){fail++;Console.WriteLine("FAIL "+type.Name+"."+m.Name+": "+e.InnerException.Message);}
   }
  }
  Console.WriteLine();Console.WriteLine("passed "+pass+", failed "+fail);
  return fail==0?0:1;
 }
}
}
