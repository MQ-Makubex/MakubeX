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
using MakubeX.V003;

public static class Chapter01V003Playtest
{
    const string Root="Assets/Chapter01V003_Playtest";
    public const string ScenePath=Root+"/Chapter01_V003_Playtest.unity";
    static string Evidence=>Path.GetFullPath("../Verification/Chapter01_v003_20261025");
    [Serializable] public class Mat
    {
        public string name,base_texture,normal_texture,unity_metallic_texture;
        public int index;
        public float[] color,emission,dark,light,grain;
        public float metallic,roughness,emission_strength;
        public bool procedural;
    }
    [Serializable] public class Lamp { public string name,type; public float[] position,direction,color; public float energy; }
    [Serializable] public class Manifest { public string source,source_sha256; public Mat[] materials; public Lamp[] lights; }
    [Serializable] public class Check
    {
        public string source,sourceSha256,scene,status,walkCheck,error;
        public int renderers,materials,environmentColliders,completedWaypoints;
        public bool doorBlockedWhenClosed,doorClearWhenOpen;
        public List<Vector3> route=new List<Vector3>(),trajectory=new List<Vector3>();
        public List<string> materialIssues=new List<string>();
    }
    static Color C(float[] c)=>new Color(c[0],c[1],c[2],c.Length>3?c[3]:1);
    static Vector3 origin,xaxis,yaxis,zaxis;
    static Vector3 P(float x,float y,float z)=>origin+xaxis*x+yaxis*y+zaxis*z;
    static Vector3 P(float[] p)=>P(p[0],p[1],p[2]);
    static Vector3 D(float[] p)=>xaxis*p[0]+yaxis*p[1]+zaxis*p[2];

