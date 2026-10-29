#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;
using MakubeX.Chapter01Integration;
using MakubeX.V004;

[InitializeOnLoad]
public static class Chapter01V004VisualVerification
{
    public const string ScenePath="Assets/Chapter01V004_Playtest/Chapter01_V004_Playtest.unity";
    public static string Evidence=>Path.GetFullPath("../Verification/Chapter01_v004_20261025/visual_fix_20261025");
    [Serializable] public class Pose {public string name,blender_camera;public float[] position,target,up;public float vertical_fov_degrees;public int frame;}
    [Serializable] public class PoseList {public Pose[] poses;}
    [Serializable] public class ViewResult {public string name,image;public int width,height;public float[] blenderPosition;public float verticalFov;public double medianRenderReadbackMs,p90RenderReadbackMs;public bool hdr,bloom;}
    [Serializable] public class CaptureReport {public string unityVersion,graphicsDevice,mode;public int renderers,lights,shadowLights;public bool hiddenMasterPresent;public List<ViewResult> views=new List<ViewResult>();}
    static Vector3 origin,xaxis,yaxis,zaxis;
    const string ActiveKey="MakubeX.V004.VisualRuntimeQA";
    static Chapter01V004VisualVerification(){EditorApplication.update+=TickRuntime;}
    [MenuItem("MakubeX/V004/Visual QA/2 Rebuild And Capture")]
    public static void RebuildAndCapture(){Chapter01V004Playtest.Build();CaptureAfter();}
    [MenuItem("MakubeX/V004/Visual QA/6 Run Runtime Walk API Test")]
    public static void RunRuntimeAutomated(){RunRuntime();SessionState.SetBool("MakubeX.V004.DirectToggle",true);}
    [MenuItem("MakubeX/V004/Visual QA/4 Run Real Input Walk Test")]
    public static void RunRuntime()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Unsaved scene changes need preservation before QA");
        EditorSceneManager.OpenScene(ScenePath);SessionState.SetBool("MakubeX.V004.DirectToggle",false);SessionState.SetBool(ActiveKey,true);EditorApplication.isPlaying=true;
    }
    static void TickRuntime()
    {
        if(!SessionState.GetBool(ActiveKey,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount<2)return;
        var player=UnityEngine.Object.FindAnyObjectByType<Chapter01WalkController>();if(player==null)return;
        var probe=player.GetComponent<V004WalkVerificationProbe>();
        if(probe==null)
        {
            ResolveAxes(GameObject.Find("Chapter01_V004_Environment"));player.verificationMode=true;
            probe=player.gameObject.AddComponent<V004WalkVerificationProbe>();probe.directToggleAutomation=SessionState.GetBool("MakubeX.V004.DirectToggle",false);probe.player=player;probe.door=UnityEngine.Object.FindAnyObjectByType<V004Door>();probe.output=Path.Combine(Evidence,"unity");
            probe.route=new[]{new[]{-17f,-11.345f,.15f},new[]{-3f,-2.2f,.15f},new[]{0f,-1.2f,.15f},new[]{0f,.9f,.2f},new[]{.3f,1.8f,.2f}}.Select(P).ToArray();
        }
        if(probe.Complete){SessionState.SetBool(ActiveKey,false);EditorApplication.isPlaying=false;}
    }
    [MenuItem("MakubeX/V004/Visual QA/5 Build V004 Intel Mac Player")]
    public static void BuildPlayer()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before building");
        if(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone)!=ScriptingImplementation.Mono2x)throw new InvalidOperationException("Existing Mac backend is not Mono x64; no unrelated PlayerSettings changes were applied");
#pragma warning disable CS0618
        UnityEditor.OSXStandalone.UserBuildSettings.architecture=OSArchitecture.x64;
        UnityEditor.OSXStandalone.UserBuildSettings.createXcodeProject=false;
