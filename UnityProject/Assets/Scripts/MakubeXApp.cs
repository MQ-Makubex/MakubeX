using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MakubeX
{
    // The artwork is kept intact. All hit areas live in the same aspect-fitted art rectangle.
    public sealed class MakubeXApp : MonoBehaviour
    {
        public const string ChapterSceneName = "Chapter01_V004_Story";
        [SerializeField] string chapterSceneNameOverride;
        public string ActiveChapterSceneName => string.IsNullOrEmpty(chapterSceneNameOverride)?ChapterSceneName:chapterSceneNameOverride;
        public const string ArtworkRevision = "HD_20261025";
        public const string ProgressSaveKey = "makubex.memory.v1";
        public const string SensitivitySaveKey = "makubex.mouse.sensitivity";
        public static MakubeXApp Instance { get; private set; }
        static Font font;
        public static Font UiFont
        {
            get {
                if (font) return font;
                font = Resources.Load<Font>("Fonts/Chinese");
#if UNITY_WEBGL && !UNITY_EDITOR
                // Browsers cannot read the host's installed fonts. The OFL font ships in Resources.
                if (!font) throw new InvalidOperationException("WebGL 缺少 Resources/Fonts/Chinese 字体资源，请补齐中文字体后重新构建。");
#else
                if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Unicode MS", "Heiti SC", "PingFang SC", "Arial" }, 24);
#endif
                return font;
            }
        }
        public string CurrentPage { get; private set; } = "cover";
        public bool IsPaused { get; private set; }
        public float MouseSensitivity { get; private set; } = 1f;
        public int Completed { get; private set; }
        public int Selected { get; private set; }
        public bool Busy { get; private set; }
        public bool HasAllArtwork => artwork.Count == 3;
        public event Action<string> PageChanged;
        public event Action<bool> SettingsVisibilityChanged;
        public MakubeXAudio Audio {get;private set;}
        readonly Dictionary<string, Texture2D> artwork = new Dictionary<string, Texture2D>();
        readonly string[] titles = { "名字", "雷帝", "朋友", "空出来的位置", "少年王", "无法计算", "世界的真相", "世界之外" };
        // Pixel centres measured on the supplied 1672 x 941 final image, not the old CSS layout.
        readonly Vector2[] centres = { new Vector2(281,273), new Vector2(620,185), new Vector2(768,405),
            new Vector2(1031,220), new Vector2(1294,405), new Vector2(851,626), new Vector2(1158,690), new Vector2(1519,686) };
        readonly Color gold = new Color(.9f, .78f, .59f);
        readonly Color panelColor = new Color(.025f,.055f,.10f,.97f);
        Canvas canvas;
        RectTransform safe, page, settings, modal, letterbox;
        Text loadStatus, loadPercent, selectionLabel, sensitivityLabel, fullscreenLabel;
        Image loadFill;
        Button launch;
        Camera entryCamera;
        string entrySceneName;
        Material mapMaterial;
        bool webStartRequested;
        readonly List<Button> chapterButtons=new List<Button>();
        MemoryMapEntrance mapEntrance;
        Chapter01V004Story currentStory;
        float lastEscapeInput=float.NegativeInfinity;
        public bool IsMapEntering => mapEntrance && mapEntrance.IsPlaying;
        public float MapEntranceProgress => mapEntrance ? mapEntrance.Progress : 1;
#if UNITY_WEBGL && !UNITY_EDITOR
        RectTransform browserFullscreenButton;
        readonly Vector3[] fullscreenCorners=new Vector3[4];
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void MakubeX_SetFullscreenArea(float left,float bottom,float right,float top,int enabled);
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int MakubeX_GetFullscreenState();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int MakubeX_WebStartRequested();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void MakubeX_SetInputState(int page,int paused);
#endif

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            entrySceneName=gameObject.scene.name;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Completed = Mathf.Clamp(PlayerPrefs.GetInt(ProgressSaveKey, 0), 0, 8);
            MouseSensitivity = Mathf.Clamp(PlayerPrefs.GetInt(SensitivitySaveKey,100),20,180) / 100f;
            Audio=GetComponent<MakubeXAudio>();if(!Audio)Audio=gameObject.AddComponent<MakubeXAudio>();
            entryCamera = Camera.main;
            BuildCanvas();
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.stickyCursorLock=false;
            webStartRequested=MakubeX_WebStartRequested()!=0;
#endif
            if(webStartRequested) StartCoroutine(Preload());
            else StartCoroutine(LoadCover());
        }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) HandleEscapeInput();
            // A browser may consume Esc to exit fullscreen without forwarding a key event.
