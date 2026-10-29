#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MakubeX.Verification;

namespace MakubeX
{
    public sealed class PhaseOneV004QAProbe:MonoBehaviour
    {
        [Serializable] public class Result
        {
            public string status="running",stage,error,artworkRevision,captureMethod="Actual Editor Camera.Render with original overlay canvases directed to the camera; full native player captures verified separately";
            public bool settingsMapOnly,lockedChapterRejected,doorKey,bagKey,chairKey,bootKey,screenStayedOffUntilThirdE,screenLitAfterBoot,returnedToMap,chapterTwoUnlocked,replayProgressPreserved;
            public int respawns;public Vector3 lastPlayerPosition,lastMovementTarget;public List<string> errors=new List<string>(),lines=new List<string>(),captures=new List<string>();
        }
        public string output;
        public bool Complete{get;private set;}
        public Result result=new Result();
        MakubeXApp app;Chapter01V004Story story;
        float deadline;Exception failure;
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);app=GetComponent<MakubeXApp>();Application.logMessageReceived+=Log;
            var work=Run();while(true){bool more=false;try{more=work.MoveNext();}catch(Exception e){failure=e;}if(!more || failure!=null)break;yield return work.Current;}
            Application.logMessageReceived-=Log;result.error=failure?.ToString();result.status=failure==null && result.errors.Count==0?"passed":"failed";result.stage="complete";Save();Complete=true;
            Debug.Log("PHASEONE_V004_QA_"+result.status.ToUpperInvariant()+" "+result.error);
        }
        IEnumerator Run()
        {
            Stage("waiting-for-cover");deadline=Time.realtimeSinceStartup+30;
            while(app.Busy){Check(Time.realtimeSinceStartup<deadline,"Cover failed to load");yield return null;}
            result.artworkRevision=MakubeXApp.ArtworkRevision;Capture("01-cover");
            app.HandleEscape();result.settingsMapOnly=!app.IsPaused;Click("cover-start");deadline=Time.realtimeSinceStartup+30;
            while(app.CurrentPage!="memory-map"){Check(Time.realtimeSinceStartup<deadline,"Map failed to load");yield return null;}
            Check(app.HasAllArtwork,"All three images did not load");Capture("02-map");int selected=app.Selected;app.SelectChapter(7);result.lockedChapterRejected=app.Selected==selected;Check(result.lockedChapterRejected,"Locked chapter was selectable");
            app.HandleEscape();result.settingsMapOnly&=app.IsPaused;Capture("03-map-settings");app.HandleEscape();Check(result.settingsMapOnly && !app.IsPaused,"Settings entry contract failed");
            Click("chapter-launch");Check(app.CurrentPage=="chapter-loading","Chapter loading page missing");Capture("04-chapter-loading");deadline=Time.realtimeSinceStartup+60;
            while(app.CurrentPage!="game"){Check(Time.realtimeSinceStartup<deadline,"Latest chapter failed to activate");yield return null;}
            story=UnityEngine.Object.FindAnyObjectByType<Chapter01V004Story>();Check(story!=null,"Old chapter was loaded instead of v004");story.verificationMovement=true;
            app.HandleEscape();Check(!app.IsPaused,"Settings unexpectedly opened in chapter");Check(!story.ComputerOn && !story.ScreenLit,"CRT must start off");Capture("05-bridge-v004");
            var walker=story.walker;
            foreach(var target in new[]{new Vector3(3,.15f,2.2f),new Vector3(0,.15f,1.2f)})
            {Stage("walking-bridge");deadline=Time.realtimeSinceStartup+30;while(Flat(target,walker.transform.position)>.11f){Check(Time.realtimeSinceStartup<deadline,"Bridge route blocked");Drive(target);yield return null;}}
            Stage("awaiting-real-E-door");deadline=Time.realtimeSinceStartup+300;while(!story.door.IsOpen){Check(Time.realtimeSinceStartup<deadline,"No E input at door");yield return null;}result.doorKey=true;
            while(Quaternion.Angle(story.door.hinge.rotation,story.door.openPose.rotation)>1)yield return null;
            foreach(var target in new[]{new Vector3(0,.17f,-.9f),story.BagApproach})
            {Stage("walking-to-bag");deadline=Time.realtimeSinceStartup+25;while(Flat(target,walker.transform.position)>.11f){Check(Time.realtimeSinceStartup<deadline,"Path to actual Tripo bag blocked");Drive(target);yield return null;}}
            Capture("06-real-bag");Stage("awaiting-real-E-bag");deadline=Time.realtimeSinceStartup+300;
            while(story.Phase=="explore"){Check(Time.realtimeSinceStartup<deadline,"No E bag input");yield return null;}result.bagKey=true;Capture("07-bag-dialogue");
            while(story.Part<1){RememberLine();yield return null;}
            foreach(var target in new[]{new Vector3(0,.17f,-1.1f),story.ChairApproach})
            {Stage("walking-to-chair");deadline=Time.realtimeSinceStartup+25;while(Flat(target,walker.transform.position)>.11f){Check(Time.realtimeSinceStartup<deadline,"Path to actual Tripo chair blocked");Drive(target);yield return null;}}
            Stage("awaiting-real-E-chair");deadline=Time.realtimeSinceStartup+300;while(story.Phase=="explore"){Check(Time.realtimeSinceStartup<deadline,"No E chair input");yield return null;}result.chairKey=true;
            while(story.Phase!="await-boot"){RememberLine();yield return null;}
            deadline=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<deadline){Check(!story.ComputerOn && !story.ScreenLit,"Screen booted automatically after sit");yield return null;}
            result.screenStayedOffUntilThirdE=true;Capture("08-seated-screen-off");Stage("awaiting-real-E-boot");deadline=Time.realtimeSinceStartup+300;
            while(!story.ComputerOn){Check(Time.realtimeSinceStartup<deadline,"No independent E boot input");yield return null;}result.bootKey=true;deadline=Time.realtimeSinceStartup+30;
            while(app.CurrentPage=="game")
            {
                Check(Time.realtimeSinceStartup<deadline,"Story did not complete");RememberLine();if(story)result.respawns=story.walker.Respawns;
                if(story && story.ScreenLit && !result.screenLitAfterBoot){result.screenLitAfterBoot=true;Capture("09-screen-powered");}
                yield return null;
            }
            result.returnedToMap=app.CurrentPage=="memory-map";result.chapterTwoUnlocked=app.Completed==1;
            Check(result.returnedToMap && result.chapterTwoUnlocked && result.screenLitAfterBoot,"Completion/unlock failed");Capture("10-map-completed");
            Check(result.respawns==0,"Integration traversal used respawn");
            app.SelectChapter(0);Click("chapter-launch");deadline=Time.realtimeSinceStartup+40;while(app.CurrentPage!="game"){Check(Time.realtimeSinceStartup<deadline,"Replay failed to load");yield return null;}
            result.replayProgressPreserved=app.Completed==1 && PlayerPrefs.GetInt(MakubeXApp.ProgressSaveKey)==1;Check(result.replayProgressPreserved,"Replay lowered saved progress");Capture("11-replay");
            Check(result.lines.Contains("爷爷……这是我的名字吗？") && result.lines.Contains("我不知道自己从哪里来。但那一天，我有了家。"),"Approved dialogue did not run");
        }
        void RememberLine(){if(story && !string.IsNullOrEmpty(story.CurrentLine) && !result.lines.Contains(story.CurrentLine))result.lines.Add(story.CurrentLine);}
        void Drive(Vector3 target){result.lastMovementTarget=target;story.walker.Face(target-story.walker.transform.position);story.walker.Step(Vector2.up,false,Time.deltaTime);result.lastPlayerPosition=story.walker.transform.position;Check(story.walker.transform.position.y>-.2f,"Player fell");}
        static float Flat(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        void Click(string name){var button=app.GetComponentsInChildren<Button>(true).First(b=>b.name==name && b.gameObject.activeInHierarchy);Check(button.interactable,"Button disabled: "+name);button.onClick.Invoke();}
        void Capture(string name){string path=Path.Combine(output,name+".png");typeof(VerificationCaptureBridge).GetMethod("CaptureOffscreen",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{path});result.captures.Add(path);}
        void Stage(string value){result.stage=value;Save();}
        void Save(){File.WriteAllText(Path.Combine(output,"workflow.json"),JsonUtility.ToJson(result,true));}
        void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        void Log(string message,string stack,LogType kind){if(kind==LogType.Error || kind==LogType.Exception || kind==LogType.Assert)result.errors.Add(message+"\n"+stack);}
    }
}
#endif
