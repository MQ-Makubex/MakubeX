#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using MakubeX.Chapter01Integration;

[InitializeOnLoad]
public static class Chapter01IntegrationCheck
{
    public const string Root = "Assets/Chapter01Integration_20261024";
    public const string ScenePath = Root + "/Chapter01_BridgeToHut_Test.unity";
    static string Output => Path.GetFullPath("../Verification/Chapter01_20261024/unity");
    const string ActiveKey = "MakubeX.Chapter01IntegrationCheck.Active";
    [Serializable] public class Point { public float[] position; }
    [Serializable] public class Cam { public string name; public float[] position, target; public float lens; }
    [Serializable] public class Mat { public string name; }
    [Serializable] public class Manifest { public Point[] route; public Cam[] cameras; public Mat[] materials; }
    [Serializable] public class Report
    {
        public string unityVersion, scene, graphicsDevice;
        public int renderers, colliders, materials, boundMaterials, routeSamples, supportedSamples, missingScriptObjects;
        public bool meterScale, allMaterialsSupported;
        public Vector3 axisX, axisY, axisZ;
        public List<string> issues = new List<string>(), dependencies = new List<string>(), unusedMaterials = new List<string>();
    }
    static Chapter01IntegrationCheck() { EditorApplication.update += Tick; }
    static Vector3 P(GameObject env, float[] p)
    {
        var ts=env.GetComponentsInChildren<Transform>();
        Vector3 o=ts.First(t=>t.name=="AXIS_Origin").position;
        return o+(ts.First(t=>t.name=="AXIS_X").position-o)*p[0]+(ts.First(t=>t.name=="AXIS_Y").position-o)*p[1]+(ts.First(t=>t.name=="AXIS_Z").position-o)*p[2];
    }
    static Manifest ReadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/UnitySceneManifest.json"));

