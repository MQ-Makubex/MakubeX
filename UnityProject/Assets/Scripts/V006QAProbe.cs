#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using MakubeX.V005;
using MakubeX.Verification;
namespace MakubeX {
 public sealed class V006QAProbe:MonoBehaviour {
  [Serializable]public class Result {
   public string status="running",stage,error,inputMethod="Actual Editor PlayMode; CharacterController.Step, V004Door.Toggle and story.Interact APIs, not physical keyboard";
   public string graphics;public int respawns,renderers,lights,shadowLights,uniqueMeshes;public bool v006Loaded,materialsValid,doorClosedBlocks,enteredRoom,ambienceCrossfade,bag,chair,crtSeparate,headPatTheme,returnedMap,settings,pointerOptIn;public float medianFrameMs,p90FrameMs;
   public List<string> errors=new List<string>(),captures=new List<string>(),cues=new List<string>();
  }
  [Serializable]class Pose{public string name;public float[] position,target,up;public float vertical_fov_degrees;public int frame;}
  [Serializable]class Manifest{public Pose[] poses;}
  public Result result=new Result();public bool Complete{get;private set;}
  string output;MakubeXApp app;Chapter01V004Story story;Exception failure;readonly List<float> frameMs=new List<float>();
  IEnumerator Start(){output=Path.GetFullPath("../Verification/Chapter01_v006_20261027/playmode");Directory.CreateDirectory(output);AudioListener.volume=0;app=GetComponent<MakubeXApp>();Application.logMessageReceived+=Log;
   var work=Run();while(true){bool more=false;try{more=work.MoveNext();}catch(Exception e){failure=e;}if(!more||failure!=null)break;yield return work.Current;}
   Application.logMessageReceived-=Log;result.error=failure?.ToString();result.status=failure==null&&result.errors.Count==0?"passed":"failed";Save();Complete=true;
  }
  IEnumerator Run(){
   Stage("load-map");float deadline=Time.realtimeSinceStartup+60;while(app.Busy){Check(Time.realtimeSinceStartup<deadline,"Entry timeout");yield return null;}
   app.StartFromWeb();while(app.CurrentPage!="memory-map"||app.Busy){Check(Time.realtimeSinceStartup<deadline,"Map timeout");yield return null;}deadline=Time.realtimeSinceStartup+90;app.ActivateChapter(0);
   while(app.CurrentPage!="game"){Check(Time.realtimeSinceStartup<deadline,"Chapter timeout");yield return null;}
   story=FindAnyObjectByType<Chapter01V004Story>();var profile=FindAnyObjectByType<V005Lighting>();while(!profile.Applied)yield return null;
   result.v006Loaded=app.ActiveChapterSceneName=="Chapter01_V006_Story"&&story.gameObject.scene.name=="Chapter01_V006_Story"&&story.useAuthoredLighting;Check(result.v006Loaded,"Not v006");
   story.verificationMovement=true;story.walker.verificationMode=true;result.graphics=SystemInfo.graphicsDeviceName;result.pointerOptIn=story.walker.clickToLook;
   var renderers=story.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>()).ToArray();result.renderers=renderers.Length;
   result.materialsValid=renderers.All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported));Check(result.materialsValid,"Unsupported or missing material");
   result.lights=FindObjectsByType<Light>().Count(l=>l.gameObject.scene==story.gameObject.scene);result.shadowLights=FindObjectsByType<Light>().Count(l=>l.gameObject.scene==story.gameObject.scene&&l.shadows!=LightShadows.None);
   result.uniqueMeshes=renderers.Select(r=>r.GetComponent<MeshFilter>()?.sharedMesh).Where(m=>m).Distinct().Count();
   app.Audio.CueObserved+=ObserveCue;Stage("render-authored-poses");Capture("bridge-gameplay");
   var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Chapter01V006_Playtest/SceneManifest.json"));
   var closed=story.door.hinge.rotation;
   foreach(var pose in manifest.poses){story.door.hinge.rotation=pose.frame==100?story.door.openPose.rotation:closed;var crtMaterial=story.computer.GetComponent<Renderer>().sharedMaterial;crtMaterial.SetFloat("_Powered",pose.frame==100?1:0);CapturePose(pose);crtMaterial.SetFloat("_Powered",0);yield return null;}
   story.door.hinge.rotation=closed;Physics.SyncTransforms();
   Stage("frame-sample");for(int i=0;i<35;i++){yield return null;if(i>=5)frameMs.Add(Time.unscaledDeltaTime*1000);}frameMs.Sort();result.medianFrameMs=frameMs[frameMs.Count/2];result.p90FrameMs=frameMs[(int)(frameMs.Count*.9f)];
   Stage("settings");app.BrowserEscapePressed();Check(app.IsPaused,"ESC settings missing");
   var before=story.walker.transform.position;story.walker.Step(Vector2.up,false,.05f);bool old=story.door.IsOpen;story.door.Toggle();
   var ui=app.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Settings");var text=ui.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();
   result.settings=story.walker.transform.position==before&&story.door.IsOpen==old&&text.Count(t=>t=="设置")==1&&ui.GetComponent<Image>().color.a<.3f;Check(result.settings,"Pause regression");Capture("transparent-settings");app.CloseSettings();
   Stage("bridge-walk");yield return Walk(new Vector3(3,.15f,2.2f));yield return Walk(new Vector3(0,.15f,1.2f));
   story.walker.Face(Vector3.back);bool hit=false;for(int i=0;i<30;i++){story.walker.Step(Vector2.up,false,.025f);Physics.SyncTransforms();hit|=(story.walker.LastFlags&CollisionFlags.Sides)!=0;yield return null;}
   result.doorClosedBlocks=hit&&story.walker.transform.position.z>.5f;Check(result.doorClosedBlocks,"Closed door did not block");story.door.Toggle();
   while(Quaternion.Angle(story.door.hinge.rotation,story.door.openPose.rotation)>1)yield return null;
   Check(!app.Audio.Inside,"Opening door alone counted as entering");Stage("walk-inside");yield return Walk(new Vector3(0,.17f,-.9f));yield return Walk(story.BagApproach);result.enteredRoom=app.Audio.Inside;Check(result.enteredRoom,"Player did not enter");
   yield return new WaitForSeconds(3.7f);result.ambienceCrossfade=app.Audio.InteriorBlend>.99f;Check(result.ambienceCrossfade,"Indoor blend missing");
   Check(!result.cues.Contains("GrandpaHeadPatStarted"),"Theme started before headpat");Stage("bag-interact-api");story.Interact();deadline=Time.realtimeSinceStartup+30;while(story.Part<1){Check(Time.realtimeSinceStartup<deadline,"Bag timeout");yield return null;}result.bag=result.cues.Contains("BagInspected");
   yield return Walk(new Vector3(0,.17f,-1.1f));yield return Walk(story.ChairApproach);Stage("chair-interact-api");story.Interact();deadline=Time.realtimeSinceStartup+30;
   while(story.Phase!="await-boot"){Check(Time.realtimeSinceStartup<deadline,"Chair timeout");yield return null;}result.chair=result.cues.Contains("PlayerSatDown");yield return new WaitForSeconds(1);
   result.crtSeparate=!story.ComputerOn&&!story.ScreenLit;Check(result.crtSeparate,"CRT auto started after chair");Check(!result.cues.Contains("GrandpaHeadPatStarted"),"Theme started before third interaction");
   Capture("seated-screen-off");Stage("crt-interact-api");story.Interact();deadline=Time.realtimeSinceStartup+35;
   while(app.CurrentPage=="game"){Check(Time.realtimeSinceStartup<deadline,"Ending timeout");if(story&&story.ScreenLit&&!result.captures.Any(p=>p.EndsWith("crt-powered.png")))Capture("crt-powered");if(story)result.respawns=story.walker.Respawns;yield return null;}
   result.returnedMap=app.CurrentPage=="memory-map";Check(result.bag&&result.chair&&result.returnedMap&&result.headPatTheme&&result.respawns==0,"Story or music regression");Capture("returned-map");app.Audio.CueObserved-=ObserveCue;
  }
  void ObserveCue(MakubeXAudio.Trace cue){result.cues.Add(cue.cue);if(cue.cue=="GrandpaHeadPatStarted"){result.headPatTheme=story.HeadPatSoundTriggered&&cue.clip==app.Audio.Catalog.homeTheme.name;}}
  IEnumerator Walk(Vector3 target){float deadline=Time.realtimeSinceStartup+120;while(Flat(story.walker.transform.position,target)>.11f){Check(Time.realtimeSinceStartup<deadline,"Walk blocked at "+target+" from "+story.walker.transform.position);story.walker.Face(target-story.walker.transform.position);story.walker.Step(Vector2.up,false,.04f);Physics.SyncTransforms();Check(story.walker.transform.position.y>-.3f,"Fell from route");yield return null;}}
  static float Flat(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
  static Vector3 P(float[] a)=>new Vector3(-a[0],a[2],-a[1]);
  void CapturePose(Pose pose){
   var go=Instantiate(story.walker.view.gameObject);go.name="QA pose "+pose.name;var listener=go.GetComponent<AudioListener>();if(listener)DestroyImmediate(listener);var cam=go.GetComponent<Camera>();cam.enabled=false;cam.transform.SetPositionAndRotation(P(pose.position),Quaternion.LookRotation(P(pose.target)-P(pose.position),P(pose.up)));cam.fieldOfView=pose.vertical_fov_degrees;cam.aspect=16f/9;
   var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf){antiAliasing=2};var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;
   try{cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();string path=Path.Combine(output,pose.name+".png");File.WriteAllBytes(path,tex.EncodeToPNG());result.captures.Add(path);}finally{cam.targetTexture=null;RenderTexture.active=previous;rt.Release();DestroyImmediate(tex);DestroyImmediate(rt);DestroyImmediate(go);}
  }
  void Capture(string name){string path=Path.Combine(output,name+".png");typeof(VerificationCaptureBridge).GetMethod("CaptureOffscreen",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{path});result.captures.Add(path);}
  void Stage(string name){result.stage=name;Save();}void Save(){File.WriteAllText(Path.Combine(output,"results.json"),JsonUtility.ToJson(result,true));}
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}void Log(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)result.errors.Add(message+"\n"+trace);}
 }
}
#endif
