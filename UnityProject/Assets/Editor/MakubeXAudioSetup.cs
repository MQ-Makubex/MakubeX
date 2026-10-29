#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using MakubeX;

public static class MakubeXAudioSetup
{
    public static string Evidence=>Path.GetFullPath("../Verification/Audio_20261026");
    const string MixerPath="Assets/Audio/MakubeX_20261026/MakubeX.mixer";
    const string CatalogPath="Assets/Resources/Audio/MakubeXAudioCatalog.asset";
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    [MenuItem("MakubeX/Audio/1 Configure Supplied Audio And Mixer")]
    public static void Configure()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before configuring audio");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Directory.CreateDirectory("Assets/Resources/Audio");
        var controllerType=TypeCache.GetTypesDerivedFrom<AudioMixer>().First(t=>t.FullName=="UnityEditor.Audio.AudioMixerController");
        var mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if(!mixer)mixer=(AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath",Flags).Invoke(null,new object[]{MixerPath});
        var groupType=controllerType.Assembly.GetType("UnityEditor.Audio.AudioMixerGroupController");
        var master=controllerType.GetProperty("masterGroup",Flags).GetValue(mixer);
        var groupMap=new Dictionary<string,object>{{"Master",master}};
        var existing=AssetDatabase.LoadAllAssetsAtPath(MixerPath).OfType<AudioMixerGroup>().ToArray();
        foreach(var name in new[]{"Music","Music Duck","Ambience","Outside Bed","Inside Bed","SFX","Dialogue","UI"})
        {
            object group=existing.FirstOrDefault(g=>g.name==name);
            if(group==null)group=controllerType.GetMethod("CreateNewGroup",Flags).Invoke(mixer,new object[]{name,false});
            groupMap[name]=group;
        }
        void Children(string parent,params string[] children)
        {
            var array=Array.CreateInstance(groupType,children.Length);for(int i=0;i<children.Length;i++)array.SetValue(groupMap[children[i]],i);
            groupType.GetProperty("children",Flags).SetValue(groupMap[parent],array);
        }
        Children("Master","Music","Ambience","SFX","Dialogue","UI");Children("Music","Music Duck");Children("Ambience","Outside Bed","Inside Bed");
        var exposedType=controllerType.Assembly.GetType("UnityEditor.Audio.ExposedAudioParameter");var exposed=Array.CreateInstance(exposedType,4);
        var buses=new[]{"Music","Music Duck","Ambience","SFX"};var names=new[]{"MusicVolumeDb","MusicDuckDb","AmbienceVolumeDb","SFXVolumeDb"};
        for(int i=0;i<buses.Length;i++)
        {
            object parameter=Activator.CreateInstance(exposedType);exposedType.GetField("guid",Flags).SetValue(parameter,groupType.GetMethod("GetGUIDForVolume",Flags).Invoke(groupMap[buses[i]],null));exposedType.GetField("name",Flags).SetValue(parameter,names[i]);exposed.SetValue(parameter,i);
        }
        controllerType.GetProperty("exposedParameters",Flags).SetValue(mixer,exposed);
        var snapshot=controllerType.GetProperty("TargetSnapshot",Flags).GetValue(mixer);
        groupType.GetMethod("SetValueForVolume",Flags).Invoke(groupMap["Outside Bed"],new[]{(object)mixer,snapshot,8f});
        groupType.GetMethod("SetValueForVolume",Flags).Invoke(groupMap["Inside Bed"],new[]{(object)mixer,snapshot,14f});
        mixer.updateMode=AudioMixerUpdateMode.UnscaledTime;EditorUtility.SetDirty(mixer);
        var catalog=AssetDatabase.LoadAssetAtPath<MakubeXAudioCatalog>(CatalogPath);
        if(!catalog){catalog=ScriptableObject.CreateInstance<MakubeXAudioCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
        catalog.mixer=mixer;catalog.music=(AudioMixerGroup)groupMap["Music Duck"];catalog.ambienceOutside=(AudioMixerGroup)groupMap["Outside Bed"];catalog.ambienceInside=(AudioMixerGroup)groupMap["Inside Bed"];catalog.sfx=(AudioMixerGroup)groupMap["SFX"];
        var fields=new Dictionary<string,string>{
            {"mapLoop","MemoryMap/MUS_MemoryMap_UnreadMemories_Loop.ogg"},{"outsideLoop","Chapter01/AMB_Outside_LowerCity_Loop.ogg"},{"insideLoop","Chapter01/AMB_Inside_Hut_Loop.ogg"},
            {"nameFragment","Chapter01/MUS_Name_Fragment_Once.ogg"},{"homeTheme","Chapter01/MUS_Home_Theme_Once.ogg"},{"doorOpen","Chapter01/SFX_Door_Open_Placeholder.ogg"},{"doorClose","Chapter01/SFX_Door_Close_Placeholder.ogg"},
            {"bag","Chapter01/SFX_Bag_Inspect_Placeholder.ogg"},{"chair","Chapter01/SFX_Chair_Sit_Placeholder.ogg"},{"crt","Chapter01/SFX_CRT_PowerOn_Placeholder.ogg"},{"pat","Chapter01/SFX_Grandpa_Pat_Placeholder.ogg"}};
        var report=new ImportReport{mixer=MixerPath,catalog=CatalogPath};
        foreach(var pair in fields)
        {
            string path="Assets/Audio/MakubeX_20261026/"+pair.Value;
            if(pair.Key=="mapLoop" || pair.Key=="nameFragment" || pair.Key=="homeTheme")
            {
                string polished="Assets/Audio/MakubeX_20261026/Polished/"+Path.GetFileNameWithoutExtension(pair.Value)+"_Polished.wav";
                if(File.Exists(polished))path=polished;
            }
            if(!File.Exists(path))throw new FileNotFoundException("Supplied audio missing",path);
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;if(importer==null)throw new Exception("Audio importer missing: "+path);
            var settings=importer.defaultSampleSettings;bool stream=pair.Key.EndsWith("Loop");
            settings.loadType=stream?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=stream?AudioCompressionFormat.Vorbis:AudioCompressionFormat.PCM;settings.quality=.85f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.loadInBackground=false;importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);if(!clip || clip.length<=0)throw new Exception("Audio decode failed: "+path);
            typeof(MakubeXAudioCatalog).GetField(pair.Key).SetValue(catalog,clip);report.clips.Add(new ClipRow{path=path,seconds=clip.length,channels=clip.channels,rate=clip.frequency,streaming=stream});
        }
        EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();Directory.CreateDirectory(Evidence);File.WriteAllText(Path.Combine(Evidence,"import.json"),JsonUtility.ToJson(report,true));Debug.Log("MAKUBEX_AUDIO_CONFIGURED "+report.clips.Count+" supplied clips");
    }
    [Serializable] class ImportReport{public string mixer,catalog;public List<ClipRow> clips=new List<ClipRow>();}
    [Serializable] class ClipRow{public string path;public float seconds;public int channels,rate;public bool streaming;}
}
#endif
