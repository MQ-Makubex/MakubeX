#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MakubeX.Verification;
namespace MakubeX {
 public sealed class MemoryMapLocalQAProbe:MonoBehaviour {
  [Serializable] public class Result {
   public string status="running",error;
   public bool noSecondCover,allArtwork,roundMesh,roundHitTest,selectionKeepsPage,selectionDoesNotReplay,singleClickStaysOnMap,doubleClickEntersChapter,returnedWithEntrance,settingsAfterEntrance;
   public List<string> errors=new List<string>(),pages=new List<string>(),captures=new List<string>();
  }
  public Result result=new Result();public bool Complete{get;private set;}
  string output;MakubeXApp app;Exception failure;
  IEnumerator Start(){
   output=Path.GetFullPath("../Verification/MemoryMapLocal_20261026/playmode");Directory.CreateDirectory(output);
   app=GetComponent<MakubeXApp>();AudioListener.volume=0;Application.logMessageReceived+=Log;
   var run=Run();while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){failure=e;}if(!more||failure!=null)break;yield return run.Current;}
   Application.logMessageReceived-=Log;result.error=failure?.ToString();result.status=failure==null&&result.errors.Count==0?"passed":"failed";
   File.WriteAllText(Path.Combine(output,"results.json"),JsonUtility.ToJson(result,true));Complete=true;
  }
  IEnumerator Run(){
   float deadline=Time.realtimeSinceStartup+30;
   while(app.Busy){Check(Time.realtimeSinceStartup<deadline,"Initial cover load timed out");yield return null;}
   app.PageChanged+=Page;
   app.StartFromWeb();deadline=Time.realtimeSinceStartup+30;
   while(app.CurrentPage!="memory-map"){Check(Time.realtimeSinceStartup<deadline,"Map load timed out");yield return null;}
   result.noSecondCover=!result.pages.Contains("cover")&&!app.GetComponentsInChildren<Button>().Any(b=>b.name=="cover-start");Check(result.noSecondCover,"An internal Start cover reappeared");
   result.allArtwork=app.HasAllArtwork;Check(result.allArtwork,"Expected UI art did not load");
   var intro=app.GetComponentInChildren<MemoryMapEntrance>();Check(intro&&intro.IsPlaying,"Entrance did not start");
   Check(Shader.Find("MakubeX/MemoryMapState").isSupported&&Shader.Find("MakubeX/MemoryMapFog").isSupported,"Unsupported shader");
   float[] samples={.04f,.32f,.62f,.88f};int sample=0;
   while(app.Busy){
    Check(Time.realtimeSinceStartup<deadline,"Entrance did not finish");
    if(sample<samples.Length&&intro.Progress>=samples[sample])Capture("intro-"+(sample++).ToString("00"));yield return null;
   }
   Capture("map-settled");
   var node=app.GetComponentsInChildren<Button>().Single(b=>b.name=="node-01");
   var circle=node.GetComponent<CircularChapterHighlight>();Check(circle,"Node still uses rectangular Graphic");
   using(var mesh=new VertexHelper()){
    typeof(CircularChapterHighlight).GetMethod("OnPopulateMesh",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(VertexHelper)},null).Invoke(circle,new object[]{mesh});
    var vertices=new List<UIVertex>();mesh.GetUIVertexStream(vertices);var rect=circle.rectTransform.rect;float radius=Mathf.Min(rect.width,rect.height)*.5f;
    result.roundMesh=vertices.Count>100&&vertices.All(v=>((Vector2)v.position-rect.center).sqrMagnitude<=radius*radius+.1f);
   }
   Check(result.roundMesh,"Circle mesh spills outside its radius");
   var rt=node.GetComponent<RectTransform>();var hit=node.GetComponent<ArtworkHitArea>();var centre=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center));var corner=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.min+Vector2.one));
   result.roundHitTest=hit.IsRaycastLocationValid(centre,null)&&!hit.IsRaycastLocationValid(corner,null);Check(result.roundHitTest,"Circle hit mask is wrong");
   var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=centre,clickCount=1};
   ExecuteEvents.Execute(node.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
   float hoverUntil=Time.unscaledTime+.25f;while(Time.unscaledTime<hoverUntil)yield return null;Capture("circle-hover");
   var originalControls=node.transform.parent.gameObject;var originalIntro=intro;
   ExecuteEvents.Execute(node.gameObject,pointer,ExecuteEvents.pointerClickHandler);
   result.singleClickStaysOnMap=app.CurrentPage=="memory-map"&&!app.Busy;
   result.selectionKeepsPage=node&&ReferenceEquals(node.transform.parent.gameObject,originalControls);
   result.selectionDoesNotReplay=intro&&ReferenceEquals(app.GetComponentInChildren<MemoryMapEntrance>(),originalIntro)&&!intro.IsPlaying;
   Check(result.singleClickStaysOnMap&&result.selectionKeepsPage&&result.selectionDoesNotReplay,"Single selection rebuilds the map or restarts the entrance");
   app.HandleEscape();result.settingsAfterEntrance=app.IsPaused;app.HandleEscape();Check(result.settingsAfterEntrance&&!app.IsPaused,"Settings broken");
   pointer.clickCount=2;ExecuteEvents.Execute(node.gameObject,pointer,ExecuteEvents.pointerClickHandler);
   Check(app.CurrentPage=="chapter-loading","Double click did not immediately load chapter");deadline=Time.realtimeSinceStartup+60;
   while(app.CurrentPage!="game"){Check(Time.realtimeSinceStartup<deadline,"Chapter activation timed out");yield return null;}
   result.doubleClickEntersChapter=UnityEngine.Object.FindAnyObjectByType<Chapter01V004Story>()!=null;Check(result.doubleClickEntersChapter,"Wrong chapter");Capture("double-click-chapter");
   app.OpenMap();result.returnedWithEntrance=app.CurrentPage=="memory-map"&&app.IsMapEntering;Check(result.returnedWithEntrance,"Map return did not animate");
   deadline=Time.realtimeSinceStartup+10;while(app.Busy){Check(Time.realtimeSinceStartup<deadline,"Return entrance timed out");yield return null;}
   app.PageChanged-=Page;
  }
  void Page(string page){result.pages.Add(page);}
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  void Capture(string name){var path=Path.Combine(output,name+".png");typeof(VerificationCaptureBridge).GetMethod("CaptureOffscreen",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{path});result.captures.Add(path);}
  void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)result.errors.Add(message+"\n"+trace);}
 }
}
#endif
