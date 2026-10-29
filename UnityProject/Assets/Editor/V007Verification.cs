#if UNITY_EDITOR
using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using MakubeX;
[InitializeOnLoad]public static class V007Verification {
 const string Key="MakubeX.V007.QA";static string E=>Path.GetFullPath("../Verification/Chapter01_v007_20261027");
 [Serializable]class Saved{public bool existed;public int progress;}
 static V007Verification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
 public static void Run(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve existing editor session");File.WriteAllText(Path.Combine(E,"prefs-backup.json"),JsonUtility.ToJson(new Saved{existed=PlayerPrefs.HasKey(MakubeXApp.ProgressSaveKey),progress=PlayerPrefs.GetInt(MakubeXApp.ProgressSaveKey)}));SessionState.SetBool(Key,true);SessionState.SetInt(Key+"Exit",1);EditorSceneManager.OpenScene(Chapter01V007Playtest.EntryScene);EditorApplication.isPlaying=true;}
 static void Tick(){if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;EditorApplication.QueuePlayerLoopUpdate();var app=MakubeXApp.Instance;if(!app)return;var probe=app.GetComponent<V007QAProbe>();if(!probe)probe=app.gameObject.AddComponent<V007QAProbe>();if(probe.Complete){SessionState.SetInt(Key+"Exit",probe.result.status=="passed"?0:1);EditorApplication.isPlaying=false;}}
 static void Changed(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);var p=JsonUtility.FromJson<Saved>(File.ReadAllText(Path.Combine(E,"prefs-backup.json")));if(p.existed)PlayerPrefs.SetInt(MakubeXApp.ProgressSaveKey,p.progress);else PlayerPrefs.DeleteKey(MakubeXApp.ProgressSaveKey);PlayerPrefs.Save();EditorApplication.Exit(SessionState.GetInt(Key+"Exit",1));}
}
#endif
