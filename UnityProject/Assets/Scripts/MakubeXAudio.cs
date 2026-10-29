using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using MakubeX.V004;

namespace MakubeX
{
    // Owned by the existing persistent MakubeXApp, never by a transient scene or UI page.
    [DisallowMultipleComponent]
    public sealed class MakubeXAudio:MonoBehaviour
    {
        public const string MusicKey="makubex.audio.music",AmbienceKey="makubex.audio.ambience",SfxKey="makubex.audio.sfx";
        [Serializable] public class Trace
        {
            public string cue,clip;public float realtime;public double dsp;public int chapter;public Vector3 player;
        }
        public readonly List<Trace> Events=new List<Trace>();
        public MakubeXAudioCatalog Catalog{get;private set;}
        public bool Ready=>Catalog && Catalog.mixer;
        public bool Inside{get;private set;}
        public float InteriorBlend=>interior.value;
        public float SettingsDuckDb=>settingsDuck.value;
        public float DialogueDuckDb=>dialogueDuck.value;
        public float MusicVolume{get;private set;}=.75f;
        public float AmbienceVolume{get;private set;}=.85f;
        public float SfxVolume{get;private set;}=.85f;
        public AudioSource[] Voices=>voices;
        public Chapter01V004Story BoundStory=>story;
        public event Action<Trace> CueObserved;
        AudioSource[] voices;
        Envelope[] musicGains={new Envelope(),new Envelope()};
        Envelope ambience=new Envelope(),interior=new Envelope(),settingsDuck=new Envelope(),dialogueDuck=new Envelope();
        Envelope themeBed=new Envelope{value=1,from=1,target=1};
        MakubeXApp app;Chapter01V004Story story;V004Door door;
        Matrix4x4 worldToBlender;
        readonly HashSet<Chapter01SoundEvent> played=new HashSet<Chapter01SoundEvent>();
        bool chapterActive,started;int generation,musicSlot;Scene? storyScene;
        sealed class Envelope
        {
            public float value,target,from,duration,elapsed;
            public void To(float v,float seconds){if(Mathf.Approximately(target,v))return;from=value;target=v;duration=Mathf.Max(.001f,seconds);elapsed=0;}
            public void Tick(float dt){elapsed+=dt;value=Mathf.Lerp(from,target,Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/Mathf.Max(.001f,duration))));}
        }
        void Awake()
        {
            app=GetComponent<MakubeXApp>();Catalog=Resources.Load<MakubeXAudioCatalog>("Audio/MakubeXAudioCatalog");
            if(!app || !Ready){Debug.LogError("MakubeX audio catalog/mixer missing; no replacement audio generated");return;}
            var groups=new[]{Catalog.music,Catalog.music,Catalog.ambienceOutside,Catalog.ambienceInside,Catalog.sfx,Catalog.sfx};
            var names=new[]{"Music A","Music B","Outside bed","Inside bed","SFX A","SFX B"};voices=new AudioSource[6];
            for(int i=0;i<voices.Length;i++)
            {
                var go=new GameObject(names[i]);go.transform.SetParent(transform,false);var source=go.AddComponent<AudioSource>();voices[i]=source;
                source.playOnAwake=false;source.spatialBlend=0;source.dopplerLevel=0;source.volume=0;source.outputAudioMixerGroup=groups[i];source.priority=i<2?64:128;
            }
            app.PageChanged+=Page;app.SettingsVisibilityChanged+=Settings;
            SceneManager.sceneLoaded+=Loaded;SceneManager.sceneUnloaded+=Unloaded;
        }
        void Start()
        {
            if(!Ready)return;Catalog.mixer.updateMode=AudioMixerUpdateMode.UnscaledTime;started=true;
            MusicVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey,.75f));AmbienceVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceKey,.85f));SfxVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey,.85f));ApplyVolumes();
        }
        void Update()
        {
            if(!Ready || !started)return;float dt=Time.unscaledDeltaTime;
            settingsDuck.Tick(dt);dialogueDuck.Tick(dt);Catalog.mixer.SetFloat("MusicDuckDb",settingsDuck.value+dialogueDuck.value);
            for(int i=0;i<2;i++){musicGains[i].Tick(dt);voices[i].volume=musicGains[i].value;if(musicGains[i].target==0 && musicGains[i].value<=.00001f && voices[i].isPlaying)voices[i].Stop();}
            ambience.Tick(dt);interior.Tick(dt);themeBed.Tick(dt);
            voices[2].volume=ambience.value*Mathf.Cos(interior.value*Mathf.PI*.5f)*.85f;
            voices[3].volume=ambience.value*Mathf.Sin(interior.value*Mathf.PI*.5f)*.85f*themeBed.value;
            if(!chapterActive && ambience.value<=.00001f){voices[2].Stop();voices[3].Stop();}
        }
        void LateUpdate()
        {
            if(!chapterActive || !story)return;
            // Use the physical CharacterController root, never the cinematic camera or door state.
            var p=worldToBlender.MultiplyPoint3x4(story.walker.transform.position);
            bool inside=p.y>(Inside?.1f:.45f) && p.y<5.1f && Mathf.Abs(p.x)<4.8f && p.z>-.15f && p.z<3.7f;
            if(inside==Inside)return;Inside=inside;
            if(inside && !voices[3].isPlaying){voices[3].clip=Catalog.insideLoop;voices[3].loop=true;voices[3].Play();}
            if(!inside && !voices[2].isPlaying){voices[2].clip=Catalog.outsideLoop;voices[2].loop=true;voices[2].Play();}
            interior.To(inside?1:0,3.5f);Log(inside?"HutInteriorEntered":"HutInteriorExited",inside?Catalog.insideLoop:Catalog.outsideLoop);
        }
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(!Ready)return;
            foreach(var root in scene.GetRootGameObjects())
            {
                var found=root.GetComponentInChildren<Chapter01V004Story>(true);if(!found)continue;Bind(found);break;
            }
        }
        void Bind(Chapter01V004Story found)
        {
            if(story==found)return;Unbind();story=found;storyScene=found.gameObject.scene;door=story.door;story.SoundEvent+=StoryCue;story.DialogueVisibilityChanged+=Dialogue;door.OpenStateChanged+=Door;
            var ts=found.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("AXIS_")).ToArray();var matrix=Matrix4x4.identity;Vector3 origin=Vector3.zero;
            foreach(var t in ts)if(t.name=="AXIS_Origin")origin=t.position;
            foreach(var t in ts){int index=t.name=="AXIS_X"?0:t.name=="AXIS_Y"?1:t.name=="AXIS_Z"?2:-1;if(index>=0){var v=t.position-origin;matrix.SetColumn(index,new Vector4(v.x,v.y,v.z,0));}}
            matrix.SetColumn(3,new Vector4(origin.x,origin.y,origin.z,1));worldToBlender=matrix.inverse;
            generation++;played.Clear();chapterActive=true;Inside=false;interior.value=interior.from=interior.target=0;themeBed.To(1,.6f);
            voices[2].clip=Catalog.outsideLoop;voices[2].loop=true;voices[2].Play();voices[3].Stop();ambience.To(1,.75f);FadeMusic(1.8f);Log("Chapter01Started",Catalog.outsideLoop);
        }
        void Unloaded(Scene scene)
        {
            if(!storyScene.HasValue || scene!=storyScene.Value)return;
            chapterActive=false;ambience.To(0,2);dialogueDuck.To(0,.6f);Unbind();
        }
        void Unbind()
        {
            if(story){story.SoundEvent-=StoryCue;story.DialogueVisibilityChanged-=Dialogue;}
            if(door)door.OpenStateChanged-=Door;story=null;door=null;storyScene=null;
        }
        void Page(string page)
        {
            if(!Ready)return;
            if(page=="memory-map")
            {
                chapterActive=false;ambience.To(0,2);themeBed.To(1,1.2f);dialogueDuck.To(0,.6f);Unbind();PlayMusic(Catalog.mapLoop,true,.75f,1.5f);Log("MemoryMapEntered",Catalog.mapLoop);
            }
            else if(page=="chapter-loading"){FadeMusic(1.8f);Log("ChapterSelectionFade",null);}
            else if(page=="cover"){FadeMusic(1.5f);ambience.To(0,1.5f);chapterActive=false;}
        }
        void Settings(bool open){settingsDuck.To(open?-6:0,open?.25f:.6f);Log(open?"MapSettingsOpened":"MapSettingsClosed",null);}
        void Dialogue(bool visible){dialogueDuck.To(visible?-5:0,visible?.2f:.7f);Log(visible?"DialogueVisible":"DialogueHidden",null);}
        void Door(bool open){Sfx(open?Catalog.doorOpen:Catalog.doorClose);Log(open?"DoorOpened":"DoorClosed",open?Catalog.doorOpen:Catalog.doorClose);}
        void StoryCue(Chapter01SoundEvent cue)
        {
            if(!played.Add(cue)){Log("DuplicateSuppressed:"+cue,null);return;}
            switch(cue)
            {
                case Chapter01SoundEvent.BagInspected:Sfx(Catalog.bag);PlayMusic(Catalog.nameFragment,false,.50f,.06f);break;
                case Chapter01SoundEvent.PlayerSatDown:Sfx(Catalog.chair);break;
                case Chapter01SoundEvent.CRTPoweredOn:Sfx(Catalog.crt);break;
                // Keep the first piano note intact and gently make space in the room bed.
                case Chapter01SoundEvent.GrandpaHeadPatStarted:Sfx(Catalog.pat);themeBed.To(Mathf.Pow(10,-2.5f/20),.7f);PlayMusic(Catalog.homeTheme,false,.70f,.12f);break;
                // Existing story returns after 2.1 s; this tail overlaps the map's 1.5 s entrance.
                case Chapter01SoundEvent.Chapter01Finished:FadeMusic(3.6f);dialogueDuck.To(0,.8f);break;
            }
            Log(cue.ToString(),cue==Chapter01SoundEvent.GrandpaHeadPatStarted?Catalog.homeTheme:cue==Chapter01SoundEvent.BagInspected?Catalog.nameFragment:null);
        }
        void PlayMusic(AudioClip clip,bool loop,float gain,float fade)
        {
            if(!clip){Debug.LogError("Required music clip missing");return;}
            for(int i=0;i<2;i++)if(voices[i].isPlaying && voices[i].clip==clip){musicGains[i].To(gain,fade);musicGains[1-i].To(0,fade);return;}
            musicSlot=1-musicSlot;var source=voices[musicSlot];source.Stop();source.clip=clip;source.loop=loop;source.volume=0;musicGains[musicSlot].value=musicGains[musicSlot].from=musicGains[musicSlot].target=0;
            source.Play();musicGains[musicSlot].To(gain,fade);musicGains[1-musicSlot].To(0,fade);
        }
        void FadeMusic(float seconds){for(int i=0;i<2;i++)musicGains[i].To(0,seconds);}
        void Sfx(AudioClip clip)
        {
            if(!clip){Debug.LogError("Required SFX clip missing");return;}
            for(int i=4;i<6;i++)if(voices[i].isPlaying && voices[i].clip==clip)return;
            int slot=!voices[4].isPlaying?4:!voices[5].isPlaying?5:-1;
            if(slot<0){Log("SfxBusySuppressed",clip);return;}voices[slot].clip=clip;voices[slot].loop=false;voices[slot].volume=.9f;voices[slot].Play();
        }
        public void SetVolume(string bus,float value)
        {
            value=Mathf.Clamp01(value);
            if(bus=="Music"){MusicVolume=value;PlayerPrefs.SetFloat(MusicKey,value);}
            else if(bus=="Ambience"){AmbienceVolume=value;PlayerPrefs.SetFloat(AmbienceKey,value);}
            else if(bus=="SFX"){SfxVolume=value;PlayerPrefs.SetFloat(SfxKey,value);}else throw new ArgumentException(bus);
            ApplyVolumes();PlayerPrefs.Save();
        }
        void ApplyVolumes(){if(!Ready || !started)return;Catalog.mixer.SetFloat("MusicVolumeDb",Db(MusicVolume));Catalog.mixer.SetFloat("AmbienceVolumeDb",Db(AmbienceVolume));Catalog.mixer.SetFloat("SFXVolumeDb",Db(SfxVolume));}
        static float Db(float value)=>value<=.0001f?-80:20*Mathf.Log10(value);
        void Log(string cue,AudioClip clip){var row=new Trace{cue=cue,clip=clip?clip.name:null,realtime=Time.realtimeSinceStartup,dsp=AudioSettings.dspTime,chapter=generation,player=story?story.walker.transform.position:Vector3.zero};if(Events.Count>=1024)Events.RemoveAt(0);Events.Add(row);CueObserved?.Invoke(row);}
        void OnDestroy(){Unbind();if(app){app.PageChanged-=Page;app.SettingsVisibilityChanged-=Settings;}SceneManager.sceneLoaded-=Loaded;SceneManager.sceneUnloaded-=Unloaded;}
    }
}
