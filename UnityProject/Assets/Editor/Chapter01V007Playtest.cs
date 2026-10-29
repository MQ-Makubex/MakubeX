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
using MakubeX.V004;
using MakubeX.V005;
using MakubeX.V006;
using System.Security.Cryptography;

public static class Chapter01V007Playtest
{
    const string Root="Assets/Chapter01V007_Playtest";
    const int ExteriorLayer=28,DistantLayer=29;
    public const string ScenePath=Root+"/Chapter01_V007_Playtest.unity";
    static string Evidence=>Path.GetFullPath("../Verification/Chapter01_v007_20261027");
    [Serializable] public class Mat
    {
        public string name,base_texture,normal_texture,unity_metallic_texture,emission_texture;
        public int index;
        public float[] color,emission,dark,light,grain;
        public float metallic,roughness,emission_strength,normal_strength;public bool world_windows;
        public bool procedural;
    }
    [Serializable] public class Lamp { public string name,type; public float[] position,direction,color; public float energy,area_size,area_size_y,radius,spot_size,spot_blend; }
    [Serializable] public class LightMapping {public string sourceName,sourceType,unityType,strategy;public float sourceEnergy,intensity,range;public int cullingMask;public bool realtimeShadows;}
    [Serializable] public class ExportChecks {public string[] visible_tower_instances;}
    [Serializable] public class SourceMesh {public string name,shared_mesh;public string[] collections,material_names;}
    [Serializable] public class Manifest { public string source,source_sha256; public Mat[] materials; public Lamp[] lights; public ExportChecks export_checks; public SourceMesh[] meshes; }
    [Serializable] public class Check
    {
        public string source,sourceSha256,scene,status,walkCheck,error;
        public int renderers,materials,environmentColliders,completedWaypoints;
        public float lookSensitivity;
        public bool doorBlockedWhenClosed,doorClearWhenOpen;
        public bool hiddenMasterExcluded,allVisibleTowerInstancesRetained;
        public List<LightMapping> lightMappings=new List<LightMapping>();
        public List<Vector3> route=new List<Vector3>(),trajectory=new List<Vector3>();
        public List<string> materialIssues=new List<string>();
    }
    static Color C(float[] c)=>new Color(c[0],c[1],c[2],c.Length>3?c[3]:1);
    static Color Albedo(float[] c){var value=C(c);return QualitySettings.activeColorSpace==ColorSpace.Gamma?value.gamma:value;}
    static Vector3 origin,xaxis,yaxis,zaxis;
    static Vector3 P(float x,float y,float z)=>origin+xaxis*x+yaxis*y+zaxis*z;
    static Vector3 P(float[] p)=>P(p[0],p[1],p[2]);
    static Vector3 D(float[] p)=>xaxis*p[0]+yaxis*p[1]+zaxis*p[2];