    [MenuItem("MakubeX/Chapter 01 Integration/Open Bridge to Hut Test")]
    public static void OpenTest()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(ScenePath);
    }

    public static void Build()
    {
        // Only this newly generated scene is rebuilt; existing chapter scenes and source assets are untouched.
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var info=ReadManifest();
        var env=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Chapter01_TheName.fbx"));
        env.name="Existing_Chapter01_Environment";
        // Unity renamed the existing material assets to M000, M001... at CreateAsset time.
        // Resolve the original FBX material names through the export manifest's stable index.
        var mats=new Dictionary<string,Material>();
        for(int i=0;i<info.materials.Length;i++)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/M"+i.ToString("D3")+".mat");
            if(material==null)throw new Exception("Missing existing material asset at index "+i);
            mats.Add(info.materials[i].name,material);
        }
        var unmatched=new HashSet<string>();
        var usedMaterials=new HashSet<Material>();
        foreach(var rr in env.GetComponentsInChildren<Renderer>())
        {
            rr.sharedMaterials=rr.sharedMaterials.Select(m=>{
                if(m!=null && mats.TryGetValue(m.name,out var replacement)){usedMaterials.Add(replacement);return replacement;}
                unmatched.Add(m==null?"null material":m.name);return m;
            }).ToArray();
        }
        if(unmatched.Count>0)throw new Exception("Unmapped FBX materials: "+string.Join(", ",unmatched));
        var collision=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Chapter01_Collision.fbx"));
        collision.name="Existing_Collision_Guides";
        foreach(var mf in collision.GetComponentsInChildren<MeshFilter>())
        {
            var c=mf.gameObject.AddComponent<MeshCollider>();c.sharedMesh=mf.sharedMesh;
            var rr=mf.GetComponent<Renderer>();if(rr!=null)rr.enabled=false;
        }
        foreach(var mf in env.GetComponentsInChildren<MeshFilter>())
        {
            if(mf.name.StartsWith("Platform_GuardRails") || mf.name.StartsWith("Hut_Frame_Walls") || mf.name.StartsWith("Walkable_"))
            {var c=mf.gameObject.AddComponent<MeshCollider>();c.sharedMesh=mf.sharedMesh;}
            if(new[]{"PROP_Bed","PROP_Workbench","PROP_Chair","PROP_Crates","PROP_CRT","PROP_OldBag"}.Any(prefix=>mf.name.StartsWith(prefix)))mf.gameObject.AddComponent<BoxCollider>();
        }
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.12f,.18f,.32f);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.06f,.11f,.22f);RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.003f;
        var moon=new GameObject("Moon_Key",typeof(Light)).GetComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(.39f,.55f,1);moon.intensity=.8f;moon.transform.rotation=Quaternion.Euler(32,-35,0);
        var warm=new GameObject("Hut_WarmFill",typeof(Light)).GetComponent<Light>();warm.type=LightType.Point;warm.color=new Color(1,.58f,.25f);warm.intensity=3;warm.range=11;warm.transform.position=P(env,new[]{0f,2.5f,2.8f});warm.renderMode=LightRenderMode.ForcePixel;
        foreach(var ci in info.cameras)
        {
            var c=new GameObject(ci.name,typeof(Camera)).GetComponent<Camera>();c.transform.position=P(env,ci.position);c.transform.rotation=Quaternion.LookRotation(P(env,ci.target)-c.transform.position,Vector3.up);
            c.fieldOfView=2*Mathf.Atan(24/(2*ci.lens))*Mathf.Rad2Deg;c.nearClipPlane=.08f;c.farClipPlane=600;c.enabled=false;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.035f,.065f,.14f);
            if(ci.name=="CAM_07_TopView"){c.orthographic=true;c.orthographicSize=19.5f;}
        }
        var player=new GameObject("BridgeTest_Player",typeof(CharacterController),typeof(Chapter01WalkController));
        var body=player.GetComponent<CharacterController>();body.height=1.8f;body.radius=.28f;body.center=new Vector3(0,.9f,0);body.stepOffset=.3f;body.slopeLimit=45;body.skinWidth=.03f;body.minMoveDistance=0;
        var control=player.GetComponent<Chapter01WalkController>();control.spawn=P(env,info.route[0].position);player.transform.position=control.spawn;control.Face(P(env,info.route[1].position)-control.spawn);
        var eye=new GameObject("Player Camera",typeof(Camera),typeof(AudioListener));eye.tag="MainCamera";eye.transform.SetParent(player.transform,false);eye.transform.localPosition=new Vector3(0,1.62f,0);
        control.view=eye.GetComponent<Camera>();control.view.fieldOfView=65;control.view.nearClipPlane=.05f;control.view.farClipPlane=600;control.view.clearFlags=CameraClearFlags.SolidColor;control.view.backgroundColor=new Color(.035f,.065f,.14f);
        Physics.SyncTransforms();
        var report=new Report{unityVersion=Application.unityVersion,scene=ScenePath,graphicsDevice=SystemInfo.graphicsDeviceName,renderers=env.GetComponentsInChildren<Renderer>().Length,colliders=collision.GetComponentsInChildren<Collider>().Length+env.GetComponentsInChildren<Collider>().Length,materials=mats.Count};
        report.boundMaterials=usedMaterials.Count;
        report.unusedMaterials=mats.Where(entry=>!usedMaterials.Contains(entry.Value)).Select(entry=>entry.Key).ToList();
        report.axisX=P(env,new[]{1f,0,0})-P(env,new[]{0f,0,0});report.axisY=P(env,new[]{0f,1,0})-P(env,new[]{0f,0,0});report.axisZ=P(env,new[]{0f,0,1})-P(env,new[]{0f,0,0});
        var foundation=env.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Hut_SteelFoundation")).bounds.size;
        report.meterScale=Mathf.Abs(foundation.x-14)<.05f && Mathf.Abs(foundation.y-16)<.05f && Mathf.Abs(foundation.z-11)<.05f;
        report.allMaterialsSupported=mats.Values.All(m=>m.shader!=null && m.shader.isSupported);
        foreach(var entry in mats) if(entry.Value.mainTexture==null && entry.Key.StartsWith("tripo_mat")) report.issues.Add("Missing base texture: "+entry.Key);
        body.enabled=false;
        for(int i=1;i<info.route.Length;i++)
        {
            Vector3 a=P(env,info.route[i-1].position),b=P(env,info.route[i].position);int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.2f);
            for(int j=0;j<=steps;j++)
            {
                Vector3 q=Vector3.Lerp(a,b,j/(float)steps);report.routeSamples++;
                if(Physics.Raycast(new Vector3(q.x,.45f,q.z),Vector3.down,out var hit,1f) && hit.point.y>=-.05f && hit.point.y<=.26f)report.supportedSamples++;
                else report.issues.Add("Unsupported route at "+q);
            }
        }
        body.enabled=true;
        foreach(var go in scene.GetRootGameObjects())foreach(var t in go.GetComponentsInChildren<Transform>(true))report.missingScriptObjects+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        report.dependencies=AssetDatabase.GetDependencies(ScenePath,true).ToList();
        if(report.dependencies.Count(p=>p.EndsWith(".mat"))!=usedMaterials.Count)report.issues.Add("Scene does not reference all bound external materials");
        File.WriteAllText(Path.Combine(Output,"integration.json"),JsonUtility.ToJson(report,true));
        foreach(string name in new[]{"CAM_01_Overview","CAM_03_HutInterior"})Capture(GameObject.Find(name).GetComponent<Camera>(),Path.Combine(Output,name+".png"));
        if(!report.meterScale || !report.allMaterialsSupported || report.missingScriptObjects>0 || report.supportedSamples!=report.routeSamples || report.issues.Count>0)throw new Exception("Integration checks failed; inspect integration.json");
        Debug.Log("CHAPTER01_INTEGRATION_CREATED "+ScenePath);
    }

    static void Capture(Camera cam,string path)
    {
        var previous=RenderTexture.active;var oldTarget=cam.targetTexture;var rt=new RenderTexture(1200,840,24);var image=new Texture2D(1200,840,TextureFormat.RGB24,false);
        try {cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1200,840),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally {cam.targetTexture=oldTarget;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
    }

    public static void RunPlay()
    {
        EditorSceneManager.OpenScene(ScenePath);
        SessionState.SetBool(ActiveKey,true);SessionState.SetFloat(ActiveKey+".Start",(float)EditorApplication.timeSinceStartup);
        EditorApplication.isPlaying=true;
    }
    public static void BuildAndRunPlay() { Build(); RunPlay(); }
    static void Tick()
    {
        if(!SessionState.GetBool(ActiveKey,false))return;
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat(ActiveKey+".Start",0)>240)
        {SessionState.SetBool(ActiveKey,false);Debug.LogError("CHAPTER01_PLAY_TIMEOUT");EditorApplication.Exit(2);return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.frameCount<2)return;
        var player=UnityEngine.Object.FindAnyObjectByType<Chapter01WalkController>();
        if(player==null)return;
        var probe=player.GetComponent<BridgeWalkProbe>();
        if(probe==null)
        {
            player.verificationMode=true;
            probe=player.gameObject.AddComponent<BridgeWalkProbe>();probe.player=player;probe.output=Output;
            var env=GameObject.Find("Existing_Chapter01_Environment");probe.route=ReadManifest().route.Select(p=>P(env,p.position)).ToArray();
        }
        if(probe.Complete){SessionState.SetBool(ActiveKey,false);EditorApplication.Exit(probe.Passed?0:1);}
    }
}
#endif
