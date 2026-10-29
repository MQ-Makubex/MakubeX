#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using MakubeX;
using MakubeX.Chapter01Integration;
using MakubeX.V004;

public static class Chapter01V004StorySetup
{
    public const string ScenePath="Assets/Scenes/Chapter01_V004_Story.unity";
    public static string Evidence=>Path.GetFullPath("../Verification/PhaseOne_HD_V004_20261025");
    [MenuItem("MakubeX/Phase One V004/1 Integrate Artwork And Latest Scene")]
    public static void Integrate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve unsaved scene edits and stop Play Mode before integration");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(var name in new[]{"cover","memory-map","chapter01-loading"})if(!File.Exists("Assets/StreamingAssets/UI/"+MakubeXApp.ArtworkRevision+"/"+name+".png"))throw new FileNotFoundException(name);
        EditorSceneManager.OpenScene(Chapter01V004VisualVerification.ScenePath);
        var env=GameObject.Find("Chapter01_V004_Environment");var transforms=env.GetComponentsInChildren<Transform>(true);
        Func<string,Transform> find=name=>transforms.First(t=>t.name==name);
        var origin=find("AXIS_Origin").position;var x=find("AXIS_X").position-origin;var y=find("AXIS_Y").position-origin;var z=find("AXIS_Z").position-origin;
        Func<float,float,float,Vector3> p=(a,b,c)=>origin+x*a+y*b+z*c;
        var walker=UnityEngine.Object.FindAnyObjectByType<Chapter01WalkController>();var door=UnityEngine.Object.FindAnyObjectByType<V004Door>();
        var story=new GameObject("Chapter 01 approved story integration").AddComponent<Chapter01V004Story>();
        story.walker=walker;story.door=door;walker.showHelp=false;door.showHelp=false;
        story.bag=find("PROP_OldBag");story.chair=find("PROP_Chair");story.computer=find("PROP_CRT");
        story.seat=p(-2.15f,2.5f,.17f);story.elderStart=p(-3.8f,1.75f,.17f);story.elderBesideChair=p(-3.05f,2.22f,.17f);
        story.bagCamera=p(1,2.6f,2.2f);story.roomCamera=p(.35f,-.08f,2.05f);story.bootCamera=p(-.1f,1,2.2f);
        var crt=story.computer.GetComponent<Renderer>();string matPath="Assets/Chapter01V004_Playtest/Materials/StoryPoweredCRT.mat";
        var shader=Shader.Find("MakubeX/V004 Story Powered CRT");if(!shader || !shader.isSupported)throw new InvalidOperationException("Story CRT shader unsupported");
        var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!material){material=new Material(crt.sharedMaterial);AssetDatabase.CreateAsset(material,matPath);}else material.CopyPropertiesFromMaterial(crt.sharedMaterial);
        material.shader=shader;material.DisableKeyword("_EMISSION");material.SetFloat("_Powered",0);EditorUtility.SetDirty(material);story.poweredScreenTemplate=material;
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);AssetDatabase.SaveAssets();
        var previous=EditorBuildSettings.scenes;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MakubeX.Editor.ProjectSetup.EntryScene,true),new EditorBuildSettingsScene(ScenePath,true)}.Concat(previous.Where(s=>s.path!=ScenePath && s.path!=MakubeX.Editor.ProjectSetup.EntryScene)).ToArray();
        var result=new IntegrationResult{scene=ScenePath,source=Chapter01V004VisualVerification.ScenePath,artworkRevision=MakubeXApp.ArtworkRevision,renderers=env.GetComponentsInChildren<Renderer>().Length,lights=UnityEngine.Object.FindObjectsByType<Light>().Length,masterAbsent=!transforms.Any(t=>t.name=="LatticeTower_Master"),bag=story.bag.position,chair=story.chair.position,crt=story.computer.position};
        Directory.CreateDirectory(Evidence);File.WriteAllText(Path.Combine(Evidence,"scene_integration.json"),JsonUtility.ToJson(result,true));
        EditorSceneManager.OpenScene(MakubeX.Editor.ProjectSetup.EntryScene);Debug.Log("PHASEONE_V004_INTEGRATED "+ScenePath);
    }
    [Serializable] public class IntegrationResult{public string scene,source,artworkRevision;public int renderers,lights;public bool masterAbsent;public Vector3 bag,chair,crt;}
    [MenuItem("MakubeX/Phase One V004/3 Build Integrated Intel Mac Player")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play Mode and preserve unsaved edits before building");
        if(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)!=ScriptingImplementation.Mono2x)throw new InvalidOperationException("Existing backend must be Mono x64");
#pragma warning disable CS0618
        UnityEditor.OSXStandalone.UserBuildSettings.architecture=OSArchitecture.x64;UnityEditor.OSXStandalone.UserBuildSettings.createXcodeProject=false;
#pragma warning restore CS0618
        string output=Path.GetFullPath("Builds/PhaseOne_HD_V004_20261025/MakubeX.app");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{MakubeX.Editor.ProjectSetup.EntryScene,ScenePath},target=BuildTarget.StandaloneOSX,locationPathName=output,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
        var result=new BuildResultRecord{output=output,result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize};
        using(var br=new BinaryReader(File.OpenRead(Path.Combine(output,"Contents/MacOS",PlayerSettings.productName))))result.intelX64=br.ReadUInt32()==0xfeedfacf && br.ReadUInt32()==0x01000007;
        string data=Path.Combine(output,"Contents/Resources/Data");result.dataPresent=File.Exists(Path.Combine(data,"data.unity3d")) || File.Exists(Path.Combine(data,"globalgamemanagers"));
        if(result.errors>0 || !result.intelX64 || !result.dataPresent)result.result="FailedValidation";
        File.WriteAllText(Path.Combine(Evidence,"build.json"),JsonUtility.ToJson(result,true));
        if(result.result!="Succeeded")throw new Exception("Integrated build failed");Debug.Log("PHASEONE_V004_PLAYER_BUILT "+output);
    }
    [Serializable] public class BuildResultRecord{public string output,result;public int errors,warnings;public ulong bytes;public bool intelX64,dataPresent;}
}
#endif
