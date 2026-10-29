using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MakubeX.Chapter01Integration;

namespace MakubeX.V004
{
    // Attached only to a temporary Play Mode instance by the v004 QA command.
    public sealed class V004WalkVerificationProbe : MonoBehaviour
    {
        [Serializable] public class Sample {public float seconds;public Vector3 position;public bool grounded;}
        [Serializable] public class Report
        {
            public string status="running",stage,error,graphicsDevice,inputMethod;
            public bool openToggleObserved,closeToggleObserved;
            public bool actualEditorPlayMode,closedDoorBlockedMovement,openKeyObserved,closeKeyObserved,openSightClear,enteredRoom,returnedToBridge;
            public int completedWaypoints,respawns,width,height,frames;
            public float lookSensitivity,seconds,minimumFootY=100,medianFrameMs,p90FrameMs;
            public List<string> errors=new List<string>(),screenshots=new List<string>();
            public List<Sample> trajectory=new List<Sample>();
        }
        public Chapter01WalkController player;
        public V004Door door;
        public Vector3[] route;
        public string output;
        public bool directToggleAutomation;
        public bool Complete {get;private set;}
        public bool Passed=>report.status=="passed";
        Report report=new Report();
        readonly List<float> frameTimes=new List<float>();
        float started,lastSample;
        Exception failure;
        Quaternion closedRotation;
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);started=Time.realtimeSinceStartup;
            report.actualEditorPlayMode=Application.isEditor && Application.isPlaying && !Application.isBatchMode;report.graphicsDevice=SystemInfo.graphicsDeviceName;
            report.inputMethod=directToggleAutomation?"Actual Play Mode, direct V004Door.Toggle API; physical E input pending because desktop locked":"Physical E key handled by original V004Door.Update";
            report.width=player.view.pixelWidth;report.height=player.view.pixelHeight;report.lookSensitivity=player.lookSensitivity;
            player.verificationMode=true;closedRotation=door.hinge.rotation;Application.logMessageReceived+=OnLog;
            var work=Traverse();
            while(true)
            {
                bool more=false;
                try {more=work.MoveNext();}catch(Exception e){failure=e;}
                if(!more || failure!=null)break;
                yield return work.Current;
            }
            Application.logMessageReceived-=OnLog;
            report.respawns=player.Respawns;report.seconds=Time.realtimeSinceStartup-started;report.error=failure?.ToString();
            frameTimes.Sort();if(frameTimes.Count>0){report.medianFrameMs=frameTimes[frameTimes.Count/2];report.p90FrameMs=frameTimes[(int)(frameTimes.Count*.9f)];}
            report.status=failure==null && report.errors.Count==0?"passed":"failed";report.stage="complete";Save();Complete=true;
            Debug.Log("V004_RUNTIME_VISUAL_QA_"+report.status.ToUpperInvariant()+" "+report.error);
        }
        IEnumerator Traverse()
        {
            Require(report.actualEditorPlayMode,"QA requires actual non-batch Editor Play Mode");
            Stage("settling-on-bridge");float deadline=Time.realtimeSinceStartup+1;
            while(Time.realtimeSinceStartup<deadline){Move(Vector2.zero);yield return null;}
            Require(player.Body.isGrounded,"Spawn is not grounded");Capture("runtime-bridge");
            for(int i=1;i<route.Length;i++)
            {
                Stage("walking-outbound-"+i);deadline=Time.realtimeSinceStartup+FlatDistance(route[i])/player.walkSpeed*4+12;
                while(FlatDistance(route[i])>.1f){Require(Time.realtimeSinceStartup<deadline,"Outbound movement blocked at waypoint "+i);player.Face(route[i]-player.transform.position);Move(Vector2.up);yield return null;}
                report.completedWaypoints++;
                if(i==2)
                {
                    Stage("checking-closed-door-collision");player.Face(route[3]-player.transform.position);
                    bool sideHit=false;deadline=Time.realtimeSinceStartup+1.1f;
                    while(Time.realtimeSinceStartup<deadline){Move(Vector2.up);sideHit|=(player.LastFlags&CollisionFlags.Sides)!=0;yield return null;}
                    report.closedDoorBlockedMovement=sideHit && player.transform.position.z>.5f;
                    Require(report.closedDoorBlockedMovement,"Closed Tripo door failed to block CharacterController");Capture("runtime-door-closed");
                    Require(Vector3.Distance(player.transform.position,door.hinge.position)<3,"Player is outside the real E interaction range");
                    if(directToggleAutomation)door.Toggle();
                    Stage(directToggleAutomation?"automated-door-open":"awaiting-real-E-open");deadline=Time.realtimeSinceStartup+90;
                    while(!door.IsOpen){Require(Time.realtimeSinceStartup<deadline,"No E open input received");yield return null;}
                    report.openToggleObserved=true;report.openKeyObserved=!directToggleAutomation;Stage("waiting-for-open-animation");
                    while(Quaternion.Angle(door.hinge.rotation,door.openPose.rotation)>1){Move(Vector2.zero);yield return null;}
                    Physics.SyncTransforms();
                    var hits=Physics.RaycastAll(new Vector3(0,1.4f,1),Vector3.back,8).Where(h=>h.collider!=player.Body).OrderBy(h=>h.distance).ToArray();
                    report.openSightClear=hits.Length>0 && hits[0].distance>1.5f && hits.All(h=>h.collider.name!="LatticeTower_Master");
                    Require(report.openSightClear,"Open doorway sight ray is still obstructed");
                }
            }
            report.enteredRoom=player.transform.position.y>.13f && FlatDistance(route[route.Length-1])<.11f;Require(report.enteredRoom,"Room interior was not reached");Capture("runtime-inside");
            for(int i=route.Length-2;i>=0;i--)
            {
                Stage("walking-return-"+i);deadline=Time.realtimeSinceStartup+FlatDistance(route[i])/player.walkSpeed*4+12;
                while(FlatDistance(route[i])>.1f){Require(Time.realtimeSinceStartup<deadline,"Return movement blocked at waypoint "+i);player.Face(route[i]-player.transform.position);Move(Vector2.up);yield return null;}
                report.completedWaypoints++;
                if(i==2)
                {
                    if(directToggleAutomation)door.Toggle();
                    Stage(directToggleAutomation?"automated-door-close":"awaiting-real-E-close");deadline=Time.realtimeSinceStartup+90;
                    while(door.IsOpen){Require(Time.realtimeSinceStartup<deadline,"No E close input received");yield return null;}
                    report.closeToggleObserved=true;report.closeKeyObserved=!directToggleAutomation;Stage("waiting-for-close-animation");
                    while(Quaternion.Angle(door.hinge.rotation,closedRotation)>1){Move(Vector2.zero);yield return null;}
                    Capture("runtime-door-closed-again");
                }
            }
            report.returnedToBridge=FlatDistance(route[0])<.11f;Require(report.returnedToBridge,"Did not return to bridge");
            Require(player.Respawns==0 && report.minimumFootY>-.1f,"Traversal fell or used respawn");Capture("runtime-returned");
        }
        float FlatDistance(Vector3 target){var d=target-player.transform.position;d.y=0;return d.magnitude;}
        void Move(Vector2 input)
        {
            Physics.SyncTransforms();player.Step(input,false,Time.deltaTime);report.frames++;
            if(Time.unscaledDeltaTime>0 && Time.unscaledDeltaTime<.25f)frameTimes.Add(Time.unscaledDeltaTime*1000);
            report.minimumFootY=Mathf.Min(report.minimumFootY,player.transform.position.y);
            if(Time.realtimeSinceStartup-lastSample>.1f){lastSample=Time.realtimeSinceStartup;report.trajectory.Add(new Sample{seconds=Time.realtimeSinceStartup-started,position=player.transform.position,grounded=player.Body.isGrounded});}
            Require(player.transform.position.y>-.5f,"Player fell off route");
        }
        void Stage(string value){report.stage=value;Save();}
        void Save(){File.WriteAllText(Path.Combine(output,"runtime_walk.json"),JsonUtility.ToJson(report,true));}
        void Capture(string name)
        {
            var cam=player.view;var old=cam.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {cam.targetTexture=rt;cam.aspect=16f/9;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();string path=Path.Combine(output,name+".png");File.WriteAllBytes(path,tex.EncodeToPNG());report.screenshots.Add(path);}
            finally {cam.targetTexture=old;RenderTexture.active=active;rt.Release();Destroy(rt);Destroy(tex);}
        }
        void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
        void OnLog(string message,string stack,LogType kind){if(kind==LogType.Error || kind==LogType.Exception || kind==LogType.Assert)report.errors.Add(message+"\n"+stack);}
    }
}