#if UNITY_WEBGL && !UNITY_EDITOR
            SyncBrowserFullscreenArea();
            SyncBrowserInputState();
#endif
            if (fullscreenLabel) fullscreenLabel.text=FullscreenText();
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        void SyncBrowserInputState(){MakubeX_SetInputState(CurrentPage=="game"?1:CurrentPage=="memory-map"?2:0,IsPaused?1:0);}
        void SyncBrowserFullscreenArea()
        {
            if (!browserFullscreenButton || !settings || !IsPaused || modal || Screen.width<=0 || Screen.height<=0) {
                MakubeX_SetFullscreenArea(0,0,0,0,0); return;
            }
            browserFullscreenButton.GetWorldCorners(fullscreenCorners);
            Vector2 lower=RectTransformUtility.WorldToScreenPoint(null,fullscreenCorners[0]);
            Vector2 upper=RectTransformUtility.WorldToScreenPoint(null,fullscreenCorners[2]);
            MakubeX_SetFullscreenArea(lower.x/Screen.width,lower.y/Screen.height,upper.x/Screen.width,upper.y/Screen.height,1);
        }
#endif
        void OnApplicationFocus(bool focused) { if (!focused) SavePreferences(); }
        void OnApplicationPause(bool paused) { if (paused) SavePreferences(); }
        void OnApplicationQuit() { SavePreferences(); }
        static void SavePreferences()
        {
            // PlayerPrefs.Save persists Web builds in IndexedDB. The Web template also enables
            // autoSyncPersistentDataPath; don't rely on OnApplicationQuit in browser tabs.
            PlayerPrefs.Save();
        }
        static string ArtworkUrl(string name)
        {
            string path=Application.streamingAssetsPath.Replace('\\','/').TrimEnd('/')+"/UI/"+ArtworkRevision+"/"+Uri.EscapeDataString(name)+".png";
            // streamingAssetsPath is already an HTTP(S) / jar URL in a Web / Android player.
            // On macOS it is a filesystem path, which UnityWebRequest reads using a file URI.
            return path.Contains("://") || path.StartsWith("jar:",StringComparison.OrdinalIgnoreCase) ? path : new Uri(path).AbsoluteUri;
        }
        IEnumerator ReadArtwork(string name,Action<bool> finished)
        {
            if (artwork.ContainsKey(name)) { finished(true); yield break; }
            using (var request=UnityWebRequestTexture.GetTexture(ArtworkUrl(name),true)) {
                yield return request.SendWebRequest();
                if (request.result!=UnityWebRequest.Result.Success) {
                    Debug.LogError("图片读取失败："+name+".png — "+request.error);
                    finished(false); yield break;
                }
                var texture=DownloadHandlerTexture.GetContent(request);
                if (!texture) { Debug.LogError("图片解码失败："+name+".png"); finished(false); yield break; }
                artwork[name]=texture;
                finished(true);
            }
        }
        IEnumerator LoadCover()
        {
            Busy=true;
            NewPage("cover");
            Label(page,"Loading cover","正在读取首页图片…",new Rect(.25f,.45f,.5f,.08f),28,TextAnchor.MiddleCenter);
            yield return ReadArtwork("cover",_=>{});
            Busy=false;
            if(webStartRequested) StartCoroutine(Preload());
            else ShowCover();
        }
        void OnDestroy()
        {
            if (Instance != this) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            MakubeX_SetFullscreenArea(0,0,0,0,0);
#endif
            Instance = null;
            if (mapMaterial) Destroy(mapMaterial);
            foreach (var item in artwork) if (item.Value) Destroy(item.Value);
        }
        void BuildCanvas()
        {
            var go = new GameObject("MakubeX UGUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            go.transform.SetParent(transform,false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1672,941); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            letterbox=Panel(go.transform,"Letterbox",new Rect(0,0,1,1),Color.black);
            letterbox.GetComponent<Image>().raycastTarget = false;
            safe = Rect(go.transform,"Art Safe Area",new Rect(0,0,1,1));
            var fit = safe.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 1672f/941f;
            if (!EventSystem.current) {
                var es = new GameObject("UGUI EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
                es.transform.SetParent(transform,false);
            }
        }
        void NewPage(string name, string art = null)
        {
            chapterButtons.Clear();
            canvas.enabled = true;
            if(letterbox)letterbox.gameObject.SetActive(true);
            CurrentPage = name;
            if (page) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            page = Panel(safe,name,new Rect(0,0,1,1),new Color(.027f,.055f,.1f));
            if (art != null && artwork.TryGetValue(art,out var texture)) {
                var raw = Rect(page,"Approved Artwork",new Rect(0,0,1,1)).gameObject.AddComponent<RawImage>();
                raw.texture = texture; raw.raycastTarget = false;
                if (art == "memory-map") {
                    if (!mapMaterial) mapMaterial = new Material(Resources.Load<Shader>("Shaders/MemoryMapState"));
                    mapMaterial.SetFloat("_Completed",Completed); raw.material = mapMaterial;
                    mapMaterial.SetFloat("_Entrance",1);
                    raw.gameObject.AddComponent<MemoryMapWaveMesh>();
                }
            } else if (art != null) Label(page,"Missing Artwork","缺少已确认图片："+art+".png",new Rect(.1f,.28f,.8f,.1f),30,TextAnchor.MiddleCenter);
            PageChanged?.Invoke(name);
        }
        public void ShowCover()
        {
            NewPage("cover","cover");
            var start = Button(page,"cover-start",artwork.ContainsKey("cover") ? "" : "开始",
                new Rect(203f/1672,433f/941,275f/1672,51f/941),StartFromCover, true);
            start.gameObject.AddComponent<ArtworkHitArea>();
        }
        public void StartFromCover()
        {
            if (Busy || CurrentPage != "cover") return;
            // The existing Start hotspot also retries a failed cover request.
            if (!artwork.ContainsKey("cover")) { StartCoroutine(LoadCover()); return; }
            StartCoroutine(Preload());
        }
        // A browser cover click is queued if Unity is still reading its cover texture.
        public void StartFromWeb()
        {
            if (webStartRequested) return;
            webStartRequested=true;
            if (!Busy && CurrentPage=="cover") StartFromCover();
        }
        IEnumerator Preload()
        {
            Busy = true;
            NewPage("loading",webStartRequested ? null : "cover");
            if(webStartRequested) {
                MemoryMapEntrance.CreateLoadingFog(page);
                Label(page,"Map preparation","正在准备记忆轨迹…",new Rect(.25f,.83f,.5f,.05f),24,TextAnchor.MiddleCenter);
            }
            BuildProgress(new Rect(.31f,.90f,.38f,.010f),new Rect(.25f,.925f,.5f,.04f));
            string[] names = webStartRequested ? new[] { "cover","memory-map","chapter01-loading" } : new[] { "memory-map","chapter01-loading" };
            for (int i=0;i<names.Length;i++) {
                loadStatus.text = "正在读取 " + names[i] + ".png";
                if (!artwork.ContainsKey(names[i])) {
                    bool success=false;
                    yield return ReadArtwork(names[i],loaded=>success=loaded);
                    if (!success) {
                        loadStatus.text = "读取失败："+names[i]+".png"; Busy=false;
                        Button(page,"retry-assets","返回首页重试",new Rect(.4f,.67f,.2f,.06f),ShowCover); yield break;
                    }
                }
                SetProgress((i+1f)/names.Length); yield return null;
            }
            Busy=false; Selected=Mathf.Min(Completed,7); ShowMap();
        }
        void BuildProgress(Rect track,Rect status)
        {
            var rail = Panel(page,"Actual loading progress",track,new Color(.035f,.06f,.1f,1));
            loadFill = Panel(rail,"Fill",new Rect(0,0,0,1),gold).GetComponent<Image>();
            loadStatus = Label(page,"Loading status","",status,21,TextAnchor.MiddleCenter);
            loadPercent = Label(page,"Loading percent","0%",new Rect(status.xMax-.08f,status.y,status.width*.16f,status.height),21,TextAnchor.MiddleRight);
        }
        void SetProgress(float value)
        {
            loadFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value),1);
            loadPercent.text = Mathf.RoundToInt(value*100)+"%";
        }
        public void SelectChapter(int index)
        {
            if (CurrentPage != "memory-map" || index<0 || index>7 || index>Completed || IsPaused || Busy) return;
            Selected=index; RefreshChapterSelection();
        }
        public void ActivateChapter(int index)
        {
            if(CurrentPage!="memory-map" || Busy || IsPaused || index<0 || index>Completed || index>7)return;
            SelectChapter(index);
            if(index==0) LaunchSelectedChapter();
        }
        [UnityEngine.Scripting.Preserve]
        public void ActivateChapterFromWeb(string chapter)
        {
            if(int.TryParse(chapter,out var index))ActivateChapter(index);
        }
        void RefreshChapterSelection()
        {
            for(int i=0;i<chapterButtons.Count;i++) {
                var colors=chapterButtons[i].colors;
                colors.normalColor=i==Selected ? new Color(1,.86f,.55f,.30f) : new Color(1,.86f,.55f,0);
                chapterButtons[i].colors=colors;
            }
            if(selectionLabel)selectionLabel.text=(Selected+1).ToString("00")+" · "+titles[Selected];
            if(launch) {
                launch.interactable=Selected==0;
                launch.GetComponentInChildren<Text>().text=Selected==0?(Completed>0?"再次回忆":"开始回忆"):"已解锁 · 制作中";
            }
        }
        void ShowMap()
        {
            NewPage("memory-map","memory-map");
            var controls=Rect(page,"Chapter controls",new Rect(0,0,1,1));
            var controlsVisibility=controls.gameObject.AddComponent<CanvasGroup>();
            for (int i=0;i<8;i++) {
                int index=i;
                Vector2 p=centres[i];
                var target=Rect(controls,"node-"+(i+1).ToString("00"),new Rect((p.x-92)/1672,(p.y-92)/941,184f/1672,184f/941));
                var glow=target.gameObject.AddComponent<CircularChapterHighlight>();
                var b=target.gameObject.AddComponent<Button>();b.targetGraphic=glow;
                var palette=b.colors;palette.normalColor=new Color(1,.86f,.55f,0);
                palette.highlightedColor=new Color(1,.87f,.61f,.5f);palette.pressedColor=new Color(1,.9f,.68f,.7f);
                palette.disabledColor=new Color(1,1,1,0);palette.fadeDuration=.12f;b.colors=palette;
                b.navigation=new Navigation{mode=Navigation.Mode.None};
                b.onClick.AddListener(()=>SelectChapter(index));
                target.gameObject.AddComponent<ChapterNodeGesture>().Initialize(this,index);
                chapterButtons.Add(b);
                b.gameObject.AddComponent<ArtworkHitArea>().circular=true;
                b.interactable=i<=Completed;
                var status=i<Completed ? "已完成" : i==Completed ? (i==0?"可进入":"已解锁 · 制作中") : "未解锁 · 制作中";
                Label(controls,"status-"+(i+1),status,new Rect((p.x-90)/1672,(p.y+72)/941,180f/1672,26f/941),15,TextAnchor.MiddleCenter,
                    i<Completed?new Color(1,.84f,.45f):i==Completed?gold:new Color(.58f,.64f,.7f),true);
                // The original 02 lock is baked in. Replace only its small lock glyph when it becomes available.
                if(i==1 && Completed>=1) {
                    Panel(controls,"02 unlocked glyph backing",new Rect(761f/1672,101f/941,27f/1672,29f/941),new Color(.045f,.075f,.12f));
                    Label(controls,"02 unlocked glyph","✓",new Rect(761f/1672,101f/941,27f/1672,29f/941),19,TextAnchor.MiddleCenter,gold);
                }
            }
            Label(controls,"Map progress","记忆已找回 "+Completed+" / 8",new Rect(.03f,.945f,.2f,.03f),16,TextAnchor.MiddleLeft);
#if UNITY_WEBGL && !UNITY_EDITOR
            Label(controls,"Settings hint","退出键　菜单",new Rect(.65f,.03f,.32f,.035f),18,TextAnchor.MiddleRight);
#else
            Label(controls,"Settings hint","退出键　菜单",new Rect(.85f,.03f,.12f,.035f),18,TextAnchor.MiddleRight);
#endif
            selectionLabel=Label(controls,"Selected chapter",(Selected+1).ToString("00")+" · "+titles[Selected],new Rect(.39f,.935f,.23f,.04f),20,TextAnchor.MiddleCenter, gold,true);
            Label(controls,"Double click hint","双击章节进入",new Rect(.235f,.945f,.15f,.03f),16,TextAnchor.MiddleCenter,gold,true);
            launch=Button(controls,"chapter-launch",Selected==0?(Completed>0?"再次回忆":"开始回忆"):"已解锁 · 制作中",new Rect(.625f,.932f,.18f,.05f),LaunchSelectedChapter);
            launch.interactable=Selected==0;
            RefreshChapterSelection();
            Busy=true;
            mapEntrance=page.gameObject.AddComponent<MemoryMapEntrance>();
            mapEntrance.Begin(mapMaterial,controlsVisibility,()=>{if(CurrentPage=="memory-map")Busy=false;});
        }
        public void LaunchSelectedChapter()
        {
            if (Busy || IsPaused || CurrentPage!="memory-map" || Selected!=0) return;
            StartCoroutine(LoadChapter());
        }
        IEnumerator LoadChapter()
        {
            Busy=true;
            NewPage("chapter-loading","chapter01-loading");
            // Cover the embedded sample 37% and sample bar with the actual Unity load operation.
            Panel(page,"Replace baked sample progress",new Rect(538f/1672,815f/941,595f/1672,70f/941),new Color(.025f,.05f,.09f,.99f));
            BuildProgress(new Rect(548f/1672,825f/941,574f/1672,11f/941),new Rect(566f/1672,846f/941,540f/1672,32f/941));
            loadStatus.text="正在准备第一章场景…";
            var start=Time.realtimeSinceStartup;
            if (!Application.CanStreamedLevelBeLoaded(ActiveChapterSceneName)) {
                loadStatus.text="第一章场景缺失，请重新生成场景"; Busy=false;
                Button(page,"loading-back","返回记忆轨迹",new Rect(.43f,.94f,.14f,.045f),ShowMap); yield break;
            }
            var operation=SceneManager.LoadSceneAsync(ActiveChapterSceneName,LoadSceneMode.Additive);
            operation.allowSceneActivation=false;
            while(operation.progress<.9f) { SetProgress(operation.progress/.9f); yield return null; }
            SetProgress(1); loadStatus.text="场景准备完成";
            while(Time.realtimeSinceStartup-start<1.1f) yield return null;
            if (entryCamera) entryCamera.enabled=false;
            if (entryCamera && entryCamera.TryGetComponent<AudioListener>(out var entryListener)) entryListener.enabled=false;
            operation.allowSceneActivation=true;
            yield return operation;
            var chapter=SceneManager.GetSceneByName(ActiveChapterSceneName);
            SceneManager.SetActiveScene(chapter);
            currentStory=UnityEngine.Object.FindAnyObjectByType<Chapter01V004Story>();
            CurrentPage="game"; page.gameObject.SetActive(false); canvas.enabled=false; Busy=false;
        }
        public void CompleteChapter(int chapter)
        {
            if (chapter!=1 || CurrentPage!="game" || Busy) return;
            Completed=Mathf.Max(Completed,1);
            PlayerPrefs.SetInt(ProgressSaveKey,Completed); SavePreferences();
            Busy=true; StartCoroutine(ReturnAfterEnding());
        }
        IEnumerator ReturnAfterEnding() { yield return new WaitForSeconds(2.1f); Busy=false; OpenMap(); }
        public void OpenMap()
        {
            if (CurrentPage=="chapter-loading" || CurrentPage=="loading") return;
            if(currentStory)currentStory.walker.ReleaseLook();currentStory=null;
            CloseSettings(); StopAllCoroutines(); Busy=false;
            var chapter=SceneManager.GetSceneByName(ActiveChapterSceneName);
            if(chapter.IsValid() && chapter.isLoaded) {
                var entry=SceneManager.GetSceneByName(entrySceneName);
                if(entry.IsValid()) SceneManager.SetActiveScene(entry);
                foreach(var root in chapter.GetRootGameObjects()) root.SetActive(false);
                SceneManager.UnloadSceneAsync(chapter);
            }
            if(entryCamera) entryCamera.enabled=true;
            if(entryCamera && entryCamera.TryGetComponent<AudioListener>(out var listener)) listener.enabled=true;
            Selected=Mathf.Min(Completed,7); ShowMap();
        }
        public void HandleEscape()
        {
            if(modal) { DismissModal(); return; }
            if(IsPaused) { CloseSettings(); return; }
            if((CurrentPage=="memory-map" || CurrentPage=="game") && !Busy) OpenSettings();
        }
        void HandleEscapeInput(){if(Time.unscaledTime-lastEscapeInput<.18f)return;lastEscapeInput=Time.unscaledTime;HandleEscape();}
        [UnityEngine.Scripting.Preserve]
        public void BrowserEscapePressed(){HandleEscapeInput();}
        void OpenSettings()
        {
            IsPaused=true; Time.timeScale=0;canvas.enabled=true;
            if(letterbox)letterbox.gameObject.SetActive(CurrentPage!="game");
#if UNITY_WEBGL && !UNITY_EDITOR
            SyncBrowserInputState();
#endif
            if(currentStory)currentStory.walker.ReleaseLook();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            SettingsVisibilityChanged?.Invoke(true);
            settings=Panel(safe,"Settings",new Rect(0,0,1,1),new Color(.012f,.025f,.045f,.15f));
            Panel(settings,"Translucent panel",new Rect(.10f,.08f,.8f,.84f),new Color(.018f,.038f,.07f,.58f));
            Panel(settings,"Sidebar",new Rect(.13f,.24f,.23f,.58f),new Color(.018f,.035f,.07f,.18f));
            Panel(settings,"Divider",new Rect(.373f,.24f,.001f,.58f),new Color(.9f,.78f,.59f,.25f));
            Label(settings,"Settings title","设置",new Rect(.15f,.12f,.5f,.07f),34);
            var fs=Button(settings,"settings-fullscreen",FullscreenText(),new Rect(.16f,.29f,.18f,.06f),ToggleFullscreen);
#if UNITY_WEBGL && !UNITY_EDITOR
            fs.onClick.RemoveListener(ToggleFullscreen);
            browserFullscreenButton=fs.GetComponent<RectTransform>();
#endif
#if UNITY_EDITOR
            fs.interactable=false;
#endif
            fullscreenLabel=fs.GetComponentInChildren<Text>();
            Button(settings,"settings-return-map","返回记忆轨迹",new Rect(.16f,.39f,.18f,.06f),OpenMap);
            Button(settings,"settings-reset","清除进度",new Rect(.16f,.49f,.18f,.06f),AskReset);
            Label(settings,"Language title","语言",new Rect(.16f,.61f,.18f,.04f),20);
            Label(settings,"Language value","中文",new Rect(.16f,.66f,.18f,.05f),24);
            Label(settings,"Controls title","操作",new Rect(.41f,.24f,.43f,.05f),24);
            string controls="方向键移动，上档键快走，E 键互动。\n点击画面后移动鼠标，转动视线。\n退出键打开或关闭菜单。";
            Label(settings,"Controls",controls,new Rect(.41f,.315f,.43f,.13f),21);
            sensitivityLabel=Label(settings,"Sensitivity value","视角灵敏度  "+Mathf.RoundToInt(MouseSensitivity*100)+"%",new Rect(.41f,.475f,.43f,.045f),22);
            BuildSensitivity(settings,new Rect(.41f,.54f,.43f,.03f));
            Label(settings,"Audio title","声音",new Rect(.41f,.61f,.43f,.05f),24);
            if(Audio && Audio.Ready) {
                BuildAudioVolume(settings,"Music","音乐",.68f,Audio.MusicVolume);
                BuildAudioVolume(settings,"Ambience","环境音",.73f,Audio.AmbienceVolume);
                BuildAudioVolume(settings,"SFX","音效",.78f,Audio.SfxVolume);
            } else Label(settings,"Audio unavailable","音频资源缺失。",new Rect(.41f,.68f,.43f,.06f),20);
            Button(settings,"settings-close",CurrentPage=="game"?"返回游戏":"返回图谱",new Rect(.40f,.855f,.20f,.05f),CloseSettings);
            foreach(var text in settings.GetComponentsInChildren<Text>()) {
                var shadow=text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.7f);shadow.effectDistance=new Vector2(1,-1);
            }
            EventSystem.current.SetSelectedGameObject(null);
        }
        void BuildSensitivity(Transform parent,Rect bounds)
        {
            var root=Rect(parent,"settings-mouse",bounds); var slider=root.gameObject.AddComponent<Slider>();
            Panel(root,"Track",new Rect(0,.42f,1,.16f),new Color(.16f,.22f,.29f));
            var fillArea=Rect(root,"Fill Area",new Rect(.01f,.42f,.98f,.16f));
            var fill=Panel(fillArea,"Fill",new Rect(0,.3f,1,.4f),gold);
            var handleArea=Rect(root,"Handle Area",new Rect(.01f,0,.98f,1));
            var handle=Panel(handleArea,"Handle",new Rect(0,0,0,1),Color.white);
            handle.sizeDelta=new Vector2(8,0);
            slider.fillRect=fill; slider.handleRect=handle; slider.targetGraphic=handle.GetComponent<Image>();
            slider.minValue=2; slider.maxValue=18; slider.wholeNumbers=true; slider.value=Mathf.RoundToInt(MouseSensitivity*10);
            slider.onValueChanged.AddListener(v=>{
                MouseSensitivity=v/10; sensitivityLabel.text="视角灵敏度  "+Mathf.RoundToInt(v*10)+"%";
                PlayerPrefs.SetInt(SensitivitySaveKey,Mathf.RoundToInt(v*10)); SavePreferences();
            });
        }
        void BuildAudioVolume(Transform parent,string bus,string title,float y,float initial)
        {
            Label(parent,bus+" audio label",title,new Rect(.41f,y,.12f,.035f),20);
            var value=Label(parent,bus+" audio value",Mathf.RoundToInt(initial*100)+"%",new Rect(.81f,y,.06f,.035f),18,TextAnchor.MiddleRight);
            var root=Rect(parent,"settings-audio-"+bus,new Rect(.54f,y,.24f,.035f));var slider=root.gameObject.AddComponent<Slider>();
            Panel(root,"Track",new Rect(0,.42f,1,.16f),new Color(.16f,.22f,.29f));var fillArea=Rect(root,"Fill Area",new Rect(.01f,.42f,.98f,.16f));var fill=Panel(fillArea,"Fill",new Rect(0,.3f,1,.4f),gold);
            var handleArea=Rect(root,"Handle Area",new Rect(.01f,0,.98f,1));var handle=Panel(handleArea,"Handle",new Rect(0,0,0,1),Color.white);handle.sizeDelta=new Vector2(8,0);
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=100;slider.wholeNumbers=true;slider.value=Mathf.RoundToInt(initial*100);
            slider.onValueChanged.AddListener(v=>{Audio.SetVolume(bus,v/100);value.text=Mathf.RoundToInt(v)+"%";});
        }
        string FullscreenText() {
#if UNITY_EDITOR
            return "全屏 · 独立运行时可用";
#elif UNITY_WEBGL
            switch(MakubeX_GetFullscreenState()) {
                case 1: return "退出全屏模式";
                case 2: return "正在请求全屏…";
                case -1: return "浏览器不支持全屏";
                case -2: return "全屏被拒绝 · 点击重试";
                default: return "进入全屏模式";
            }
#else
            return Screen.fullScreen?"退出全屏模式":"进入全屏模式";
#endif
        }
        void ToggleFullscreen() {
#if UNITY_WEBGL && !UNITY_EDITOR
            // The native DOM handler in MakubeXFullscreen.jslib owns Web fullscreen requests.
            return;
#else
            Screen.fullScreenMode=Screen.fullScreen?FullScreenMode.Windowed:FullScreenMode.FullScreenWindow;
            StartCoroutine(UpdateFullscreenLabel());
#endif
        }
        IEnumerator UpdateFullscreenLabel() { yield return null; yield return null; if(fullscreenLabel) fullscreenLabel.text=FullscreenText(); }
        public void CloseSettings()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            browserFullscreenButton=null;
            MakubeX_SetFullscreenArea(0,0,0,0,0);
