#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MakubeX;

[InitializeOnLoad]
public static class PhaseOneV004Verification
{
    const string Key="MakubeX.PhaseOneV004.QA";
    static string Backup=>Path.Combine(Chapter01V004StorySetup.Evidence,"playerprefs_backup.json");
    [Serializable] class Saved{public bool existed;public int value;}
    static PhaseOneV004Verification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    [MenuItem("MakubeX/Phase One V004/2 Verify Integrated Workflow With Real E Keys")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve unsaved changes and stop Play Mode first");
        Directory.CreateDirectory(Chapter01V004StorySetup.Evidence);
        File.WriteAllText(Backup,JsonUtility.ToJson(new Saved{existed=PlayerPrefs.HasKey(MakubeXApp.ProgressSaveKey),value=PlayerPrefs.GetInt(MakubeXApp.ProgressSaveKey)},true));
        PlayerPrefs.SetInt(MakubeXApp.ProgressSaveKey,0);PlayerPrefs.Save();SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene(MakubeX.Editor.ProjectSetup.EntryScene);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount<2)return;
        EditorApplication.QueuePlayerLoopUpdate();var app=MakubeXApp.Instance;if(!app)return;
        var probe=app.GetComponent<PhaseOneV004QAProbe>();if(!probe){probe=app.gameObject.AddComponent<PhaseOneV004QAProbe>();probe.output=Path.Combine(Chapter01V004StorySetup.Evidence,"captures");}
        if(probe.Complete)EditorApplication.isPlaying=false;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key,false))return;
        SessionState.SetBool(Key,false);
        if(File.Exists(Backup)){var saved=JsonUtility.FromJson<Saved>(File.ReadAllText(Backup));if(saved.existed)PlayerPrefs.SetInt(MakubeXApp.ProgressSaveKey,saved.value);else PlayerPrefs.DeleteKey(MakubeXApp.ProgressSaveKey);PlayerPrefs.Save();File.WriteAllText(Path.Combine(Chapter01V004StorySetup.Evidence,"playerprefs_restored.txt"),"Original progress restored after real Play Mode verification");}
    }
}
#endif
