#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using MakubeX.Verification;
namespace MakubeX {
 public sealed class PlayComfortQAProbe:MonoBehaviour {
  [Serializable] public class Result {
   public string status="running",error;
   public bool chapterClickLookOptIn,lookWithoutHeldButton,chapterEscapeOpens,escapeDeduplicated,pausedMovementBlocked,pausedLookBlocked,pausedInteractionBlocked,pausedDoorBlocked,transparentMenu,oneChineseHeading,noEnglishWords,volumeControlsWork,settingsDuckWorks,settingsDuckRestores,closeRestoresGame,returnMapWorks;
   public List<string> labels=new List<string>(),errors=new List<string>(),captures=new List<string>();
  }
  public Result result=new Result();public bool Complete{get;private set;}
  string output;MakubeXApp app;Exception failure;
  IEnumerator Start(){
   output=Path.GetFullPath("../Verification/PlayComfortLocal_20261026/playmode");Directory.CreateDirectory(output);AudioListener.volume=0;app=GetComponent<MakubeXApp>();Application.logMessageReceived+=Log;
   var run=Run();while(true){bool more=false;try{more=run.MoveNext();}catch(Exception e){failure=e;}if(!more||failure!=null)break;yield return run.Current;}
   Application.logMessageReceived-=Log;result.error=failure?.ToString();result.status=failure==null&&result.errors.Count==0?"passed":"failed";
   File.WriteAllText(Path.Combine(output,"results.json"),JsonUtility.ToJson(result,true));Complete=true;
  }
  IEnumerator Run(){
   float deadline=Time.realtimeSinceStartup+30;while(app.Busy){Check(Time.realtimeSinceStartup<deadline,"Initial load timeout");yield return null;}
   app.StartFromWeb();while(app.CurrentPage!="memory-map"||app.Busy){Check(Time.realtimeSinceStartup<deadline,"Map timeout");yield return null;}
   app.HandleEscape();Capture("map-settings");app.CloseSettings();
   app.ActivateChapter(0);deadline=Time.realtimeSinceStartup+60;
   while(app.CurrentPage!="game"){Check(Time.realtimeSinceStartup<deadline,"Chapter timeout");yield return null;}
   var story=UnityEngine.Object.FindAnyObjectByType<Chapter01V004Story>();Check(story,"Missing v004 story");
   while(!story.ReadableNightEnabled)yield return null;
   var walker=story.walker;for(int i=0;i<5;i++)yield return null;
   story.SetReadableNight(false);yield return null;yield return null;Capture("bridge-before");
   story.SetReadableNight(true);yield return null;yield return null;Capture("bridge-after");
   var view=walker.view;var parent=view.transform.parent;var position=view.transform.localPosition;var rotation=view.transform.localRotation;
   view.transform.SetParent(story.transform,true);view.transform.position=story.roomCamera;view.transform.LookAt(Vector3.Lerp(story.computer.position+Vector3.up*.4f,story.seat+Vector3.up,.5f));
   story.SetReadableNight(false);yield return null;yield return null;Capture("interior-before");
   story.SetReadableNight(true);yield return null;yield return null;Capture("interior-after");
   view.transform.SetParent(parent,false);view.transform.localPosition=position;view.transform.localRotation=rotation;
   result.chapterClickLookOptIn=walker.clickToLook;Check(result.chapterClickLookOptIn,"Story did not opt into click-to-look");
   var originalRotation=walker.transform.rotation;walker.CaptureLook();Check(walker.LookCaptured,"Capture state not retained");
   walker.ApplyLookDelta(new Vector2(2,0));result.lookWithoutHeldButton=Quaternion.Angle(originalRotation,walker.transform.rotation)>1;Check(result.lookWithoutHeldButton,"Look delta requires a held button");walker.ApplyLookDelta(new Vector2(-2,0));
   app.BrowserEscapePressed();app.BrowserEscapePressed();
   result.chapterEscapeOpens=app.IsPaused&&Time.timeScale==0;Check(result.chapterEscapeOpens,"Chapter Escape failed");
   var roots=app.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Settings").ToArray();result.escapeDeduplicated=roots.Length==1&&app.IsPaused;Check(result.escapeDeduplicated,"Escape double-toggled");
   var settings=roots.Single();
   result.labels=settings.GetComponentsInChildren<Text>().Select(t=>t.text).ToList();
   result.oneChineseHeading=result.labels.Count(t=>t=="设置")==1;
   result.noEnglishWords=result.labels.All(t=>!Regex.IsMatch(t,"[A-Za-z]{2,}"));
   Check(result.oneChineseHeading&&result.noEnglishWords,"Repeated heading or untranslated settings label");
   var card=settings.GetComponentsInChildren<Image>().Single(i=>i.name=="Translucent panel");
   var box=app.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Letterbox");
   result.transparentMenu=settings.GetComponent<Image>().color.a<.3f&&card.color.a<.7f&&!box.gameObject.activeSelf&&settings.GetComponentInParent<Canvas>().enabled;
   Check(result.transparentMenu,"Opaque cover hides chapter behind menu");
   var playerPosition=walker.transform.position;originalRotation=walker.transform.rotation;var cameraRotation=view.transform.localRotation;
   walker.Step(Vector2.up,false,.05f);result.pausedMovementBlocked=Vector3.Distance(playerPosition,walker.transform.position)<.00001f;
   walker.ApplyLookDelta(Vector2.one);result.pausedLookBlocked=Quaternion.Angle(originalRotation,walker.transform.rotation)<.001f&&Quaternion.Angle(cameraRotation,view.transform.localRotation)<.001f&&!walker.LookCaptured;
   bool doorWasOpen=story.door.IsOpen;story.door.Toggle();result.pausedDoorBlocked=story.door.IsOpen==doorWasOpen;
   walker.Body.enabled=false;walker.transform.position=story.BagApproach;walker.Body.enabled=true;Physics.SyncTransforms();story.Interact();result.pausedInteractionBlocked=story.Phase=="explore"&&story.Part==0;
   walker.Body.enabled=false;walker.transform.position=playerPosition;walker.Body.enabled=true;Physics.SyncTransforms();
   Check(result.pausedMovementBlocked&&result.pausedLookBlocked&&result.pausedDoorBlocked&&result.pausedInteractionBlocked,"Gameplay input leaked while menu open");
   var sliders=settings.GetComponentsInChildren<Slider>();
   var music=sliders.Single(s=>s.name=="settings-audio-Music");float old=music.value;music.value=33;result.volumeControlsWork=Mathf.Abs(app.Audio.MusicVolume-.33f)<.001f;music.value=old;Check(result.volumeControlsWork,"Music slider not connected");
   float until=Time.realtimeSinceStartup+.4f;while(Time.realtimeSinceStartup<until)yield return null;
   result.settingsDuckWorks=Mathf.Abs(app.Audio.SettingsDuckDb+6)<.1f;Check(result.settingsDuckWorks,"Settings music duck missing");Capture("chapter-transparent-settings");
   app.CloseSettings();result.closeRestoresGame=!app.IsPaused&&Time.timeScale==1&&!settings.GetComponentInParent<Canvas>().enabled;Check(result.closeRestoresGame,"Close did not restore game");
   until=Time.realtimeSinceStartup+.7f;while(Time.realtimeSinceStartup<until)yield return null;
   result.settingsDuckRestores=Mathf.Abs(app.Audio.SettingsDuckDb)<.1f;Check(result.settingsDuckRestores,"Music did not restore");
   app.HandleEscape();var returnButton=app.GetComponentsInChildren<Button>().Single(b=>b.name=="settings-return-map");returnButton.onClick.Invoke();result.returnMapWorks=app.CurrentPage=="memory-map"&&!app.IsPaused;Check(result.returnMapWorks,"Return to map broken");
  }
  void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
  void Capture(string name){var path=Path.Combine(output,name+".png");typeof(VerificationCaptureBridge).GetMethod("CaptureOffscreen",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{path});result.captures.Add(path);}
  void Log(string m,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert)result.errors.Add(m+"\n"+trace);}
 }
}
#endif