    [MenuItem("MakubeX/V007/Build Latest Playtest")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before building.");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve unsaved scene before v005 integration");
        Directory.CreateDirectory(Evidence);
        try { BuildInternal();IntegrateStory(); }
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
            bool simpleEmitter=mi.emission_strength>0 && string.IsNullOrEmpty(mi.base_texture) && string.IsNullOrEmpty(mi.normal_texture);
            var shader=Shader.Find(mi.world_windows?"MakubeX/V005 World Window Rhythm":simpleEmitter?"MakubeX/V004 Two Sided Emission":mi.procedural?"MakubeX/V005 Weathered Palette":"Standard");
            if(shader==null || !shader.isSupported)throw new Exception("Unsupported preview shader for "+mi.name);
            if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
            if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",mi.metallic);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",1-mi.roughness);
            if(mi.world_windows){mat.SetFloat("_Strength",mi.emission_strength);mat.color=C(mi.color);}
            else if(mi.procedural)
            {
                mat.color=Color.white;mat.SetColor("_Dark",Albedo(mi.dark));mat.SetColor("_Light",Albedo(mi.light));mat.SetVector("_Grain",new Vector4(mi.grain[0],mi.grain[1],mi.grain[2],0));
            }
            else
            {
                mat.color=Albedo(mi.color);mat.mainTexture=null;mat.DisableKeyword("_EMISSION");mat.SetColor("_EmissionColor",Color.black);mat.SetTexture("_EmissionMap",null);
                if(!string.IsNullOrEmpty(mi.base_texture)){mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.base_texture);if(mat.mainTexture==null)throw new Exception("Missing base map "+mi.base_texture);mat.color=mi.name=="Moon"?C(mi.color):Color.white;}
                if(!string.IsNullOrEmpty(mi.normal_texture)){mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.normal_texture));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",mi.normal_strength);}
                if(!string.IsNullOrEmpty(mi.unity_metallic_texture)){mat.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.unity_metallic_texture));mat.SetFloat("_GlossMapScale",1);mat.EnableKeyword("_METALLICGLOSSMAP");}
                if(mi.emission_strength>0){if(!simpleEmitter)mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",C(mi.emission)*mi.emission_strength);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;if(!string.IsNullOrEmpty(mi.emission_texture))mat.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+mi.emission_texture));}
            }
            EditorUtility.SetDirty(mat);mats.Add(mi.name,mat);
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Chapter01_V007.fbx");
        if(prefab==null)throw new Exception("Latest FBX was not imported");
        var env=(GameObject)PrefabUtility.InstantiatePrefab(prefab);env.name="Chapter01_V007_Environment";
        PrefabUtility.UnpackPrefabInstance(env,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        PersistMeshes(env);
        var ts=env.GetComponentsInChildren<Transform>();
        origin=ts.First(t=>t.name=="AXIS_Origin").position;
        xaxis=ts.First(t=>t.name=="AXIS_X").position-origin;yaxis=ts.First(t=>t.name=="AXIS_Y").position-origin;zaxis=ts.First(t=>t.name=="AXIS_Z").position-origin;
        var check=new Check{source=info.source,sourceSha256=info.source_sha256,scene=ScenePath,materials=mats.Count};
        var distantMaterials=new Dictionary<string,Material>();var rightMaterials=new Dictionary<string,Material>();
        check.hiddenMasterExcluded=!env.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.name=="LatticeTower_Master");
        if(!check.hiddenMasterExcluded)throw new Exception("Hidden LatticeTower_Master still present in FBX");
        var importedNames=new HashSet<string>(env.GetComponentsInChildren<Transform>(true).Select(t=>t.name));
        check.allVisibleTowerInstancesRetained=info.export_checks!=null && info.export_checks.visible_tower_instances.All(importedNames.Contains);
        if(!check.allVisibleTowerInstancesRetained)throw new Exception("Visible source tower instances were lost during export/import");
        foreach(var rr in env.GetComponentsInChildren<Renderer>())
        {
            var sourceMesh=info.meshes.FirstOrDefault(m=>m.name==rr.name);
            bool rightCity=rr.name.StartsWith("V006_City_Right_");
            bool distant=sourceMesh!=null && sourceMesh.collections.Any(c=>c=="01_UpperCity" || c=="02_LowerCity");
            bool exterior=sourceMesh!=null && (sourceMesh.collections.Contains("03_Bridges") || sourceMesh.collections.Contains("04b_Roof") || rr.name.StartsWith("Hero_") || rr.name.StartsWith("V004_Pale_") || rr.name=="V004_Black_Iron_Frame" || rr.name.StartsWith("V004_Continuous_") || rr.name.StartsWith("V004_Wiring_") || rr.name.StartsWith("V004_Porch_"));
            rr.gameObject.layer=distant?DistantLayer:exterior?ExteriorLayer:0;
            rr.sharedMaterials=rr.sharedMaterials.Select(m=>{
                string key=m==null?"null":m.name;
                if(mats.TryGetValue(key,out var replacement))
                {
                    var materialInfo=info.materials.First(mi=>mi.name==key);
                    if(rightCity && (materialInfo.world_windows || (materialInfo.emission_strength<=0 && string.IsNullOrEmpty(materialInfo.base_texture))))
                    {
                        if(!rightMaterials.TryGetValue(key,out var mistMaterial)){
                            string path=Root+"/Materials/Right_M"+materialInfo.index.ToString("D3")+".mat";
                            mistMaterial=AssetDatabase.LoadAssetAtPath<Material>(path);var source=materialInfo.world_windows?replacement:CreateDistantMaterial(materialInfo,info.lights);
                            if(!mistMaterial){mistMaterial=new Material(source);AssetDatabase.CreateAsset(mistMaterial,path);}else mistMaterial.CopyPropertiesFromMaterial(source);
                            mistMaterial.shader=Shader.Find(materialInfo.world_windows?"MakubeX/V006 Misty Windows":"MakubeX/V006 Misty Distant");EditorUtility.SetDirty(mistMaterial);rightMaterials.Add(key,mistMaterial);
                        }return mistMaterial;
                    }
                    if(distant && materialInfo.emission_strength<=0 && string.IsNullOrEmpty(materialInfo.base_texture))
                    {
                        if(!distantMaterials.TryGetValue(key,out var farMaterial))
                        {farMaterial=CreateDistantMaterial(materialInfo,info.lights);distantMaterials.Add(key,farMaterial);}
                        return farMaterial;
                    }
                    return replacement;
                }
                check.materialIssues.Add(rr.name+": "+key);return m;
            }).ToArray();
            if(sourceMesh!=null && sourceMesh.material_names!=null){
                var mesh=rr.GetComponent<MeshFilter>().sharedMesh;var active=new HashSet<string>();
                for(int k=0;k<mesh.subMeshCount && k<rr.sharedMaterials.Length;k++)if(mesh.GetIndexCount(k)>0)active.Add(info.materials.FirstOrDefault(mi=>mats[mi.name]==rr.sharedMaterials[k] || (distantMaterials.ContainsKey(mi.name)&&distantMaterials[mi.name]==rr.sharedMaterials[k]) || (rightMaterials.ContainsKey(mi.name)&&rightMaterials[mi.name]==rr.sharedMaterials[k]))?.name??"");
                foreach(string required in sourceMesh.material_names)if(!active.Contains(required))check.materialIssues.Add(rr.name+": missing material faces "+required);
            }
            // Opaque decorative lamp shells otherwise shadow their own enclosed
            // point source. Room furniture/walls retain their real shadow casters.
            bool fixture=rr.name=="Interior_Single_Ceiling_Lantern" || rr.name=="PROP_TableLantern" || rr.name.StartsWith("PROP_Streetlamp");
            rr.shadowCastingMode=rr.name.StartsWith("Stars_") || fixture?ShadowCastingMode.Off:ShadowCastingMode.On;
        }
        string[] collisionNames={"Hero_Approach_Bridge","Hero_East_Bridge","V004_Expanded_Floor","Hero_Porch_Plates","V004_Corner_Landing","V004_Continuous_Porch_And_Corridor_Rails","V004_Pale_Riveted_Panels","V004_Interior_Wall_Liners","V004_Interior_Boards_And_Exposed_Pipes","V004_Interior_Window_Insets","V004_Black_Iron_Frame","PROP_Tripo_DoorFrame","V004_Porch_Utility_Cabinets","Interior_Drawers_Cabinets"};
        foreach(var mf in env.GetComponentsInChildren<MeshFilter>())
        {
            if(collisionNames.Contains(mf.name)){var col=mf.gameObject.AddComponent<MeshCollider>();col.sharedMesh=mf.sharedMesh;}
            if(mf.name.StartsWith("PROP_") && !mf.name.Contains("Streetlamp") && !mf.name.StartsWith("PROP_Tripo_Door"))mf.gameObject.AddComponent<BoxCollider>();
        }
        var collisionRoot=new GameObject("Playtest_Floor_Supports");
        AddBox(collisionRoot.transform,"Platform support",P(0,1.35f,-.16f),new Vector3(14f,.32f,8.9f),Quaternion.identity);
        Vector3 a=P(-18,-12,-.18f),b=P(-2.8f,-2.05f,-.18f);
        AddBox(collisionRoot.transform,"Bridge seam support",(a+b)*.5f,new Vector3(2.42f,.36f,Vector3.Distance(a,b)+.2f),Quaternion.LookRotation(b-a,Vector3.up));
        AddBox(collisionRoot.transform,"East bridge support",P(11.5f,-.6f,-.18f),new Vector3(9.1f,.36f,2.4f),Quaternion.identity);
        AddBox(collisionRoot.transform,"East end safety",P(16,-.6f,.7f),new Vector3(.16f,1.4f,2.4f),Quaternion.identity);
        var hinge=ts.First(t=>t.name=="DOOR_Hinge_Open_Close");var openPose=ts.First(t=>t.name=="DOOR_OpenPose");
        var doorMesh=env.GetComponentsInChildren<MeshFilter>().First(m=>m.name=="PROP_Tripo_DoorLeaf");doorMesh.gameObject.AddComponent<BoxCollider>();
        var doorBody=hinge.gameObject.AddComponent<Rigidbody>();doorBody.isKinematic=true;doorBody.useGravity=false;
        foreach(var li in info.lights)
        {
            if(li.energy>0)check.lightMappings.Add(CreateLight(li));else check.lightMappings.Add(new LightMapping{sourceName=li.name,sourceType=li.type,sourceEnergy=0,strategy="Intentionally disabled by v005 art direction"});
        }
        ConfigureSky();new GameObject("V005 authored lighting").AddComponent<V005Lighting>().Apply();
        var player=new GameObject("V004_Player",typeof(CharacterController),typeof(Chapter01WalkController));
        var body=player.GetComponent<CharacterController>();body.height=1.8f;body.radius=.28f;body.center=new Vector3(0,.9f,0);body.stepOffset=.3f;body.slopeLimit=45;body.skinWidth=.03f;body.minMoveDistance=0;
        var control=player.GetComponent<Chapter01WalkController>();control.lookSensitivity=3.5f;control.spawn=P(-17,-11.345f,.15f);player.transform.position=control.spawn;control.Face(P(-3,-2.2f,0)-control.spawn);
        var eye=new GameObject("V004_PlayerCamera",typeof(Camera),typeof(AudioListener));eye.tag="MainCamera";eye.transform.SetParent(player.transform,false);eye.transform.localPosition=new Vector3(0,1.62f,0);
        control.view=eye.GetComponent<Camera>();control.view.fieldOfView=68;control.view.nearClipPlane=.05f;control.view.farClipPlane=750;control.view.backgroundColor=new Color(.012f,.028f,.075f);control.view.clearFlags=CameraClearFlags.Skybox;control.view.allowHDR=true;
        var post=eye.AddComponent<V006NightPost>();string postPath=Root+"/Materials/V006NightPost.mat";var postMaterial=AssetDatabase.LoadAssetAtPath<Material>(postPath);if(postMaterial==null){postMaterial=new Material(Shader.Find("Hidden/MakubeX/V006NightPost"));AssetDatabase.CreateAsset(postMaterial,postPath);}post.filterMaterial=postMaterial;
        var door=hinge.gameObject.AddComponent<V004Door>();door.hinge=hinge;door.openPose=openPose;door.player=control;
        check.route=new List<Vector3>{P(-17,-11.345f,.15f),P(-3,-2.2f,.15f),P(0,-1.2f,.15f),P(0,.9f,.2f),P(.3f,1.8f,.2f)};
        Physics.SyncTransforms();
        // Check the actual moving door collider in both authored poses, then restore the closed pose.
        var originalRotation=hinge.rotation;
        check.doorBlockedWhenClosed=DoorRay(doorMesh.gameObject,P(0,-1,1.4f),P(0,1,1.4f));
        hinge.rotation=openPose.rotation;Physics.SyncTransforms();
        check.doorClearWhenOpen=!DoorRay(doorMesh.gameObject,P(0,-1,1.4f),P(0,1,1.4f));
        QuickWalk(control,check,hinge,originalRotation);
        hinge.rotation=originalRotation;body.enabled=false;player.transform.position=control.spawn;body.enabled=true;control.Face(check.route[1]-control.spawn);Physics.SyncTransforms();
        check.lookSensitivity=control.lookSensitivity;check.renderers=env.GetComponentsInChildren<Renderer>().Length;check.environmentColliders=env.GetComponentsInChildren<Collider>().Length+collisionRoot.GetComponentsInChildren<Collider>().Length;
        check.status=check.materialIssues.Count==0 && check.doorBlockedWhenClosed && check.doorClearWhenOpen && check.walkCheck=="passed"?"passed":"needs_review";
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        File.WriteAllText(Path.Combine(Evidence,"integration.json"),JsonUtility.ToJson(check,true));
        Capture(control.view,Path.Combine(Evidence,"spawn.png"));
        var savedFacing=control.transform.rotation;var savedLook=control.view.transform.localRotation;
        control.Face(savedFacing*Vector3.right);Capture(control.view,Path.Combine(Evidence,"look-right.png"));control.view.transform.localRotation=Quaternion.Euler(-12,0,0);Capture(control.view,Path.Combine(Evidence,"look-right-sky.png"));
        control.transform.rotation=savedFacing;control.Face(savedFacing*Vector3.back);Capture(control.view,Path.Combine(Evidence,"look-back.png"));
        control.transform.rotation=savedFacing;control.view.transform.localRotation=savedLook;
        Debug.Log("V007_PLAYTEST_READY "+check.status+" "+ScenePath);
    }
    static LightMapping CreateLight(Lamp li)
    {
        var light=new GameObject(li.name,typeof(Light)).GetComponent<Light>();light.color=C(li.color);light.transform.position=P(li.position);light.renderMode=LightRenderMode.ForcePixel;light.shadows=LightShadows.None;
        if(li.type!="POINT")light.transform.rotation=Quaternion.LookRotation(D(li.direction),Vector3.up);
        string strategy;
        if(li.type=="SUN")
        {light.type=LightType.Directional;light.intensity=li.energy*.425f;light.cullingMask=(1<<ExteriorLayer)|(1<<DistantLayer);light.shadows=LightShadows.Hard;strategy="Moon directional key; only distant key with realtime shadows";}
        else if(li.type=="AREA" && li.area_size>=50)
        {
            float irradianceProxy=li.energy/Mathf.Max(1,li.area_size*li.area_size);
            light.type=LightType.Directional;light.intensity=.06f*Mathf.Sqrt(irradianceProxy);
            light.cullingMask=1<<DistantLayer;
            strategy="Broad AREA approximated by low-intensity shadowless blue directional fill; original direction/color retained";
        }
        else if(li.type=="AREA" && li.name.Contains("Interior"))
        {light.type=LightType.Spot;light.spotAngle=150;light.innerSpotAngle=110;light.range=9;light.intensity=li.energy/70f;light.shadows=LightShadows.Hard;strategy="Indoor AREA approximated by broad downward spot, preserving source direction and warm color";}
        else if(li.type=="AREA")
        {
            float irradianceProxy=li.energy/Mathf.Max(1,li.area_size*li.area_size);
            light.type=LightType.Spot;light.spotAngle=115;light.innerSpotAngle=65;light.range=li.area_size>=30?110:70;
            light.intensity=li.area_size>=30?.9f:Mathf.Sqrt(irradianceProxy)*.65f;
            light.renderMode=LightRenderMode.Auto;strategy="Finite shadowless AREA approximation, energy normalized by area; upper warm bounce stays local";
            light.cullingMask=li.area_size>=30?1<<DistantLayer:1<<ExteriorLayer;
            if(li.area_size>=30)
            {light.type=LightType.Point;light.renderMode=LightRenderMode.ForceVertex;strategy="Upper warm AREA bounce approximated by bounded shadowless vertex point, keeping warm fill in the single-pass distant shader";}
        }
        else if(li.type=="POINT")
        {
            light.type=LightType.Point;
            if(li.radius>=2)
            {light.range=35;light.intensity=1.2f;light.renderMode=LightRenderMode.ForceVertex;light.cullingMask=1<<DistantLayer;strategy="Cloud warmth baked into HDR cloud sky plus cheap shadowless vertex point on nearby city geometry";}
            else
            {light.range=li.name.Contains("Interior")?9f:li.name.Contains("Corridor")?4f:5.5f;light.intensity=Mathf.Clamp(li.energy/80f,.125f,4f);light.shadows=li.name.Contains("Interior")?LightShadows.Hard:LightShadows.None;strategy="Near-field fixture; calibrated point intensity/range, shadows only on the two interior fixtures";}
        }
        else if(li.type=="SPOT")
        {light.type=LightType.Spot;light.spotAngle=li.spot_size*Mathf.Rad2Deg;light.innerSpotAngle=light.spotAngle*(1-li.spot_blend);light.range=15;light.intensity=Mathf.Sqrt(Mathf.Max(0,li.energy))/10;strategy="Source spot with preserved cone, bounded shadowless realtime range";}
        else throw new Exception("Unmapped source light type "+li.type+": "+li.name);
        return new LightMapping{sourceName=li.name,sourceType=li.type,sourceEnergy=li.energy,unityType=light.type.ToString(),intensity=light.intensity,range=light.range,cullingMask=light.cullingMask,realtimeShadows=light.shadows!=LightShadows.None,strategy=strategy};
    }
    static Material CreateDistantMaterial(Mat source,Lamp[] lights)
    {
        string path=Root+"/Materials/Distant_M"+source.index.ToString("D3")+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);var shader=Shader.Find("MakubeX/V005 Distant Palette");
        if(shader==null || !shader.isSupported)throw new Exception("Distant vertex shader unsupported");
        if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
        material.color=C(source.color);material.SetFloat("_Metallic",source.metallic);material.SetFloat("_Glossiness",1-source.roughness);
        EditorUtility.SetDirty(material);return material;
    }
    static void ConfigureSky()
    {
        string path=Root+"/Materials/V005Sky.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("MakubeX/V005 Night Gradient"));AssetDatabase.CreateAsset(material,path);}RenderSettings.skybox=material;
    }
    static void PersistMeshes(GameObject env)
    {
        Directory.CreateDirectory(Root+"/Meshes");var unique=new Dictionary<string,Mesh>();int filters=0;
        foreach(var mf in env.GetComponentsInChildren<MeshFilter>()){
            var mesh=mf.sharedMesh;string hash;
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                w.Write(mesh.vertexCount);foreach(var v in mesh.vertices){w.Write(v.x);w.Write(v.y);w.Write(v.z);}foreach(var v in mesh.normals){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                foreach(var v in mesh.tangents){w.Write(v.x);w.Write(v.y);w.Write(v.z);w.Write(v.w);}foreach(var v in mesh.uv){w.Write(v.x);w.Write(v.y);}
                w.Write(mesh.subMeshCount);for(int i=0;i<mesh.subMeshCount;i++){w.Write((int)mesh.GetTopology(i));foreach(int index in mesh.GetIndices(i))w.Write(index);}
                hash=BitConverter.ToString(SHA256.Create().ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
            }
            if(!unique.TryGetValue(hash,out var target)){
                string path=Root+"/Meshes/"+hash+".asset";target=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!target){target=UnityEngine.Object.Instantiate(mesh);target.name="V005_"+hash.Substring(0,12);AssetDatabase.CreateAsset(target,path);}unique.Add(hash,target);
            }
            mf.sharedMesh=target;filters++;
        }
        File.WriteAllText(Path.Combine(Evidence,"mesh_reuse.json"),"{\"renderers\":"+filters+",\"unique_geometry\":"+unique.Count+"}");
    }
    public const string StoryScene="Assets/Scenes/Chapter01_V007_Story.unity",EntryScene="Assets/Scenes/Entry_V007.unity";
    static void IntegrateStory()
    {
        var env=GameObject.Find("Chapter01_V007_Environment");var ts=env.GetComponentsInChildren<Transform>(true);Func<string,Transform> find=name=>ts.First(t=>t.name==name);
        var walker=UnityEngine.Object.FindAnyObjectByType<Chapter01WalkController>();var door=UnityEngine.Object.FindAnyObjectByType<V004Door>();
        var story=new GameObject("Chapter 01 v005 story integration").AddComponent<MakubeX.Chapter01V004Story>();story.walker=walker;story.door=door;story.useAuthoredLighting=true;walker.showHelp=false;door.showHelp=false;
        story.bag=find("PROP_OldBag");story.chair=find("PROP_Chair");story.computer=find("PROP_CRT");
        story.seat=P(-2.15f,2.5f,.17f);story.elderStart=P(-3.8f,1.75f,.17f);story.elderBesideChair=P(-3.05f,2.22f,.17f);story.bagCamera=P(1,2.6f,2.2f);story.roomCamera=P(.35f,-.08f,2.05f);story.bootCamera=P(-.1f,1,2.2f);
        string path=Root+"/Materials/StoryPoweredCRT.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);var source=story.computer.GetComponent<Renderer>().sharedMaterial;
        if(!material){material=new Material(source);AssetDatabase.CreateAsset(material,path);}else material.CopyPropertiesFromMaterial(source);
        material.shader=Shader.Find("MakubeX/V004 Story Powered CRT");material.DisableKeyword("_EMISSION");material.SetFloat("_Powered",0);story.poweredScreenTemplate=material;EditorUtility.SetDirty(material);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),StoryScene);AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(MakubeX.Editor.ProjectSetup.EntryScene);var app=UnityEngine.Object.FindAnyObjectByType<MakubeX.MakubeXApp>();var serialized=new SerializedObject(app);serialized.FindProperty("chapterSceneNameOverride").stringValue="Chapter01_V007_Story";serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),EntryScene);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(EntryScene,true),new EditorBuildSettingsScene(StoryScene,true)}.Concat(EditorBuildSettings.scenes.Where(v=>v.path!=EntryScene&&v.path!=StoryScene)).ToArray();
        Debug.Log("V007_STORY_INTEGRATED "+StoryScene);
    }
    [MenuItem("MakubeX/V007/Build Local WebGL")]
    public static void BuildWeb(){WebEntryBuild.BuildAt("Builds/WebGL_ArtUpgradeV007_20261027","../Verification/Chapter01_v007_20261027",EntryScene,StoryScene);var index=Path.GetFullPath("Builds/WebGL_ArtUpgradeV007_20261027/index.html");File.WriteAllText(index,File.ReadAllText(index).Replace("<title>MakubeX · 第一章《名字》试玩</title>","<title>MakubeX · v007 美术升级本地试玩</title>"));}
    static bool DoorRay(GameObject door,Vector3 a,Vector3 b)
    {
        return Physics.RaycastAll(a,(b-a).normalized,Vector3.Distance(a,b)).Any(hit=>hit.collider.gameObject==door);
    }
    static void AddBox(Transform parent,string name,Vector3 pos,Vector3 size,Quaternion rotation)
    {var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(parent);go.transform.SetPositionAndRotation(pos,rotation);go.GetComponent<BoxCollider>().size=size;}
    static void QuickWalk(Chapter01WalkController player,Check result,Transform hinge,Quaternion closedRotation)
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
        var localLook=player.view.transform.localRotation;hinge.rotation=closedRotation;Physics.SyncTransforms();
        player.view.transform.rotation=Quaternion.LookRotation(P(0,-.3f,3.8f)-player.view.transform.position,Vector3.up);
        Capture(player.view,Path.Combine(Evidence,"door-gable-inside.png"));player.view.transform.localRotation=localLook;
    }
    static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
    static void Capture(Camera cam,string path)
    {
        var target=cam.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
        try {cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally {cam.targetTexture=target;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);}
    }
    [MenuItem("MakubeX/V007/Open Latest Playtest")]
    public static void Open()
    {if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
}
#endif
