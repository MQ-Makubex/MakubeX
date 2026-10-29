#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MakubeX;

[InitializeOnLoad]
public static class MakubeXAudioVerification
{
    const string Active="MakubeX.Audio.PassiveQA";
    [Serializable] class Pref{public string key;public bool exists;public float value;}
    [Serializable] class Prefs{public Pref[] values;}
    static string Backup=>Path.Combine(MakubeXAudioSetup.Evidence,"prefs_before.json");
    static MakubeXAudioVerification(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    [MenuItem("MakubeX/Audio/2 Start Passive PlayMode Verification")]
    public static void Start()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve unsaved changes and stop Play Mode first");
        var keys=new[]{MakubeXAudio.MusicKey,MakubeXAudio.AmbienceKey,MakubeXAudio.SfxKey};var prefs=new Prefs{values=new Pref[3]};for(int i=0;i<3;i++)prefs.values[i]=new Pref{key=keys[i],exists=PlayerPrefs.HasKey(keys[i]),value=PlayerPrefs.GetFloat(keys[i])};Directory.CreateDirectory(MakubeXAudioSetup.Evidence);File.WriteAllText(Backup,JsonUtility.ToJson(prefs,true));
        EditorSceneManager.OpenScene(MakubeX.Editor.ProjectSetup.EntryScene);SessionState.SetBool(Active,true);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Active,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;var app=MakubeXApp.Instance;if(!app)return;
        if(!app.GetComponent<MakubeXAudioVerificationProbe>()){var probe=app.gameObject.AddComponent<MakubeXAudioVerificationProbe>();probe.output=Path.Combine(MakubeXAudioSetup.Evidence,"playmode");}
    }
    [MenuItem("MakubeX/Audio/3 Capture Current Actual Mixer Output")]
    public static void Capture(){var probe=UnityEngine.Object.FindAnyObjectByType<MakubeXAudioVerificationProbe>();if(probe){probe.Capture("manual-"+DateTime.UtcNow.ToString("HHmmss"));probe.Save();}}
    [MenuItem("MakubeX/Audio/4 Finish Passive Verification")]
    public static void Finish(){var probe=UnityEngine.Object.FindAnyObjectByType<MakubeXAudioVerificationProbe>();if(probe)probe.Finish();EditorApplication.isPlaying=false;}
    static void Changed(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Active,false))return;SessionState.SetBool(Active,false);
        if(File.Exists(Backup)){var prefs=JsonUtility.FromJson<Prefs>(File.ReadAllText(Backup));foreach(var item in prefs.values)if(item.exists)PlayerPrefs.SetFloat(item.key,item.value);else PlayerPrefs.DeleteKey(item.key);PlayerPrefs.Save();File.WriteAllText(Path.Combine(MakubeXAudioSetup.Evidence,"prefs_restored.txt"),"Original audio preferences restored; chapter progress was never reset by this observer");}
    }
}
#endif
