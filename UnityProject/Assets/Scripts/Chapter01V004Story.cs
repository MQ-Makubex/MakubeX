using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using MakubeX.Chapter01Integration;
using MakubeX.V004;

namespace MakubeX
{
    // Approved phase-one story, bound to the existing v004 props and physical player.
    public sealed class Chapter01V004Story : MonoBehaviour
    {
        public Chapter01WalkController walker;
        public V004Door door;
        public Transform bag,chair,computer;
        public Vector3 seat,elderStart,elderBesideChair,bagCamera,roomCamera,bootCamera;
        public Material poweredScreenTemplate;
        public string Phase {get;private set;}="explore";
        public string Sequence {get;private set;}
        public int Part {get;private set;}
        public bool ComputerOn {get;private set;}
        public bool ScreenLit=>screenMaterial && screenMaterial.GetFloat("_Powered")>.5f;
        public bool ControlsLocked=>Phase!="explore";
        public string CurrentLine=>subtitle.text;
        public Vector3 BagApproach=>new Vector3(bag.position.x+1.5f,seat.y,bag.position.z);
        public Vector3 ChairApproach=>seat+Vector3.forward*1.35f;
        public bool verificationMovement;
        public event Action<Chapter01SoundEvent> SoundEvent;
        public event Action<bool> DialogueVisibilityChanged;
        public bool HeadPatSoundTriggered {get;private set;}
        Chapter01Controller actorLibrary;
        Chapter01Controller.Character child,elder;
        Material screenMaterial;
        Camera view;
        Transform cameraParent;
        Vector3 cameraLocalPosition;
        Quaternion cameraLocalRotation,bagRotation;
        Text objective,prompt,subtitle,speaker,ending;
        GameObject dialogue;
        float originalSensitivity;
        Text controlsHint;
        public bool useAuthoredLighting;
        bool lightingSaved;
        AmbientMode savedAmbientMode;
        Color savedSky,savedEquator,savedGround;
        SphericalHarmonicsL2 savedProbe;
        float savedReflection,savedExposure,savedShadowLift;
        public bool ReadableNightEnabled {get;private set;}
        void Awake()
        {
            if(!walker || !bag || !chair || !computer || !poweredScreenTemplate)throw new InvalidOperationException("v004 story bindings missing");
            view=walker.view;cameraParent=view.transform.parent;cameraLocalPosition=view.transform.localPosition;cameraLocalRotation=view.transform.localRotation;
            originalSensitivity=walker.lookSensitivity;bagRotation=bag.rotation;
            walker.clickToLook=true;
            walker.showHelp=false;door.showHelp=false;
            var rig=new GameObject("Approved phase-one placeholder actor library");rig.SetActive(false);rig.transform.SetParent(transform,false);
            actorLibrary=rig.AddComponent<Chapter01Controller>();actorLibrary.actorLibraryOnly=true;rig.SetActive(true);actorLibrary.enabled=false;
            child=actorLibrary.CreateImportedSceneActor(false);elder=actorLibrary.CreateImportedSceneActor(true);
            child.root.gameObject.SetActive(false);elder.root.position=elderStart;elder.root.rotation=Quaternion.LookRotation(seat-elderStart,Vector3.up);
            var renderer=computer.GetComponent<Renderer>();screenMaterial=new Material(poweredScreenTemplate);renderer.sharedMaterial=screenMaterial;screenMaterial.SetFloat("_Powered",0);
            BuildHud();RefreshObjective();
        }
        IEnumerator Start()
        {
            while(SceneManager.GetActiveScene()!=gameObject.scene)yield return null;
            if(!useAuthoredLighting)SetReadableNight(true);
        }
        public void SetReadableNight(bool enabled)
        {
            var post=view.GetComponent<V004NightPost>();
            if(!lightingSaved) {
                savedAmbientMode=RenderSettings.ambientMode;savedSky=RenderSettings.ambientSkyColor;
                savedEquator=RenderSettings.ambientEquatorColor;savedGround=RenderSettings.ambientGroundColor;
                savedProbe=RenderSettings.ambientProbe;savedReflection=RenderSettings.reflectionIntensity;
                if(post){savedExposure=post.exposure;savedShadowLift=post.shadowLift;}lightingSaved=true;
            }
            ReadableNightEnabled=enabled;
            RenderSettings.ambientMode=enabled ? AmbientMode.Trilight : savedAmbientMode;
            RenderSettings.ambientSkyColor=enabled ? new Color(.24f,.30f,.43f) : savedSky;
            RenderSettings.ambientEquatorColor=enabled ? new Color(.14f,.185f,.275f) : savedEquator;
            RenderSettings.ambientGroundColor=enabled ? new Color(.075f,.095f,.14f) : savedGround;
            if(enabled) {
                var probe=new SphericalHarmonicsL2();probe.AddAmbientLight(new Color(.12f,.16f,.24f));
                probe.AddDirectionalLight(Vector3.up,new Color(.18f,.22f,.34f),.45f);RenderSettings.ambientProbe=probe;
            } else RenderSettings.ambientProbe=savedProbe;
            RenderSettings.reflectionIntensity=enabled ? .24f : savedReflection;
            if(post){post.exposure=enabled ? savedExposure*1.12f : savedExposure;post.shadowLift=enabled ? .01f : savedShadowLift;}
        }
        void Update()
        {
            if(MakubeXApp.Instance && MakubeXApp.Instance.IsPaused)return;
            if(MakubeXApp.Instance)walker.lookSensitivity=originalSensitivity*MakubeXApp.Instance.MouseSensitivity;
            if(controlsHint)controlsHint.text=walker.LookCaptured ? "移动鼠标观察　方向键移动　上档键快走　E 键互动　退出键菜单" : "单击画面后移动鼠标观察　方向键移动　E 键互动　退出键菜单";
            if(Phase=="explore")
            {
                walker.verificationMode=verificationMovement;
                var target=Part==0?bag.position:seat;
                bool near=FlatDistance(walker.transform.position,target)<(Part==0?1.65f:1.8f);
                prompt.text=near?(Part==0?"[ E ]  查看「間久部」旧袋子":"[ E ]  坐到椅子上"):door && Vector3.Distance(walker.transform.position,door.hinge.position)<3?"[ E ]  开关门":"";
                if(Input.GetKeyDown(KeyCode.E))Interact();
            }
            else if(Phase=="await-boot")
            {
                prompt.text="[ E ]  打开电脑";
                if(Input.GetKeyDown(KeyCode.E))Interact();
            }
            if(ControlsLocked)walker.verificationMode=true;
        }
        public void Interact()
        {
            if(MakubeXApp.Instance && MakubeXApp.Instance.IsPaused)return;
            if(Phase=="await-boot" && Part==2 && !ComputerOn){ComputerOn=true;StartCoroutine(PlayBoot());return;}
            if(Phase!="explore")return;
            if(Part==0 && FlatDistance(walker.transform.position,bag.position)<1.65f)StartCoroutine(PlayBag());
            else if(Part==1 && FlatDistance(walker.transform.position,seat)<1.8f)StartCoroutine(PlayChair());
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        void Begin(string kind,Vector3 camera,Vector3 focus)
        {
            Phase="scene";Sequence=kind;walker.verificationMode=true;door.enabled=false;prompt.text="";
            child.root.gameObject.SetActive(true);child.root.position=walker.transform.position;child.root.rotation=walker.transform.rotation;
            view.transform.SetParent(transform,true);view.transform.position=camera;view.transform.LookAt(focus);view.fieldOfView=62;
        }
        void Say(string who,string text){dialogue.SetActive(true);speaker.text=who;subtitle.text=text;DialogueVisibilityChanged?.Invoke(true);}
        IEnumerator PlayBag()
        {
            Begin("bag",bagCamera,Vector3.Lerp(bag.position,walker.transform.position+Vector3.up*1.2f,.5f));
            SoundEvent?.Invoke(Chapter01SoundEvent.BagInspected);
            child.root.rotation=Quaternion.LookRotation(new Vector3(bag.position.x-child.root.position.x,0,bag.position.z-child.root.position.z));
            Say("马克贝斯","爷爷……这是我的名字吗？");yield return new WaitForSeconds(2.5f);
            Say("源水","不知道。发现你的时候，它就在你身边。");yield return new WaitForSeconds(2.7f);
            Say("源水","过来，坐这儿。");yield return new WaitForSeconds(2.4f);
            bag.rotation=bagRotation;Part=1;Phase="explore";Sequence=null;dialogue.SetActive(false);child.root.gameObject.SetActive(false);
            DialogueVisibilityChanged?.Invoke(false);
            view.transform.SetParent(cameraParent,false);view.transform.localPosition=cameraLocalPosition;view.transform.localRotation=cameraLocalRotation;view.fieldOfView=63;
            walker.verificationMode=verificationMovement;door.enabled=true;RefreshObjective();
        }
        IEnumerator PlayChair()
        {
            Begin("chair",roomCamera,seat+Vector3.up*1.2f);walker.Body.enabled=false;
            var from=walker.transform.position;float t=0;
            while(t<.95f){t+=Time.deltaTime;walker.transform.position=Vector3.Lerp(from,seat,Mathf.SmoothStep(0,1,t/.95f));child.root.position=walker.transform.position;child.root.rotation=Quaternion.Euler(0,180,0);yield return null;}
            walker.transform.position=seat;child.root.position=seat;
            foreach(var go in child.standing)go.SetActive(false);foreach(var go in child.sitting)go.SetActive(true);
            SoundEvent?.Invoke(Chapter01SoundEvent.PlayerSatDown);
            t=0;while(t<2.3f){t+=Time.deltaTime;elder.root.position=Vector3.Lerp(elderStart,elderBesideChair,Mathf.SmoothStep(0,1,t/2.3f));elder.root.LookAt(new Vector3(computer.position.x,elder.root.position.y,computer.position.z));yield return null;}
            elder.arms[1].localRotation=Quaternion.Euler(-30,0,-35);Say("源水","来，打开它。");yield return new WaitForSeconds(2.15f);
            Phase="await-boot";Sequence=null;Part=2;dialogue.SetActive(false);RefreshObjective();
            DialogueVisibilityChanged?.Invoke(false);
            // Screen remains off until a separate key press starts PlayBoot.
        }
        IEnumerator PlayBoot()
        {
            Begin("boot",bootCamera,Vector3.Lerp(computer.position,seat+Vector3.up*1.2f,.4f));
            SoundEvent?.Invoke(Chapter01SoundEvent.CRTPoweredOn);
            child.root.position=seat;child.root.rotation=Quaternion.Euler(0,180,0);
            child.arms[1].localRotation=Quaternion.Euler(-43,0,7);yield return new WaitForSeconds(.75f);screenMaterial.SetFloat("_Powered",1);
            yield return new WaitForSeconds(.85f);Say("马克贝斯","亮了……");yield return new WaitForSeconds(1);
            Say("马克贝斯","它还能做什么？");yield return new WaitForSeconds(2.1f);
            Say("源水","可以做很多。");yield return new WaitForSeconds(1);
            Say("源水","我们慢慢学。");yield return new WaitForSeconds(.8f);
            var arm=elder.arms[1];var local=arm.localPosition;var rotation=arm.localRotation;float t=0;
            while(t<1.9f){t+=Time.deltaTime;float a=Mathf.Sin(Mathf.Clamp01(t/1.9f)*Mathf.PI);arm.position=Vector3.Lerp(elder.root.TransformPoint(local),child.head.position+Vector3.up*.2f,a);arm.rotation=Quaternion.Slerp(elder.root.rotation*rotation,Quaternion.Euler(0,0,90),a);
                if(!HeadPatSoundTriggered && Vector3.Distance(arm.position,child.head.position+Vector3.up*.2f)<.09f){HeadPatSoundTriggered=true;SoundEvent?.Invoke(Chapter01SoundEvent.GrandpaHeadPatStarted);}yield return null;}
            arm.localPosition=local;arm.localRotation=rotation;child.head.localRotation=Quaternion.Euler(-18,28,0);
            Say("马克贝斯 · 回忆","我不知道自己从哪里来。但那一天，我有了家。");yield return new WaitForSeconds(2.9f);
            Phase="done";Sequence=null;Part=3;dialogue.SetActive(false);prompt.text="";objective.text="第一章 · 名字";ending.text="记忆已找回\n正在返回记忆轨迹……";
            DialogueVisibilityChanged?.Invoke(false);SoundEvent?.Invoke(Chapter01SoundEvent.Chapter01Finished);
            if(MakubeXApp.Instance)MakubeXApp.Instance.CompleteChapter(1);
        }
        void RefreshObjective(){objective.text=Part==0?"找到那个写着「間久部」的旧袋子":Part==1?"走到椅子旁，按 E 坐下":"爷爷说：来，打开它。按 E 开机";}
        void BuildHud()
        {
            var go=new GameObject("Chapter 01 story HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=20;
            var scale=go.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1600,900);
            Label(go.transform,"第一章 · 名字",new Rect(.03f,.025f,.4f,.045f),26);
            Label(go.transform,"临时占位人物 / 动画待正式制作",new Rect(.03f,.075f,.45f,.035f),16);
            objective=Label(go.transform,"",new Rect(.53f,.035f,.44f,.07f),22);
            prompt=Label(go.transform,"",new Rect(.25f,.78f,.5f,.07f),24,TextAnchor.MiddleCenter);
            controlsHint=Label(go.transform,"单击画面后移动鼠标观察　方向键移动　E 键互动　退出键菜单",new Rect(.09f,.94f,.82f,.04f),18,TextAnchor.MiddleCenter);
            dialogue=new GameObject("Approved dialogue",typeof(RectTransform),typeof(Image));dialogue.transform.SetParent(go.transform,false);Position((RectTransform)dialogue.transform,new Rect(.13f,.79f,.74f,.14f));dialogue.GetComponent<Image>().color=new Color(.015f,.025f,.04f,.93f);dialogue.GetComponent<Image>().raycastTarget=false;
            speaker=Label(dialogue.transform,"",new Rect(.025f,.08f,.94f,.25f),20);subtitle=Label(dialogue.transform,"",new Rect(.025f,.38f,.94f,.52f),27);
            ending=Label(go.transform,"",new Rect(.2f,.38f,.6f,.2f),34,TextAnchor.MiddleCenter);dialogue.SetActive(false);
        }
        static void Position(RectTransform rt,Rect rect){rt.anchorMin=new Vector2(rect.x,1-rect.yMax);rt.anchorMax=new Vector2(rect.xMax,1-rect.y);rt.offsetMin=rt.offsetMax=Vector2.zero;}
        static Text Label(Transform parent,string value,Rect rect,int size,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var go=new GameObject("Story label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);Position(go.GetComponent<RectTransform>(),rect);
            var text=go.GetComponent<Text>();text.font=MakubeXApp.UiFont;text.text=value;text.fontSize=size;text.color=new Color(.96f,.9f,.78f);text.alignment=align;text.raycastTarget=false;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        void OnDestroy(){if(screenMaterial)Destroy(screenMaterial);}
    }
}
