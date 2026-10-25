using UnityEngine;
namespace Checkmate55 {
public sealed class DialogueManager {
 public string Text {get;private set;}="Welcome aboard.";
 float until=9;
 public void Say(string text){Text=text;until=Time.unscaledTime+6f;}
 public string Visible => Time.unscaledTime<until?Text:"";
}
}
