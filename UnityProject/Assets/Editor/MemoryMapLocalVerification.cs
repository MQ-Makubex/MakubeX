#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using MakubeX;
[InitializeOnLoad]
public static class MemoryMapLocalVerification {
 const string Key="MakubeX.MemoryMapLocalQA";
 static MemoryMapLocalVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Preserve current play session");
  SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Code",1);
  EditorSceneManager.OpenScene(MakubeX.Editor.ProjectSetup.EntryScene);EditorApplication.isPlaying=true;
 }
 static void Tick(){
  if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
  EditorApplication.QueuePlayerLoopUpdate();var app=MakubeXApp.Instance;if(!app)return;
  var probe=app.GetComponent<MemoryMapLocalQAProbe>();if(!probe)probe=app.gameObject.AddComponent<MemoryMapLocalQAProbe>();
  if(probe.Complete){SessionState.SetInt(Key+"Code",probe.result.status=="passed"?0:1);EditorApplication.isPlaying=false;}
 }
 static void Changed(PlayModeStateChange state){
  if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
  SessionState.SetBool(Key,false);EditorApplication.Exit(SessionState.GetInt(Key+"Code",1));
 }
}
#endif
