#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MakubeX.Verification;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MakubeX.Editor
{
    /// <summary>
    /// Runs the real UI events and chapter interaction state machine without skipping dialogue,
    /// changing timeScale, or calling CompleteChapter. A second Play session verifies disk progress.
    /// </summary>
    [InitializeOnLoad]
    public static class VerificationRunner
    {
        const string ProgressKey = "makubex.memory.v1";
        const string BatchStateKey = "MakubeX.Verification.BatchState";
        const string BatchStartedKey = "MakubeX.Verification.BatchStarted";
        static string Root { get { return Path.GetFullPath("Verification"); } }
        static string BackupPath { get { return Path.Combine(Root, "playerprefs-backup.json"); } }
        static string StepsPath { get { return Path.Combine(Root, "steps.json"); } }

        [Serializable] public sealed class PrefBackup { public bool existed, restored, sensitivityExisted; public int value, sensitivityValue; }
        [Serializable] public sealed class Evidence
        {
            public string status, startedUtc, finishedUtc, error, captureMode;
            public List<Step> steps = new List<Step>();
        }
        [Serializable] public sealed class Step
        {
            public string action, utc, page, chapterPhase, sequence, screenshot;
            public int frame, completed, part;
            public float realtime, timeScale;
            public bool paused, controlsLocked, computerOn, screenLit;
            public Vector3 playerPosition;
        }

        static Evidence evidence;
        static UnityEngine.Object app, chapter;
        static VerificationCaptureBridge capture;
        static int stage, substage, nextActionFrame;
        static double entered;
        static float runStart;
        static bool active, captureBusy, resume;
        static string status = "idle";
        static Vector3 previousPosition;
        public static string Status { get { return status; } }

        static VerificationRunner() { EditorApplication.update += Tick; EditorApplication.update += TickBatch; }

        /// <summary>First process: use with -batchmode -executeMethod and without -quit.</summary>
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch requires an Editor launched with -batchmode.");
            try
            {
                Prepare();
                SessionState.SetFloat(BatchStartedKey, (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(BatchStateKey, "wait-first-play");
                EditorApplication.isPlaying = true;
                Debug.Log("MAKUBEX_BATCH_VERIFICATION_STARTED: real-time simulation; batch offscreen capture at 1600x900.");
            }
            catch (Exception exception) { BatchFailure(exception.ToString()); }
        }

        /// <summary>Second process: reads the prior process's on-disk progress and restores the original user values.</summary>
        public static void ResumeBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("ResumeBatch requires an Editor launched with -batchmode.");
            try
            {
                LoadEvidence();
                if (evidence.status != "awaiting-restart") throw new InvalidOperationException("The first batch process has not completed.");
                SessionState.SetFloat(BatchStartedKey, (float)EditorApplication.timeSinceStartup);
                SessionState.SetString(BatchStateKey, "wait-second-play");
                EditorSceneManager.OpenScene(ProjectSetup.EntryScene, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
                Debug.Log("MAKUBEX_BATCH_RESTART_PROCESS_STARTED: reloading persisted PlayerPrefs from disk.");
            }
            catch (Exception exception) { BatchFailure(exception.ToString()); }
        }

        static void TickBatch()
        {
            string batchState = SessionState.GetString(BatchStateKey, "");
            if (string.IsNullOrEmpty(batchState)) return;
            try
            {
                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(BatchStartedKey, 0) > 480)
                    throw new TimeoutException("Batch verification exceeded 480 seconds in " + batchState);
                if (status == "failed") { BatchFailure(evidence != null ? evidence.error : "Verification failed."); return; }
                EditorApplication.QueuePlayerLoopUpdate();
                switch (batchState)
                {
                    case "wait-first-play":
                        if (!EditorApplication.isPlaying || Time.frameCount < 3 || Find("MakubeX.MakubeXApp") == null) return;
                        Run();
                        SessionState.SetString(BatchStateKey, "first-running");
                        break;
                    case "first-running":
                        if (status != "awaiting-restart") return;
                        PlayerPrefs.Save();
                        SessionState.SetString(BatchStateKey, "");
                        Debug.Log("MAKUBEX_BATCH_STAGE1_OK: process exiting with progress=1; start a fresh Editor process with ResumeBatch. " + StepsPath);
                        EditorApplication.Exit(0);
                        break;
                    case "wait-second-play":
                        if (!EditorApplication.isPlaying || Time.frameCount < 3 || Find("MakubeX.MakubeXApp") == null) return;
                        VerifyRestartAndRestore();
                        SessionState.SetString(BatchStateKey, "second-running");
                        break;
                    case "second-running":
                        if (status != "passed") return;
                        SessionState.SetString(BatchStateKey, "");
                        Debug.Log("MAKUBEX_BATCH_VERIFICATION_OK: " + StepsPath);
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception exception) { BatchFailure(exception.ToString()); }
        }

        static void BatchFailure(string reason)
        {
            active = false;
            captureBusy = false;
            Application.logMessageReceived -= OnLog;
            SessionState.SetString(BatchStateKey, "");
            status = "failed";
            try
            {
                if (evidence == null && File.Exists(StepsPath)) LoadEvidence();
                if (evidence == null) evidence = new Evidence { startedUtc = DateTime.UtcNow.ToString("o") };
                evidence.status = "failed"; evidence.error = reason; evidence.finishedUtc = DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(Root);
                File.WriteAllText(StepsPath, JsonUtility.ToJson(evidence, true));
                RestoreOriginalProgress();
            }
            catch (Exception cleanupException) { Debug.LogError("Batch verification cleanup failed: " + cleanupException); }
            Debug.LogError("MAKUBEX_BATCH_VERIFICATION_FAILED: " + reason);
            EditorApplication.Exit(1);
        }

        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Prepare requires Edit mode.");
            Directory.CreateDirectory(Root);
            if (File.Exists(BackupPath))
            {
                PrefBackup previous = JsonUtility.FromJson<PrefBackup>(File.ReadAllText(BackupPath));
                if (previous != null && !previous.restored)
                    throw new InvalidOperationException("A verification backup is still pending. Call RestoreOriginalProgress before preparing again.");
            }
            var backup = new PrefBackup { existed = PlayerPrefs.HasKey(ProgressKey), value = PlayerPrefs.GetInt(ProgressKey, 0),
                sensitivityExisted = PlayerPrefs.HasKey("makubex.mouse.sensitivity"), sensitivityValue = PlayerPrefs.GetInt("makubex.mouse.sensitivity", 100) };
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, true));
            PlayerPrefs.DeleteKey(ProgressKey);
            PlayerPrefs.Save();
            evidence = new Evidence { status = "prepared", startedUtc = DateTime.UtcNow.ToString("o"),
                captureMode = Application.isBatchMode ? "batch offscreen capture: current game Camera.Render, actual runtime canvases temporarily ScreenSpaceCamera, 1600x900" : "Game View ScreenCapture at WaitForEndOfFrame" };
            status = "prepared";
            Record("Prepared default progress key; original value backed up");
            EditorSceneManager.OpenScene(ProjectSetup.EntryScene, OpenSceneMode.Single);
        }

        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode, then call Run.");
            LoadEvidence();
            if (evidence.status != "prepared") throw new InvalidOperationException("Call Prepare in Edit mode first.");
            Begin(false);
        }

        public static void VerifyRestartAndRestore()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Restart Play mode, then call VerifyRestartAndRestore.");
            LoadEvidence();
            if (evidence.status != "awaiting-restart") throw new InvalidOperationException("The first verification pass has not completed.");
            Begin(true);
        }

        static void Begin(bool restart)
        {
            resume = restart;
            app = Find("MakubeX.MakubeXApp");
            if (app == null) throw new InvalidOperationException("Entry scene has no live MakubeXApp.");
            Application.runInBackground = true;
            Time.timeScale = 1;
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (!Application.isBatchMode && gameViewType != null) { EditorWindow gameView = EditorWindow.GetWindow(gameViewType); gameView.Show(); gameView.Focus(); }
            runStart = Time.realtimeSinceStartup;
            stage = 0;
            substage = 0;
            WaitForRenderedFrames();
            entered = EditorApplication.timeSinceStartup;
            status = restart ? "running-restart-verification" : "running";
            evidence.status = status;
            active = true;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            var bridge = new GameObject("MakubeX verification capture — Editor only");
            UnityEngine.Object.DontDestroyOnLoad(bridge);
            capture = bridge.AddComponent<VerificationCaptureBridge>();
            Record(restart ? "Second session started; verify persisted default progress" : "First session started");
        }

        static void Tick()
        {
            if (!active) return;
            if (!EditorApplication.isPlaying)
            {
                Fail("Play mode stopped before the current verification pass completed.");
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - entered > 120)
            {
                Fail("Timed out at stage " + stage + "; status " + status + ". Game View may not be advancing.");
                return;
            }
            if (captureBusy || Time.frameCount < nextActionFrame) return;
            try
            {
                if (resume) TickRestart();
                else TickFlow();
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        static void TickFlow()
        {
            switch (stage)
            {
                case 0:
                    Require(Page == "cover", "Entry starts at cover");
                    Require(Int(app, "Completed") == 0, "Initial progress is empty");
                    Shot("01-cover"); Next(); break;
                case 1:
                    Press("cover-start");
                    Require(Page == "loading", "Start enters resource loading");
                    Record("UGUI cover-start click enters resource loading");
                    Shot("02-resource-loading"); Next(); break;
                case 2:
                    if (Page != "memory-map") return;
                    Require(Int(app, "Completed") == 0, "Resource loading preserves empty progress");
                    for (int i = 2; i <= 8; i++) Require(!GetButton("node-" + i.ToString("00")).IsInteractable(), "Only chapter 01 is initially unlocked");
                    Record("Resource load completes into memory map");
                    Shot("03-memory-map-initial"); Next(); break;
                case 3:
                    if (substage == 0)
                    {
                        Call(app, "HandleEscape");
                        Require(Bool(app, "IsPaused"), "Esc opens settings from map");
                        Record("Esc opened settings; wait two actual simulation/render frames before settings pointer events");
                        substage = 1;
                        WaitForRenderedFrames();
                        return;
                    }
                    if (substage == 1)
                    {
                        foreach(var bus in new[]{"Music","Ambience","SFX"})
                            Require(GameObject.Find("settings-audio-"+bus).GetComponent<Slider>() != null, bus+" audio volume control is available");
                        Require(GetButton("language-en").GetComponentInChildren<Text>().text.Contains("未接入"), "English is explicitly marked unavailable");
                        var slider = GameObject.Find("settings-mouse").GetComponent<Slider>();
                        float sliderValue = Mathf.Approximately(slider.value, 12) ? 8 : 12;
                        slider.value = sliderValue;
                        Require(Mathf.Approximately(Convert.ToSingle(Get(app, "MouseSensitivity")), sliderValue / 10), "Actual slider event updates mouse sensitivity");
                        Require(PlayerPrefs.GetInt("makubex.mouse.sensitivity", 0) == Mathf.RoundToInt(sliderValue * 10), "Mouse sensitivity persists through slider event");
                        Press("language-en");
                        Require(GameObject.Find("Confirmation") != null, "Unconnected English shows an explanatory notice");
                        Record("English notice created; wait two actual frames before its dismiss pointer event");
                        substage = 2;
                        WaitForRenderedFrames();
                        return;
                    }
                    Press("dialog-cancel");
                    Record("Esc opens settings; real slider updates sensitivity; English and audio explicitly unavailable");
                    Shot("04-settings"); Next(); break;
                case 4:
                    Call(app, "HandleEscape");
                    Require(!Bool(app, "IsPaused"), "Second Esc closes settings");
                    Press("node-01");
                    Require(Page == "memory-map", "Selecting chapter keeps map until launch");
                    Record("Second Esc closes settings; UGUI node-01 selects chapter");
                    Shot("05-chapter01-selected"); Next(); break;
                case 5:
                    Press("chapter-launch");
                    Require(Page == "chapter-loading", "Launch enters dedicated chapter loading page");
                    Record("UGUI chapter-launch click enters chapter-loading");
                    Shot("06-chapter01-loading"); Next(); break;
                case 6:
                    if (Page != "game") return;
                    chapter = Find("MakubeX.Chapter01Controller");
                    if (chapter == null) return;
                    Require(Int(chapter, "Part") == 0 && Text(chapter, "Phase") == "explore", "Chapter starts in free exploration");
                    Require(!Bool(chapter, "ControlsLocked"), "Free exploration controls are unlocked");
                    Record("Dedicated loading finishes into Chapter01; free exploration enabled");
                    Shot("07-chapter01-explore"); Next(); break;
                case 7:
                    previousPosition = Vector(chapter, "PlayerPosition");
                    Call(chapter, "Move", new Vector2(1, 0), .2f);
                    Require(Vector3.Distance(Vector(chapter, "PlayerPosition"), previousPosition) > .01f, "Exploration Move changes position");
                    Transform player = (Transform)Get(chapter, "Player");
                    player.position = new Vector3(0, 0, 2.6f);
                    Call(chapter, "Interact");
                    Require(Int(chapter, "Part") == 0 && Text(chapter, "Phase") == "explore", "E outside bag distance does not start dialogue");
                    player.position = Vector(chapter, "BagSpot");
                    Call(chapter, "Interact");
                    Require(Text(chapter, "Sequence") == "bag" && Bool(chapter, "ControlsLocked"), "E at bag starts automatic dialogue and locks controls");
                    previousPosition = Vector(chapter, "PlayerPosition");
                    Call(chapter, "Move", Vector2.one, 1f);
                    Require(Vector3.Distance(Vector(chapter, "PlayerPosition"), previousPosition) < .001f, "Move is blocked during dialogue");
                    Record("Actual interaction: E at bag; automatic dialogue locks Move");
                    Shot("08-bag-dialogue"); Next(); break;
                case 8:
                    if (Int(chapter, "Part") != 1 || Text(chapter, "Phase") != "explore") return;
                    Require(!Bool(chapter, "ControlsLocked"), "Bag dialogue restores exploration controls");
                    previousPosition = Vector(chapter, "PlayerPosition");
                    Call(chapter, "Move", new Vector2(-1, 0), .2f);
                    Require(Vector3.Distance(Vector(chapter, "PlayerPosition"), previousPosition) > .01f, "Move works again after dialogue");
                    ((Transform)Get(chapter, "Player")).position = Vector(chapter, "ChairSpot");
                    Call(chapter, "Interact");
                    Require(Text(chapter, "Sequence") == "chair" && Bool(chapter, "ControlsLocked"), "Independent E at chair starts sit sequence");
                    Record("Dialogue restores movement; independent E at chair starts NPC approach");
                    Shot("09-chair-sequence"); Next(); break;
                case 9:
                    if (Text(chapter, "Phase") != "await-boot") return;
                    Require(Int(chapter, "Part") == 2 && !Bool(chapter, "ComputerOn") && !Bool(chapter, "ScreenLit"), "NPC prompt waits for a fresh E; computer stays off");
                    Record("NPC approach and prompt complete; computer still off awaiting independent E");
                    Shot("10-await-independent-e"); Next(); break;
                case 10:
                    if (SecondsInStage < 1.5) return;
                    Require(!Bool(chapter, "ComputerOn"), "Waiting does not automatically boot the computer");
                    Call(chapter, "Interact");
                    Require(Bool(chapter, "ComputerOn") && !Bool(chapter, "ScreenLit") && Text(chapter, "Sequence") == "boot", "Third independent E starts boot sequence before screen lights");
                    Record("Third independent E starts computer; screen awaits its scripted delay"); Next(); break;
                case 11:
                    if (!Bool(chapter, "ScreenLit")) return;
                    Require(Bool(chapter, "ControlsLocked"), "Boot and head-pat performance locks controls");
                    Record("Scripted screen lights; boot/head-pat performance is running");
                    Shot("11-computer-on"); Next(); break;
                case 12:
                    if (SecondsInStage < 6.7) return;
                    if (Page == "game") Shot("12-head-pat-performance");
                    Record("Unaccelerated boot/head-pat timeline advances"); Next(); break;
                case 13:
                    if (Page != "memory-map") return;
                    if (Time.realtimeSinceStartup - runStart < 30) return;
                    Require(Int(app, "Completed") == 1, "Chapter completion persists first chapter");
                    Require(PlayerPrefs.GetInt(ProgressKey, 0) == 1, "Default PlayerPrefs key contains first chapter completion");
                    Require(Time.realtimeSinceStartup - runStart >= 30, "Timeline contains at least 30 real seconds at timeScale 1");
                    Record("Chapter sequence returns to map with persisted completion");
                    Shot("13-memory-map-completed"); Next(); break;
                case 14:
                    Press("node-02");
                    Require(Int(app, "Selected") == 1 && !GetButton("chapter-launch").IsInteractable(), "Chapter02 unlocks but is marked in production and cannot launch");
                    for (int i = 3; i <= 8; i++) Require(!GetButton("node-" + i.ToString("00")).IsInteractable(), "Later chapters remain locked in order");
                    Record("UGUI node-02 is unlocked after Chapter01 and selects its in-production detail");
                    Shot("14-chapter02-in-production"); Next(); break;
                case 15:
                    evidence.status = status = "awaiting-restart";
                    Record("First pass succeeded; stop and restart Play mode or Editor, then VerifyRestartAndRestore");
                    active = false;
                    Application.logMessageReceived -= OnLog;
                    Debug.Log("MAKUBEX_VERIFICATION_STAGE1_OK: awaiting restart; " + StepsPath);
                    break;
            }
        }

        static void TickRestart()
        {
            switch (stage)
            {
                case 0:
                    Require(Int(app, "Completed") == 1, "A recreated App reads Chapter01 completion from default PlayerPrefs");
                    Require(PlayerPrefs.GetInt(ProgressKey, 0) == 1, "Persisted disk progress survives restart");
                    if (Page == "cover") Press("cover-start");
                    Record("Restarted App reads the saved first-chapter completion"); Next(); break;
                case 1:
                    if (Page != "memory-map") return;
                    if (substage == 0)
                    {
                        Record("Restart resource load created map; wait two actual frames before its pointer event");
                        substage = 1;
                        WaitForRenderedFrames();
                        return;
                    }
                    Press("node-02");
                    Record("Restarted map retains Chapter01 completion and Chapter02 unlock");
                    Shot("15-restart-progress-retained"); Next(); break;
                case 2:
                    if (substage == 0)
                    {
                        Call(app, "HandleEscape");
                        Require(Bool(app, "IsPaused"), "Esc opens reset settings");
                        Record("Reset settings opened; wait two actual frames before its pointer event");
                        substage = 1;
                        WaitForRenderedFrames();
                        return;
                    }
                    if (substage == 1)
                    {
                        Press("settings-reset");
                        Require(GameObject.Find("Confirmation") != null && Int(app, "Completed") == 1, "Clear progress requires confirmation");
                        Record("Clear confirmation created; wait two actual frames before its confirm pointer event");
                        substage = 2;
                        WaitForRenderedFrames();
                        return;
                    }
                    Press("dialog-confirm");
                    Require(Int(app, "Completed") == 0 && !GetButton("node-02").IsInteractable(), "Confirmed clear resets progress and chapter02 lock");
                    Record("Actual settings reset and confirmation clear progress and re-lock chapter02");
                    Shot("16-clear-progress-confirmed"); Next(); break;
                case 3:
                    RestoreOriginalProgress();
                    evidence.status = status = "passed";
                    evidence.finishedUtc = DateTime.UtcNow.ToString("o");
                    Record("Verification passed; original user progress restored");
                    active = false;
                    Application.logMessageReceived -= OnLog;
                    Debug.Log("MAKUBEX_VERIFICATION_OK: real UI and chapter flow passed; original progress restored; " + StepsPath);
                    break;
            }
        }

        public static void RestoreOriginalProgress()
        {
            if (!File.Exists(BackupPath)) return;
            PrefBackup backup = JsonUtility.FromJson<PrefBackup>(File.ReadAllText(BackupPath));
            if (backup == null || backup.restored) return;
            if (backup.existed) PlayerPrefs.SetInt(ProgressKey, backup.value);
            else PlayerPrefs.DeleteKey(ProgressKey);
            if (backup.sensitivityExisted) PlayerPrefs.SetInt("makubex.mouse.sensitivity", backup.sensitivityValue);
            else PlayerPrefs.DeleteKey("makubex.mouse.sensitivity");
            PlayerPrefs.Save();
            backup.restored = true;
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, true));
        }

        static string Page { get { return Text(app, "CurrentPage"); } }
        static double SecondsInStage { get { return EditorApplication.timeSinceStartup - entered; } }
        static void Next()
        {
            stage++;
            substage = 0;
            entered = EditorApplication.timeSinceStartup;
            WaitForRenderedFrames();
        }
        // EditorApplication.update may run repeatedly in one game frame. Real frameCount,
        // rather than editor ticks or wall time, ensures newly created Graphics receive depth.
        static void WaitForRenderedFrames() { nextActionFrame = Time.frameCount + 2; }
        static void Require(bool passed, string message) { if (!passed) throw new InvalidOperationException(message); }

        static void Press(string name)
        {
            Button button = GetButton(name);
            Require(button != null && button.IsActive() && button.IsInteractable(), "UGUI Button is interactable: " + name);
            Require(EventSystem.current != null, "An EventSystem is active");
            Canvas.ForceUpdateCanvases();
            VerificationCaptureBridge.RefreshOffscreenLayout();
            Canvas.ForceUpdateCanvases();
            RectTransform rect = button.GetComponent<RectTransform>();
            Require(rect != null, "UGUI Button has a RectTransform: " + name);
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "GraphicRaycaster hits the Button centre: " + name + " at " + position);
            Transform firstHit = hits[0].gameObject.transform;
            Require(firstHit == button.transform || firstHit.IsChildOf(button.transform),
                "Button centre is unobstructed: " + name + "; first hit is " + firstHit.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Record("GraphicRaycaster confirmed unobstructed centre and dispatched pointerClick: " + name + " at " + position);
        }
        static Button GetButton(string name)
        {
            GameObject target = GameObject.Find(name);
            Require(target != null, "UGUI target exists: " + name);
            Button button = target.GetComponent<Button>();
            Require(button != null, "UGUI Button component exists: " + name);
            return button;
        }

        static UnityEngine.Object Find(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return UnityEngine.Object.FindAnyObjectByType(type);
            }
            return null;
        }
        static object Get(UnityEngine.Object target, string name)
        {
            Require(target != null, "Runtime object exists for " + name);
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Require(property != null, "Runtime property exists: " + name);
            return property.GetValue(target, null);
        }
        static void Call(UnityEngine.Object target, string name, params object[] args)
        {
            Require(target != null, "Runtime object exists for " + name);
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
            Require(method != null, "Runtime method exists: " + name);
            method.Invoke(target, args);
        }
        static int Int(UnityEngine.Object target, string name) { return Convert.ToInt32(Get(target, name)); }
        static bool Bool(UnityEngine.Object target, string name) { return Convert.ToBoolean(Get(target, name)); }
        static string Text(UnityEngine.Object target, string name) { return Convert.ToString(Get(target, name)); }
        static Vector3 Vector(UnityEngine.Object target, string name) { return (Vector3)Get(target, name); }

        static void Shot(string name)
        {
            captureBusy = true;
            string path = Path.Combine(Root, name + ".png");
            capture.Capture(path, written => { captureBusy = false; Record(Application.isBatchMode ? "Captured batch offscreen Camera.Render with actual runtime canvases at 1600x900" : "Captured rendered Game View", written); });
        }
        static void Record(string action, string screenshot = null)
        {
            if (evidence == null) return;
            var step = new Step { action = action, utc = DateTime.UtcNow.ToString("o"), frame = Time.frameCount,
                realtime = Time.realtimeSinceStartup, timeScale = Time.timeScale, screenshot = screenshot, page = app != null ? Page : "edit-mode" };
            if (app != null) { step.completed = Int(app, "Completed"); step.paused = Bool(app, "IsPaused"); }
            if (chapter != null)
            {
                step.chapterPhase = Text(chapter, "Phase"); step.sequence = Text(chapter, "Sequence"); step.part = Int(chapter, "Part");
                step.controlsLocked = Bool(chapter, "ControlsLocked"); step.computerOn = Bool(chapter, "ComputerOn");
                step.screenLit = Bool(chapter, "ScreenLit"); step.playerPosition = Vector(chapter, "PlayerPosition");
            }
            evidence.steps.Add(step);
            File.WriteAllText(StepsPath, JsonUtility.ToJson(evidence, true));
        }
        static void LoadEvidence()
        {
            if (!File.Exists(StepsPath)) throw new InvalidOperationException("No prepared verification report exists.");
            evidence = JsonUtility.FromJson<Evidence>(File.ReadAllText(StepsPath));
        }
        static void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Exception || kind == LogType.Error || kind == LogType.Assert)
                Fail("Unity " + kind + ": " + message + "\n" + stack);
        }
        static void Fail(string reason)
        {
            if (!active) return;
            active = false;
            captureBusy = false;
            Application.logMessageReceived -= OnLog;
            status = "failed";
            if (evidence != null)
            {
                evidence.status = status; evidence.error = reason; evidence.finishedUtc = DateTime.UtcNow.ToString("o");
                Record("FAILED: " + reason);
            }
            RestoreOriginalProgress();
            Debug.LogError("MAKUBEX_VERIFICATION_FAILED: " + reason);
        }
    }
}
#endif