    [MenuItem("MakubeX/V003/Build Latest Playtest")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before building.");
        if(EditorSceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Directory.CreateDirectory(Evidence);
        try { BuildInternal(); }
        catch(Exception e) { File.WriteAllText(Path.Combine(Evidence,"build_error.txt"),e.ToString());Debug.LogException(e);throw; }
    }

    static void BuildInternal()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var info=JsonUtility.FromJson<Manifest>(File.ReadAllText(Root+"/SceneManifest.json"));
        Directory.CreateDirectory(Root+"/Materials");
        var normals=new HashSet<string>(info.materials.Select(m=>m.normal_texture).Where(p=>!string.IsNullOrEmpty(p)));
        var metallics=new HashSet<string>(info.materials.Select(m=>m.unity_metallic_texture).Where(p=>!string.IsNullOrEmpty(p)));
        foreach(string rel in normals.Concat(metallics))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/"+rel);
            bool normal=normals.Contains(rel);
            if(importer.textureType==(normal?TextureImporterType.NormalMap:TextureImporterType.Default) && !importer.sRGBTexture && importer.maxTextureSize==2048)continue;
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=false;importer.maxTextureSize=2048;importer.isReadable=false;importer.SaveAndReimport();
        }
        var mats=new Dictionary<string,Material>();
        foreach(var mi in info.materials)
        {
            string path=Root+"/Materials/M"+mi.index.ToString("D3")+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find(mi.procedural?"MakubeX/V003 Preview Weathered":"Standard");
            if(shader==null || !shader.isSupported)throw new Exception("Unsupported preview shader for "+mi.name);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            mat.SetFloat("_Metallic",mi.metallic);mat.SetFloat("_Glossiness",1-mi.roughness);
            if(mi.procedural)
            {
                mat.color=Color.white;mat.SetColor("_Dark",C(mi.dark));mat.SetColor("_Light",C(mi.light));mat.SetVector("_Grain",new Vector4(mi.grain[0],mi.grain[1],mi.grain[2],0));
            }
            else
            {
                mat.color=C(mi.color);
                if(!string.IsNullOrEmpty(mi.base_texture)){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.base_texture);if(mat.mainTexture==null)throw new Exception("Missing base map "+mi.base_texture);mat.color=mi.name=="Moon"?C(mi.color):Color.white;}
                if(!string.IsNullOrEmpty(mi.normal_texture)){mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.normal_texture));mat.EnableKeyword("_NORMALMAP");}
                if(!string.IsNullOrEmpty(mi.unity_metallic_texture)){mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.unity_metallic_texture));mat.SetFloat("_GlossMapScale",1);mat.EnableKeyword("_METALLICGLOSSMAP");}
                if(mi.emission_strength>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",C(mi.emission)*Mathf.Min(mi.emission_strength,4));if(mi.name=="Moon")mat.SetTexture("_EmissionMap",mat.mainTexture);}
            }
            EditorUtility.SetDirty(mat);mats.Add(mi.name,mat);
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Chapter01_V003.fbx");
        if(prefab==null)throw new Exception("Latest FBX was not imported");
        var env=(GameObject)PrefabUtility.InstantiatePrefab(prefab);env.name="Chapter01_V003_Environment";
        var ts=env.GetComponentsInChildren<Transform>();
        origin=ts.First(t=>t.name=="AXIS_Origin").position;
        xaxis=ts.First(t=>t.name=="AXIS_X").position-origin;yaxis=ts.First(t=>t.name=="AXIS_Y").position-origin;zaxis=ts.First(t=>t.name=="AXIS_Z").position-origin;
        var check=new Check{source=info.source,sourceSha256=info.source_sha256,scene=ScenePath,materials=mats.Count};
        foreach(var rr in env.GetComponentsInChildren<Renderer>())
        {
            rr.sharedMaterials=rr.sharedMaterials.Select(m=>{
                string key=m==null?"null":m.name;
                if(mats.TryGetValue(key,out var replacement))return replacement;
                check.materialIssues.Add(rr.name+": "+key);return m;
            }).ToArray();
            rr.shadowCastingMode=rr.name.StartsWith("Stars_")?ShadowCastingMode.Off:ShadowCastingMode.On;
        }
        string[] collisionNames={"Hero_Approach_Bridge","Hero_East_Bridge","Hero_Worn_Floorboards","Hero_Porch_Plates","Hero_Pipe_Railings","Hut_Closed_Front_Wall","Hut_Patchwork_Enclosure","Hut_Wall_Inner_Liners","Hut_Frosted_Windows_And_Frames","Interior_Drawers_Cabinets"};
        foreach(var mf in env.GetComponentsInChildren<MeshFilter>())
        {
            if(collisionNames.Contains(mf.name)){var col=mf.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mf.sharedMesh;}
            if(mf.name.StartsWith("PROP_") && !mf.name.Contains("Streetlamp"))mf.gameObject.AddComponent<BoxCollider>();
        }
        var collisionRoot=new GameObject("Playtest_Floor_Supports");
        AddBox(collisionRoot.transform,"Platform support",P(0,1.05f,-.16f),new Vector3(12.4f,.32f,8.3f),Quaternion.identity);
        Vector3 a=P(-18,-12,-.18f),b=P(-3.95f,-2.8f,-.18f);
        AddBox(collisionRoot.transform,"Bridge seam support",(a+b)*.5f,new Vector3(2.42f,.36f,Vector3.Distance(a,b)+.2f),Quaternion.LookRotation(b-a,Vector3.up));
        AddBox(collisionRoot.transform,"East bridge support",P(11.1f,-.6f,-.18f),new Vector3(9.9f,.36f,2.4f),Quaternion.identity);
        AddBox(collisionRoot.transform,"East end safety",P(16,-.6f,.7f),new Vector3(.16f,1.4f,2.4f),Quaternion.identity);
        var hinge=ts.First(t=>t.name=="DOOR_Hinge_Open_Close");var openPose=ts.First(t=>t.name=="DOOR_OpenPose");
        var doorMesh=env.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="Hut_Operable_Door");doorMesh.gameObject.AddComponent<BoxCollider>();
        var doorBody=hinge.gameObject.AddComponent<Rigidbody>();doorBody.isKinematic=true;doorBody.useGravity=false;
        foreach(var li in info.lights)
        {
            var light=new GameObject(li.name,typeof(Light)).GetComponent<Light>();light.color=C(li.color);light.transform.position=P(li.position);light.renderMode=LightRenderMode.ForcePixel;
            if(li.type=="SUN") {light.type=LightType.Directional;light.intensity=.85f;light.transform.rotation=Quaternion.LookRotation(D(li.direction),Vector3.up);light.shadows=LightShadows.Hard;}
            else if(li.type=="AREA") {light.type=LightType.Directional;light.intensity=.22f;light.transform.rotation=Quaternion.LookRotation(D(li.direction),Vector3.up);}
            else {light.type=LightType.Point;light.range=li.name.Contains("Interior")?9f:8f;light.intensity=Mathf.Clamp(li.energy/80f,.5f,4f);light.shadows=li.name.Contains("Interior")?LightShadows.Hard:LightShadows.None;}
        }
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.16f,.20f,.31f);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.035f,.065f,.15f);RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.0017f;
        var player=new GameObject("V003_Player",typeof(CharacterController),typeof(Chapter01WalkController));
        var body=player.GetComponent<CharacterController>();body.height=1.8f;body.radius=.28f;body.center=new Vector3(0,.9f,0);body.stepOffset=.3f;body.slopeLimit=45;body.skinWidth=.03f;body.minMoveDistance=0;
        var control=player.GetComponent<Chapter01WalkController>();control.spawn=P(-17,-11.345f,.15f);player.transform.position=control.spawn;control.Face(P(-4,-2.8f,0)-control.spawn);
        var eye=new GameObject("V003_PlayerCamera",typeof(Camera),typeof(AudioListener));eye.tag="MainCamera";eye.transform.SetParent(player.transform,false);eye.transform.localPosition=new Vector3(0,1.62f,0);
        control.view=eye.GetComponent<Camera>();control.view.fieldOfView=68;control.view.nearClipPlane=.05f;control.view.farClipPlane=750;control.view.backgroundColor=new Color(.012f,.028f,.075f);control.view.clearFlags=CameraClearFlags.SolidColor;
        var door=hinge.gameObject.AddComponent<V003Door>();door.hinge=hinge;door.openPose=openPose;door.player=control;
        check.route=new List<Vector3>{P(-17,-11.345f,.15f),P(-4,-2.8f,.15f),P(-.3f,-.85f,.15f),P(-.3f,.9f,.2f),P(.3f,1.5f,.2f)};
        Physics.SyncTransforms();
        // Check the actual moving door collider in both authored poses, then restore the closed pose.
        var originalRotation=hinge.rotation;
        check.doorBlockedWhenClosed=DoorRay(doorMesh.gameObject,P(-.3f,-1,1.4f),P(-.3f,1,1.4f));
        hinge.rotation=openPose.rotation;Physics.SyncTransforms();
        check.doorClearWhenOpen=!DoorRay(doorMesh.gameObject,P(-.3f,-1,1.4f),P(-.3f,1,1.4f));
        QuickWalk(control,check);
        hinge.rotation=originalRotation;body.enabled=false;player.transform.position=control.spawn;body.enabled=true;control.Face(check.route[1]-control.spawn);Physics.SyncTransforms();
        check.renderers=env.GetComponentsInChildren<Renderer>().Length;check.environmentColliders=env.GetComponentsInChildren<Collider>().Length+collisionRoot.GetComponentsInChildren<Collider>().Length;
        check.status=check.materialIssues.Count==0 && check.doorBlockedWhenClosed && check.doorClearWhenOpen && check.walkCheck=="passed"?"passed":"needs_review";
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Evidence,"integration.json"),JsonUtility.ToJson(check,true));
        Capture(control.view,Path.Combine(Evidence,"spawn.png"));
        Debug.Log("V003_PLAYTEST_READY "+check.status+" "+ScenePath);
    }
    static bool DoorRay(GameObject door,Vector3 a,Vector3 b)
    {
        return Physics.RaycastAll(a,(b-a).normalized,Vector3.Distance(a,b)).Any(hit=>hit.collider.gameObject==door);
    }
    static void AddBox(Transform parent,string name,Vector3 pos,Vector3 size,Quaternion rotation)
    {var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(parent);go.transform.SetPositionAndRotation(pos,rotation);go.GetComponent<BoxCollider>().size=size;}
    static void QuickWalk(Chapter01WalkController player,Check result)
    {
        result.walkCheck="passed";
        for(int i=0;i<45;i++)player.Step(Vector2.zero,false,.02f);
        for(int i=1;i<result.route.Count;i++)
        {
            int budget=2000;
            while(FlatDistance(player.transform.position,result.route[i])>.1f && budget-->0)
            {
                player.Face(result.route[i]-player.transform.position);player.Step(Vector2.up,false,.02f);Physics.SyncTransforms();
                if(budget%10==0)result.trajectory.Add(player.transform.position);
                if(player.transform.position.y<-.5f)break;
            }
            if(budget<=0 || player.transform.position.y<-.5f){result.walkCheck="failed";result.error="CharacterController edit-mode walk failed at waypoint "+i+" at "+player.transform.position;return;}
            result.completedWaypoints++;
        }
        Capture(player.view,Path.Combine(Evidence,"inside-editmode-check.png"));
    }
    static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    static void Capture(Camera cam,string path)
    {
        var target=cam.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try {cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally {cam.targetTexture=target;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    [MenuItem("MakubeX/V003/Open Latest Playtest")]
    public static void Open()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
}
#endif
