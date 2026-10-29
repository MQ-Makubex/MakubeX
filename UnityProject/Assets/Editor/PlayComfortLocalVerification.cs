#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MakubeX;
[InitializeOnLoad]
public static class PlayComfortLocalVerification {
 const string Key="MakubeX.PlayComfortQA";
 [Serializable]class Prefs {public bool musicExisted;public float music;}
 static string Backup=>Path.GetFullPath("../Verification/PlayComfortLocal_20261026/prefs-backup.json");
 static PlayComfortLocalVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 public static void Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Preserve current Play session");
  Directory.CreateDirectory(Path.GetDirectoryName(Backup));File.WriteAllText(Backup,JsonUtility.ToJson(new Prefs{musicExisted=PlayerPrefs.HasKey(MakubeXAudio.MusicKey),music=PlayerPrefs.GetFloat(MakubeXAudio.MusicKey,.75f)},true));
  SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Code",1);EditorSceneManager.OpenScene(MakubeX.Editor.ProjectSetup.EntryScene);EditorApplication.isPlaying=true;
 }
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;EditorApplication.QueuePlayerLoopUpdate();var app=MakubeXApp.Instance;if(!app)return;var probe=app.GetComponent<PlayComfortQAProbe>();if(!probe)probe=app.gameObject.AddComponent<PlayComfortQAProbe>();if(probe.Complete){SessionState.SetInt(Key+"Code",probe.result.status=="passed"?0:1);EditorApplication.isPlaying=false;}}
 static void Changed(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);if(File.Exists(Backup)){var saved=JsonUtility.FromJson<Prefs>(File.ReadAllText(Backup));if(saved.musicExisted)PlayerPrefs.SetFloat(MakubeXAudio.MusicKey,saved.music);else PlayerPrefs.DeleteKey(MakubeXAudio.MusicKey);PlayerPrefs.Save();}EditorApplication.Exit(SessionState.GetInt(Key+"Code",1));}
}
#endif
