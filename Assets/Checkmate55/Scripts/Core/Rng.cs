using System;
using System.Collections.Generic;
namespace Checkmate55.Core {
// Small deterministic generator (xorshift32). Core systems never use UnityEngine.Random so runs are reproducible and testable.
public sealed class Rng {
 uint s;
 public Rng(int seed){s=(uint)(seed*747796405+2891336453u);if(s==0)s=0x9E3779B9;for(int i=0;i<4;i++)Next();}
 public uint NextUInt(){s^=s<<13;s^=s>>17;s^=s<<5;return s;}
 public float Next()=>(NextUInt()&0xFFFFFF)/(float)0x1000000;
 public float Range(float a,float b)=>a+(b-a)*Next();
 public int Range(int a,int b)=>a+(int)(Next()*(b-a));      // [a,b)
 public bool Chance(float p)=>Next()<p;
 public T Pick<T>(IList<T> list)=>list[Range(0,list.Count)];
 public int State=>(int)s;
}
}