#endif
            DismissModal();
            if(settings) { settings.gameObject.SetActive(false); Destroy(settings.gameObject); settings=null; }
            IsPaused=false; Time.timeScale=1;
            if(canvas)canvas.enabled=CurrentPage!="game";
            if(letterbox)letterbox.gameObject.SetActive(CurrentPage!="game");
#if UNITY_WEBGL && !UNITY_EDITOR
            SyncBrowserInputState();
#endif
            SettingsVisibilityChanged?.Invoke(false);
            if(EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }
        void AskReset()
        {
            Dialog("确定清除八章的完成进度吗？此操作无法撤销。",true);
        }
        public void ConfirmResetProgress()
        {
            if(!IsPaused || !modal) return;
            PlayerPrefs.DeleteKey(ProgressSaveKey); SavePreferences(); Completed=0; Selected=0;
            OpenMap();
        }
        void Notice(string message) { Dialog(message,false); }
        void Dialog(string message,bool reset)
        {
            DismissModal(); modal=Panel(safe,"Confirmation",new Rect(0,0,1,1),new Color(0,0,0,.65f));
            var box=Panel(modal,"Dialog",new Rect(.27f,.34f,.46f,.28f),panelColor);
            Label(box,"Message",message,new Rect(.07f,.12f,.86f,.4f),24,TextAnchor.MiddleCenter);
            Button(box,"dialog-cancel",reset?"取消":"知道了",new Rect(.1f,.65f,reset?.35f:.8f,.23f),DismissModal);
            if(reset) Button(box,"dialog-confirm","确认清除",new Rect(.55f,.65f,.35f,.23f),ConfirmResetProgress);
        }
        void DismissModal() { if(modal) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); modal=null; } }

        // Normalized top-left rectangles keep art, UI and hit areas in one coordinate system.
        static RectTransform Rect(Transform parent,string name,Rect r)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin=new Vector2(r.x,1-r.yMax); rt.anchorMax=new Vector2(r.xMax,1-r.y);
            rt.offsetMin=rt.offsetMax=Vector2.zero; return rt;
        }
        static RectTransform Panel(Transform parent,string name,Rect r,Color color)
        { var rt=Rect(parent,name,r); rt.gameObject.AddComponent<Image>().color=color; return rt; }
        Text Label(Transform parent,string name,string content,Rect r,int size,TextAnchor align=TextAnchor.MiddleLeft,Color? color=null,bool backing=false)
        {
            var rt=Rect(parent,name,r);
            if(backing) {
                var background=rt.gameObject.AddComponent<Image>();
                background.color=new Color(.02f,.04f,.08f,.9f); background.raycastTarget=false;
                rt=Rect(rt,"Text",new Rect(0,0,1,1));
            }
            var text=rt.gameObject.AddComponent<Text>(); text.font=UiFont; text.text=content; text.fontSize=size;
            text.color=color??gold; text.alignment=align; text.raycastTarget=false; text.supportRichText=false;
            text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate; return text;
        }
        Button Button(Transform parent,string name,string title,Rect r,UnityEngine.Events.UnityAction action,bool transparent=false)
        {
            var rt=Panel(parent,name,r,transparent?Color.white:new Color(.07f,.12f,.19f,.97f));
            var button=rt.gameObject.AddComponent<Button>(); button.targetGraphic=rt.GetComponent<Image>();
            var c=button.colors; c.normalColor=transparent?new Color(1,1,1,0):Color.white;
            c.highlightedColor=transparent?new Color(1,.87f,.61f,.17f):new Color(1.18f,1.15f,1.1f);
            c.pressedColor=transparent?new Color(1,.87f,.61f,.3f):new Color(.7f,.7f,.7f);
            c.disabledColor=transparent?new Color(1,1,1,0):new Color(.55f,.55f,.55f); button.colors=c;
            if(transparent) button.targetGraphic.CrossFadeColor(c.normalColor,0,true,true);
            button.navigation=new Navigation{mode=Navigation.Mode.None};
            if(title.Length>0) Label(rt,"Label",title,new Rect(.025f,0,.95f,1),21,TextAnchor.MiddleCenter);
            button.onClick.AddListener(action); return button;
        }
    }
    public sealed class ArtworkHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool circular;
        public bool IsRaycastLocationValid(Vector2 screenPoint,Camera eventCamera)
        {
            if(!circular) return true;
            var rt=(RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt,screenPoint,eventCamera,out var p);
            p-=rt.rect.center;
            return (p.x*p.x/(rt.rect.width*rt.rect.width)+p.y*p.y/(rt.rect.height*rt.rect.height))*4<=1;
        }
    }
}