#pragma warning restore CS0618
        string output=Path.GetFullPath("Builds/V004_VisualFix_20261025/MakubeX_V004.app");Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.StandaloneOSX,locationPathName=output,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
        var result=new PlayerBuildResult{result=report.summary.totalErrors>0?"FailedValidation":report.summary.result.ToString(),engineResult=report.summary.result.ToString(),output=output,seconds=report.summary.totalTime.TotalSeconds,bytes=report.summary.totalSize,errors=report.summary.totalErrors,warnings=report.summary.totalWarnings};
        string binary=Path.Combine(output,"Contents/MacOS",PlayerSettings.productName);
        if(File.Exists(binary))using(var reader=new BinaryReader(File.OpenRead(binary))){uint magic=reader.ReadUInt32(),cpu=reader.ReadUInt32();result.intelX64=magic==0xfeedfacf && cpu==0x01000007;}
        string data=Path.Combine(output,"Contents/Resources/Data");
        result.playerDataPresent=File.Exists(Path.Combine(data,"globalgamemanagers")) || File.Exists(Path.Combine(data,"data.unity3d"));
        if(!result.playerDataPresent)result.result="FailedValidation";
        File.WriteAllText(Path.Combine(Evidence,"player_build.json"),JsonUtility.ToJson(result,true));
        if(report.summary.result!=BuildResult.Succeeded || report.summary.totalErrors>0 || !result.intelX64 || !result.playerDataPresent)throw new Exception("V004 Intel player build failed");
        UnityEngine.Debug.Log("V004_INTEL_PLAYER_BUILT "+output);
    }
    [Serializable] public class PlayerBuildResult {public string result,engineResult,output;public double seconds;public ulong bytes;public int errors,warnings;public bool intelX64,playerDataPresent;}
    public static void ResolveAxes(GameObject env)
    {
        var ts=env.GetComponentsInChildren<Transform>(true);origin=ts.First(t=>t.name=="AXIS_Origin").position;
        xaxis=ts.First(t=>t.name=="AXIS_X").position-origin;yaxis=ts.First(t=>t.name=="AXIS_Y").position-origin;zaxis=ts.First(t=>t.name=="AXIS_Z").position-origin;
    }
    public static Vector3 P(float[] p)=>origin+xaxis*p[0]+yaxis*p[1]+zaxis*p[2];
    static Vector3 D(float[] p)=>xaxis*p[0]+yaxis*p[1]+zaxis*p[2];

    [MenuItem("MakubeX/V004/Visual QA/1 Capture Before Fix")]
    public static void CaptureBefore()=>Capture("before");
    [MenuItem("MakubeX/V004/Visual QA/3 Capture After Fix")]
    public static void CaptureAfter()=>Capture("unity");
    public static void Capture(string folder)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before deterministic captures.");
        var current=EditorSceneManager.GetActiveScene();
        if(current.isDirty)throw new InvalidOperationException("Current scene has unsaved changes; preserve them before QA capture.");
        EditorSceneManager.OpenScene(ScenePath);
        var env=GameObject.Find("Chapter01_V004_Environment");ResolveAxes(env);
        var source=UnityEngine.Object.FindAnyObjectByType<Chapter01WalkController>().view;
        var hinge=env.GetComponentsInChildren<Transform>().First(t=>t.name=="DOOR_Hinge_Open_Close");
        var open=env.GetComponentsInChildren<Transform>().First(t=>t.name=="DOOR_OpenPose");var closed=hinge.rotation;
        var poses=JsonUtility.FromJson<PoseList>(File.ReadAllText(Path.Combine(Evidence,"comparison_poses.json"))).poses;
        var report=new CaptureReport{unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName,mode="Edit Mode Camera.Render, identical authored Blender poses, HDR render target, synchronous 1px readback for comparable GPU completion timings",renderers=env.GetComponentsInChildren<Renderer>().Length,lights=UnityEngine.Object.FindObjectsByType<Light>().Length,shadowLights=UnityEngine.Object.FindObjectsByType<Light>().Count(l=>l.shadows!=LightShadows.None),hiddenMasterPresent=env.GetComponentsInChildren<MeshFilter>().Any(m=>m.name=="LatticeTower_Master")};
        Directory.CreateDirectory(Path.Combine(Evidence,folder));
        try
        {
            foreach(var pose in poses)
            {
                hinge.rotation=pose.frame>=80?open.rotation:closed;Physics.SyncTransforms();
                var go=UnityEngine.Object.Instantiate(source.gameObject);go.name="Temporary_IdenticalPose_"+pose.name;go.hideFlags=HideFlags.HideAndDontSave;
                var listener=go.GetComponent<AudioListener>();if(listener!=null)UnityEngine.Object.DestroyImmediate(listener);
                var cam=go.GetComponent<Camera>();cam.enabled=false;cam.transform.SetPositionAndRotation(P(pose.position),Quaternion.LookRotation(P(pose.target)-P(pose.position),D(pose.up)));cam.fieldOfView=pose.vertical_fov_degrees;cam.aspect=16f/9;cam.farClipPlane=750;
                string path=Path.Combine(Evidence,folder,pose.name+".png");
                var result=CaptureCamera(cam,path,true);result.name=pose.name;result.blenderPosition=pose.position;result.verticalFov=pose.vertical_fov_degrees;report.views.Add(result);
                UnityEngine.Object.DestroyImmediate(go);
            }
            File.WriteAllText(Path.Combine(Evidence,folder,"captures.json"),JsonUtility.ToJson(report,true));
            UnityEngine.Debug.Log("V004_VISUAL_CAPTURE_COMPLETE "+folder+" "+JsonUtility.ToJson(report));
        }
        finally {hinge.rotation=closed;EditorSceneManager.OpenScene(ScenePath);}
    }
    public static ViewResult CaptureCamera(Camera cam,string path,bool benchmark)
    {
        const int width=1280,height=720;
        var previous=RenderTexture.active;var target=cam.targetTexture;var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGBHalf);rt.Create();
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);var timings=new List<double>();
        try
        {
            cam.targetTexture=rt;
            for(int i=0;i<(benchmark?27:1);i++)
            {
                var watch=Stopwatch.StartNew();cam.Render();RenderTexture.active=rt;pixel.ReadPixels(new Rect(0,0,1,1),0,0,false);watch.Stop();
                if(i>=3)timings.Add(watch.Elapsed.TotalMilliseconds);
            }
            RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            timings.Sort();
            var post=cam.GetComponent<V004NightPost>();
            return new ViewResult{image=path,width=width,height=height,hdr=cam.allowHDR,bloom=post!=null && post.enabled && post.filterMaterial!=null && post.filterMaterial.shader.isSupported,medianRenderReadbackMs=timings.Count>0?timings[timings.Count/2]:0,p90RenderReadbackMs=timings.Count>0?timings[(int)(timings.Count*.9)]:0};
        }
        finally {cam.targetTexture=target;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(pixel);}
    }
}
#endif
