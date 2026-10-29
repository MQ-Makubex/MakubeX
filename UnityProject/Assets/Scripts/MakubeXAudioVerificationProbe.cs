#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace MakubeX
{
    // Observes real Play Mode only. No Interact, CompleteChapter, simulated keys, or movement calls.
    public sealed class MakubeXAudioVerificationProbe:MonoBehaviour
    {
        [Serializable] public class Voice{public string clip;public bool playing;public float volume;public int sample;}
        [Serializable] public class Frame
        {
            public float time,timeScale,insideBlend,settingsDb,dialogueDb,musicDb,ambienceDb,sfxDb,rms;
            public string page,phase;public int part,managers,sources,listeners;public bool inside,headPat;public Vector3 player;public Voice[] voices;
        }
        [Serializable] public class Report
        {
            public string status="observing",stage,method="Passive real Play Mode observer; no gameplay simulation or time acceleration";
            public bool outside90,noEarlyTheme=true,noDuplicateSources=true,doorBeforeEntry,settingsMinus6,settingsRestored,dialogueMinus5,musicMute,ambienceMute,sfxMute,themeAtActualPat,returnedToMap,replayNoEarlyTheme;
            public float outsideSeconds,crossfadeSeconds,maxOutputPeak;public int outsideLoopWraps,bagCount,chairCount,crtCount,patCount;public long outputSamples;
            public List<string> errors=new List<string>(),recordings=new List<string>();public List<MakubeXAudio.Trace> cues=new List<MakubeXAudio.Trace>();public List<Frame> frames=new List<Frame>();
        }
        public string output;public Report report=new Report();MakubeXApp app;MakubeXAudio audio;float sampled,saved,outsideStarted=-1,enteredAt=-1,themeAt=-1;int lastOutdoorSample;Vector3 startPosition;readonly float[] samples=new float[512];readonly List<AudioOutputEvidence> taps=new List<AudioOutputEvidence>();
        bool savedOutdoor,savedTheme,savedMap,outsideHoldBroken;int firstGeneration,currentGeneration;
        void Start(){app=GetComponent<MakubeXApp>();audio=GetComponent<MakubeXAudio>();Directory.CreateDirectory(output);audio.CueObserved+=Cue;Application.logMessageReceived+=Log;}
        void Cue(MakubeXAudio.Trace cue)
        {
            report.cues.Add(cue);
            if(cue.cue=="Chapter01Started")currentGeneration=cue.chapter;
            if(cue.cue=="Chapter01Started" && firstGeneration==0){firstGeneration=cue.chapter;outsideStarted=Time.realtimeSinceStartup;startPosition=audio.BoundStory.walker.transform.position;}
            if(cue.chapter==firstGeneration)
            {
                if(cue.cue=="DoorOpened" && !audio.Inside)report.doorBeforeEntry=true;
                if(cue.cue=="HutInteriorEntered")enteredAt=cue.realtime;
                if(cue.cue=="BagInspected")report.bagCount++;
                if(cue.cue=="PlayerSatDown")report.chairCount++;
                if(cue.cue=="CRTPoweredOn")report.crtCount++;
                if(cue.cue=="GrandpaHeadPatStarted"){report.patCount++;report.themeAtActualPat=audio.BoundStory && audio.BoundStory.HeadPatSoundTriggered;themeAt=cue.realtime;}
            }
            Save();
        }
        void Update()
        {
            if(!audio || !audio.Ready || Time.realtimeSinceStartup-sampled<.1f)return;sampled=Time.realtimeSinceStartup;
            var listener=UnityEngine.Object.FindObjectsByType<AudioListener>().FirstOrDefault(l=>l.enabled && l.gameObject.activeInHierarchy);
            if(listener && !listener.GetComponent<AudioOutputEvidence>()){var tap=listener.gameObject.AddComponent<AudioOutputEvidence>();taps.Add(tap);}
            AudioListener.GetOutputData(samples,0);double square=0;foreach(var x in samples)square+=x*x;
            audio.Catalog.mixer.GetFloat("MusicVolumeDb",out float music);audio.Catalog.mixer.GetFloat("MusicDuckDb",out float duck);audio.Catalog.mixer.GetFloat("AmbienceVolumeDb",out float amb);audio.Catalog.mixer.GetFloat("SFXVolumeDb",out float sfx);
            var story=audio.BoundStory;var voices=audio.Voices;
            var frame=new Frame{time=sampled,timeScale=Time.timeScale,page=app.CurrentPage,phase=story?story.Phase:null,part=story?story.Part:-1,inside=audio.Inside,insideBlend=audio.InteriorBlend,settingsDb=audio.SettingsDuckDb,dialogueDb=audio.DialogueDuckDb,musicDb=music,ambienceDb=amb,sfxDb=sfx,rms=(float)Math.Sqrt(square/samples.Length),managers=UnityEngine.Object.FindObjectsByType<MakubeXAudio>().Length,sources=audio.GetComponentsInChildren<AudioSource>().Length,listeners=UnityEngine.Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled && l.gameObject.activeInHierarchy),player=story?story.walker.transform.position:Vector3.zero,headPat=story && story.HeadPatSoundTriggered,voices=voices.Select(s=>new Voice{clip=s.clip?s.clip.name:null,playing=s.isPlaying,volume=s.volume,sample=s.isPlaying?s.timeSamples:0}).ToArray()};
            report.frames.Add(frame);
            if(frame.managers!=1 || frame.sources!=6)report.noDuplicateSources=false;
            var activeMusic=voices.Take(2).Where(s=>s.isPlaying && s.volume>.00001f).ToArray();if(activeMusic.Length==2 && activeMusic[0].clip==activeMusic[1].clip)report.noDuplicateSources=false;
            bool theme=activeMusic.Any(s=>s.clip==audio.Catalog.homeTheme);
            if(story && !story.HeadPatSoundTriggered && theme)report.noEarlyTheme=false;
            if(story && firstGeneration>0 && report.patCount>0 && story.Part==0 && !theme)report.replayNoEarlyTheme=true;
            if(app.CurrentPage=="memory-map" && app.IsPaused && Math.Abs(audio.SettingsDuckDb+6)<.05f && Math.Abs(duck+6)<.05f && Time.timeScale==0)report.settingsMinus6=true;
            if(report.settingsMinus6 && !app.IsPaused && Math.Abs(duck)<.05f)report.settingsRestored=true;
            if(Math.Abs(audio.DialogueDuckDb+5)<.05f)report.dialogueMinus5=true;
            if(music<-79)report.musicMute=true;if(amb<-79)report.ambienceMute=true;if(sfx<-79)report.sfxMute=true;
            // CharacterController settles vertically on spawn; a stationary hold is measured in X/Z.
            // A replay cannot add its time/loop wraps to the first attempt.
            if(currentGeneration==firstGeneration && outsideStarted>=0 && story && !savedOutdoor)
            {
                var delta=story.walker.transform.position-startPosition;delta.y=0;
                if(delta.magnitude>=.02f || story.Part!=0 || audio.Inside)outsideHoldBroken=true;
                if(!outsideHoldBroken)
                {
                    report.outsideSeconds=sampled-outsideStarted;int sample=voices[2].timeSamples;if(sample<lastOutdoorSample-2400)report.outsideLoopWraps++;lastOutdoorSample=sample;
                    if(report.outsideSeconds>=90 && voices[2].isPlaying && frame.rms>.00001f && report.noEarlyTheme && report.outsideLoopWraps>=3){report.outside90=true;savedOutdoor=true;Capture("outside-90s");}
                }
            }
            if(enteredAt>=0 && audio.InteriorBlend>=.9999f && report.crossfadeSeconds==0)report.crossfadeSeconds=sampled-enteredAt;
            if(themeAt>=0 && !savedTheme && sampled-themeAt>3){savedTheme=true;Capture("head-pat-theme");}
            if(report.patCount>0 && app.CurrentPage=="memory-map"){report.returnedToMap=true;if(!savedMap && audio.SettingsDuckDb>-.01f){savedMap=true;Capture("returned-map");}}
            report.stage=story?(report.outside90?story.Phase+" / step "+story.Part:"hold outside for 90s"):app.CurrentPage;
            if(sampled-saved>2){saved=sampled;Save();}
        }
        public void Capture(string name)
        {
            var tap=UnityEngine.Object.FindObjectsByType<AudioOutputEvidence>().FirstOrDefault(t=>t.GetComponent<AudioListener>().enabled);if(!tap)return;
            string path=Path.Combine(output,name+".wav");tap.Save(path);if(File.Exists(path) && !report.recordings.Contains(path))report.recordings.Add(path);
        }
        public void Save()
        {
            foreach(var tap in taps)if(tap){report.maxOutputPeak=Math.Max(report.maxOutputPeak,tap.Peak);report.outputSamples=Math.Max(report.outputSamples,tap.SampleCount);}
            var path=Path.Combine(output,"playmode_audio.json");var temporary=path+".tmp";
            File.WriteAllText(temporary,JsonUtility.ToJson(report,true));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
        }
        public void Finish()
        {
            report.status=report.errors.Count==0 && report.maxOutputPeak>.0001f && report.outside90 && report.noEarlyTheme && report.noDuplicateSources && report.doorBeforeEntry && report.settingsMinus6 && report.settingsRestored && report.dialogueMinus5 && report.musicMute && report.ambienceMute && report.sfxMute && report.themeAtActualPat && report.returnedToMap && report.bagCount==1 && report.chairCount==1 && report.crtCount==1 && report.patCount==1 && report.crossfadeSeconds>=3.2f && report.crossfadeSeconds<=4?"passed":"needs_review";Save();
        }
        void Log(string value,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)report.errors.Add(value+"\n"+stack);}
        void OnDestroy(){if(audio)audio.CueObserved-=Cue;Application.logMessageReceived-=Log;if(!string.IsNullOrEmpty(output))Save();}
    }
}
#endif
