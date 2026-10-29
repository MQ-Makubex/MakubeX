using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MakubeX
{
    /// <summary>
    /// Chapter 01 prototype. Dialogue follows the approved v1.5 script; timing, room positions
    /// and the three independent E interactions follow Chapter01/app.mjs. Models are placeholders.
    /// </summary>
    public sealed class Chapter01Controller : MonoBehaviour
    {
        public bool actorLibraryOnly;
        public string Phase { get { return phase; } }
        public string state { get { return phase; } }
        public string Sequence { get { return sequence; } }
        public int Part { get { return part; } }
        public bool ComputerOn { get { return computerOn; } }
        public bool ScreenLit { get { return computerLit; } }
        public bool ControlsLocked { get { return phase != "explore" || Paused; } }
        public string CurrentSpeaker { get { return subtitlePanel != null && subtitlePanel.activeSelf ? speakerText.text : ""; } }
        public string CurrentLine { get { return subtitlePanel != null && subtitlePanel.activeSelf ? lineText.text : ""; } }
        public Vector3 BagSpot { get { return new Vector3(2.62f, 0, -.58f); } }
        public Vector3 ChairSpot { get { return new Vector3(-2.05f, 0, -1.55f); } }
        public Vector3 PlayerPosition { get { return player.root.position; } }
        public Transform Player { get { return player.root; } }

        private bool Paused { get { return MakubeXApp.Instance != null && MakubeXApp.Instance.IsPaused; } }
        private string phase = "explore", sequence;
        private int part, shown = -1;
        private bool computerOn, computerLit, seated, dragging;
        private float sceneTime, elapsed, yaw = .22f, sitYaw, lastMouseX;
        private Vector3 sitStart;
        private readonly Vector3 elderStart = new Vector3(-4, 0, -1.94f);
        private readonly Vector3 elderByChair = new Vector3(-2.98f, 0, -1.55f);
        private Camera sceneCamera;
        private Character player, elder;
        private Transform bag;
        private Material floor, wall, trim, wood, darkWood, brass, cloth, teal, paper, skin, silver, hair, elderCloth, screen;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<Transform> markers = new List<Transform>();
        private Transform bagMarker, chairMarker, pcMarker;
        private Font font;
        private Canvas hud;
        private GameObject objectivePanel, subtitlePanel, interactPanel, topBar, bottomBar, endingPanel;
        private Text objectiveText, progressText, speakerText, lineText, interactText;
        private Image progressFill;

        public sealed class Character
        {
            public Transform root, head;
            public readonly List<Transform> arms = new List<Transform>();
            public readonly List<GameObject> standing = new List<GameObject>();
            public readonly List<GameObject> sitting = new List<GameObject>();
        }

        private struct Line
        {
            public float at;
            public string speaker, text;
            public Line(float at, string speaker, string text) { this.at = at; this.speaker = speaker; this.text = text; }
        }
        private readonly Line[] bagLines = {
            new Line(0, "马克贝斯", "爷爷……这是我的名字吗？"),
            new Line(2.5f, "源水", "不知道。发现你的时候，它就在你身边。"),
            new Line(5.2f, "源水", "过来，坐这儿。")
        };
        private readonly Line[] chairLines = { new Line(3.3f, "源水", "来，打开它。") };
        private readonly Line[] bootLines = {
            new Line(1.6f, "马克贝斯", "亮了……"),
            new Line(2.6f, "马克贝斯", "它还能做什么？"),
            new Line(4.7f, "源水", "可以做很多。"),
            new Line(5.7f, "源水", "我们慢慢学。"),
            new Line(8.4f, "马克贝斯 · 回忆", "我不知道自己从哪里来。但那一天，我有了家。")
        };

        private void Awake()
        {
            if(actorLibraryOnly)return;
            font = MakubeXApp.UiFont;
            if (font == null) font = Resources.Load<Font>("Fonts/Chinese");
#if UNITY_WEBGL && !UNITY_EDITOR
            if (font == null) throw new System.InvalidOperationException("WebGL 缺少 Resources/Fonts/Chinese 中文字体资源。");
#else
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Arial Unicode MS", "Heiti SC", "PingFang SC", "Arial" }, 24);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#endif
            BuildRoom();
            BuildHud();
            RefreshObjective();
        }

        private void Update()
        {
            if(actorLibraryOnly)return;
            if (Paused) { dragging = false; return; }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            elapsed += dt;
            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (phase == "explore")
            {
                Vector2 motion = Vector2.zero;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) motion.x--;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) motion.x++;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) motion.y--;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) motion.y++;
                Move(motion, dt);
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) { dragging = true; lastMouseX = Input.mousePosition.x; }
                if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) dragging = false;
                if (dragging)
                {
                    float sensitivity = MakubeXApp.Instance != null ? MakubeXApp.Instance.MouseSensitivity : 1;
                    yaw = Mathf.Clamp(yaw + (Input.mousePosition.x - lastMouseX) * .0045f * sensitivity, -.9f, 1);
                    lastMouseX = Input.mousePosition.x;
                }
                bool atBag = part == 0;
                float distance = Vector3.Distance(player.root.position, atBag ? BagSpot : ChairSpot);
                interactPanel.SetActive(distance < (atBag ? 1.66f : 1.35f));
                interactText.text = atBag ? "[ E ]  查看「間久部」旧袋子" : "[ E ]  坐到椅子上";
                CameraTo(new Vector3(player.root.position.x + Mathf.Sin(yaw) * 2.2f, 2.9f,
                    player.root.position.z + Mathf.Cos(yaw) * 4.4f),
                    new Vector3(player.root.position.x, 1.05f, player.root.position.z - 1), dt * 3.8f);
                elder.root.localEulerAngles = new Vector3(0, (-.48f + Mathf.Sin(elapsed * .8f) * .025f) * Mathf.Rad2Deg, 0);
            }
            else if (phase == "scene") TickSequence(dt);
            else if (phase == "await-boot")
            {
                interactPanel.SetActive(true);
                interactText.text = "[ E ]  打开电脑";
                CameraTo(new Vector3(.25f, 2.3f, 1.55f), new Vector3(-2.45f, 1.4f, -2.35f), dt * 2.6f);
            }
            else if (sceneCamera != null) sceneCamera.transform.LookAt(player.root.position + Vector3.up * 1.1f);

            SetMarkerHeight(bagMarker, 1.7f);
            SetMarkerHeight(chairMarker, 1.65f);
            SetMarkerHeight(pcMarker, 1.9f);
            foreach (Transform marker in markers)
                if (marker != null) marker.rotation = sceneCamera.transform.rotation;
        }

        /// <summary>The live controls and test runner share these same state and distance checks.</summary>
        public void Interact()
        {
            if (Paused) return;
            if (phase == "await-boot" && part == 2 && !computerOn &&
                Vector3.Distance(player.root.position, ChairSpot) < 1.35f)
            {
                computerOn = true;
                StartSequence("boot");
                return;
            }
            if (phase != "explore") return;
            if (part == 0 && Vector3.Distance(player.root.position, BagSpot) < 1.66f) StartSequence("bag");
            else if (part == 1 && Vector3.Distance(player.root.position, ChairSpot) < 1.35f) StartSequence("chair");
        }

        public void Move(Vector2 motion, float dt)
        {
            if (ControlsLocked || dt <= 0) return;
            if (motion.sqrMagnitude == 0) { player.root.position = new Vector3(player.root.position.x, 0, player.root.position.z); return; }
            motion.Normalize();
            float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 2.9f : 2.15f;
            float x = Mathf.Clamp(player.root.position.x + motion.x * speed * dt, -4.9f, 4.9f);
            float z = Mathf.Clamp(player.root.position.z + motion.y * speed * dt, -4.2f, 4.25f);
            // Approved web prototype's desk collision envelope.
            if (!(x > -4.75f && x < -.35f && z < -2.55f && z > -4.1f))
                player.root.position = new Vector3(x, Mathf.Sin(elapsed * 13) * .027f, z);
            player.root.rotation = Quaternion.Euler(0, Mathf.Atan2(motion.x, motion.y) * Mathf.Rad2Deg, 0);
        }

        private void StartSequence(string kind)
        {
            phase = "scene"; sequence = kind; sceneTime = 0; shown = -1; dragging = false;
            interactPanel.SetActive(false);
            subtitlePanel.SetActive(false);
            topBar.SetActive(true); bottomBar.SetActive(true);
            objectivePanel.GetComponent<CanvasGroup>().alpha = .12f;
            if (kind == "bag") { player.root.rotation = Quaternion.Euler(0, -.3f * Mathf.Rad2Deg, 0); bagMarker.gameObject.SetActive(false); }
            if (kind == "chair") { chairMarker.gameObject.SetActive(false); sitStart = player.root.position; sitYaw = player.root.eulerAngles.y; }
            if (kind == "boot") pcMarker.gameObject.SetActive(false);
        }

        private void TickSequence(float dt)
        {
            sceneTime += dt;
            Line[] lines = sequence == "bag" ? bagLines : sequence == "chair" ? chairLines : bootLines;
            for (int i = lines.Length - 1; i >= 0; --i)
            {
                if (sceneTime < lines[i].at) continue;
                if (shown != i) { shown = i; speakerText.text = lines[i].speaker; lineText.text = lines[i].text; subtitlePanel.SetActive(true); }
                break;
            }
            if (sequence == "bag")
            {
                CameraTo(new Vector3(3.75f, 2.3f, 2.6f), new Vector3(.7f, 1.05f, -1.15f), dt * 2.2f);
                bag.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Mathf.Min(sceneTime, 6) * 2) * .022f * Mathf.Rad2Deg);
                bag.localPosition = new Vector3(2.62f, .5f + Mathf.Min(sceneTime / 3, 1) * .2f, -.58f);
                if (sceneTime > 7.6f) EndSequence();
            }
            else if (sequence == "chair")
            {
                CameraTo(new Vector3(.75f, 2.2f, 1.95f), new Vector3(-2.5f, 1.25f, -2), dt * 2.2f);
                if (sceneTime < .95f)
                {
                    float a = Smooth(sceneTime, 0, .95f);
                    player.root.position = Vector3.Lerp(sitStart, ChairSpot, a);
                    player.root.rotation = Quaternion.Euler(0, Mathf.LerpAngle(sitYaw, 180, a), 0);
                }
                else if (!seated)
                {
                    player.root.position = ChairSpot; player.root.rotation = Quaternion.Euler(0, 180, 0);
                    foreach (GameObject item in player.standing) item.SetActive(false);
                    foreach (GameObject item in player.sitting) item.SetActive(true);
                    seated = true;
                }
                float walk = Smooth(sceneTime, 1.05f, 3.25f);
                elder.root.position = Vector3.Lerp(elderStart, elderByChair, walk);
                elder.root.rotation = Quaternion.Euler(0, Mathf.Lerp(-.48f, .4f, walk) * Mathf.Rad2Deg, 0);
                elder.arms[1].localRotation = Quaternion.Euler(walk > .94f ? -.5f * Mathf.Rad2Deg : 0, 0,
                    (walk > .94f ? -.62f : Mathf.Sin(sceneTime * 6) * .12f) * Mathf.Rad2Deg);
                elder.head.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(sceneTime * 1.3f) * .035f * Mathf.Rad2Deg);
                if (sceneTime > 5.4f) EndSequence();
            }
            else if (sequence == "boot")
            {
                CameraTo(new Vector3(.25f, 2.45f, 1.45f), new Vector3(-2.47f, 1.43f, -2.3f), dt * 2.2f);
                // Never illuminate before the separate third E interaction.
                if (sceneTime > .75f && !computerLit) PowerOn();
                float reach = sceneTime > 1.5f ? -.45f : Mathf.Min(sceneTime / .8f, 1) * -.75f;
                player.arms[1].localRotation = Quaternion.Euler(reach * Mathf.Rad2Deg, 0, .12f * Mathf.Rad2Deg);
                float pat = Smooth(sceneTime, 6.5f, 7.5f) * (1 - Smooth(sceneTime, 8.4f, 9.6f));
                Transform hand = elder.arms[1];
                hand.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-.62f, -1.45f, pat) * Mathf.Rad2Deg);
                // Offset the temporary cylinder hand so it visibly meets the child's crown.
                hand.localPosition = new Vector3(Mathf.Lerp(.29f, .6f, pat), Mathf.Lerp(.96f, 1.46f, pat), .22f * pat);
                Vector3 scale = hand.localScale; scale.y = .26f * (1 + pat * .25f); hand.localScale = scale;
                float lookUp = Smooth(sceneTime, 9.6f, 10.4f);
                player.head.localRotation = Quaternion.Euler(-18 * lookUp, 28 * lookUp, -.06f * pat * Mathf.Rad2Deg);
                if (sceneTime > 11.3f) EndSequence();
            }
        }

        private void EndSequence()
        {
            string finished = sequence; sequence = null;
            topBar.SetActive(false); bottomBar.SetActive(false); subtitlePanel.SetActive(false);
            objectivePanel.GetComponent<CanvasGroup>().alpha = 1;
            if (finished == "bag") { phase = "explore"; part = 1; chairMarker.gameObject.SetActive(true); }
            else if (finished == "chair") { phase = "await-boot"; part = 2; pcMarker.gameObject.SetActive(true); }
            else
            {
                phase = "done"; part = 3; objectivePanel.SetActive(false); interactPanel.SetActive(false); endingPanel.SetActive(true);
                if (MakubeXApp.Instance != null) MakubeXApp.Instance.CompleteChapter(1);
            }
            RefreshObjective();
        }

        private void RefreshObjective()
        {
            objectiveText.text = part == 0 ? "找到那个写着「間久部」的旧袋子" : part == 1 ? "走到椅子旁，按 E 坐下" : "爷爷说：来，打开它。按 E 开机";
            progressText.text = (Mathf.Min(part + 1, 3)).ToString("00") + " / 03";
            progressFill.rectTransform.anchorMax = new Vector2(part / 3f, 1);
        }

        private void PowerOn()
        {
            computerLit = true;
            screen.color = Hex(0x9dcfc0);
            screen.EnableKeyword("_EMISSION");
            screen.SetColor("_EmissionColor", Hex(0x5baaac) * 1.35f);
        }

        private float Smooth(float t, float lo, float hi) { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lo, hi, t)); }
        private void CameraTo(Vector3 desired, Vector3 focus, float blend)
        {
            sceneCamera.transform.position = Vector3.Lerp(sceneCamera.transform.position, desired, Mathf.Clamp01(blend));
            sceneCamera.transform.LookAt(focus);
        }
        private void SetMarkerHeight(Transform marker, float height)
        {
            Vector3 p = marker.position; p.y = height + Mathf.Sin(elapsed * 2) * .06f; marker.position = p;
        }

        private static Color Hex(uint value)
        {
            return new Color(((value >> 16) & 255) / 255f, ((value >> 8) & 255) / 255f, (value & 255) / 255f, 1);
        }

        private Material Mat(uint color, float metallic = 0, uint emission = 0, float emissionIntensity = 1)
        {
            // Serialized templates retain the Standard shader and emission variant in Web builds.
            Material template = Resources.Load<Material>(emission != 0 ? "Materials/StageEmissive" : "Materials/StageLit");
            Material m = template ? new Material(template) : new Material(Shader.Find("Standard"));
            m.color = Hex(color); m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", .18f);
            if (emission != 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Hex(emission) * emissionIntensity); }
            materials.Add(m); return m;
        }

        private Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 size, Material material, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider hit = go.GetComponent<Collider>(); if (hit != null) Destroy(hit);
            return go.transform;
        }
        private Transform Box(Transform parent, float w, float h, float d, Material m, float x, float y, float z, string name = "Room prop")
        { return Primitive(PrimitiveType.Cube, name, parent, new Vector3(w, h, d), m, new Vector3(x, y, z)); }
        private Transform Sphere(Transform parent, float radius, Material m, float x, float y, float z, string name = "Placeholder sphere")
        { return Primitive(PrimitiveType.Sphere, name, parent, Vector3.one * (radius * 2), m, new Vector3(x, y, z)); }
        private Transform Cylinder(Transform parent, float top, float bottom, float height, Material m, float x, float y, float z)
        { return Primitive(PrimitiveType.Cylinder, "Placeholder cylinder", parent, new Vector3(top + bottom, height * .5f, top + bottom), m, new Vector3(x, y, z)); }

        private void BuildRoom()
        {
            floor = Mat(0x4b5660); wall = Mat(0x303c47); trim = Mat(0x192733, .24f);
            wood = Mat(0x856649); darkWood = Mat(0x4e443c); brass = Mat(0xb89463, .55f);
            cloth = Mat(0x294d65); teal = Mat(0x85c6c3, 0, 0x122522); paper = Mat(0xc3aa7d);
            skin = Mat(0xdfb99f); silver = Mat(0xd3dce0); hair = Mat(0x3a4a52); elderCloth = Mat(0x91795f);
            screen = Mat(0x172431, 0, 0x0b1824, .3f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex(0xa7d8eb) * .7f;
            RenderSettings.ambientEquatorColor = Hex(0x72767b) * .65f;
            RenderSettings.ambientGroundColor = Hex(0x4f3e32) * .7f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Hex(0x111a27); RenderSettings.fogDensity = .015f;

            GameObject cameraObject = new GameObject("Chapter 01 camera"); cameraObject.transform.SetParent(transform, false);
            sceneCamera = cameraObject.AddComponent<Camera>(); sceneCamera.tag = "MainCamera";
            sceneCamera.fieldOfView = 53; sceneCamera.nearClipPlane = .1f; sceneCamera.farClipPlane = 100;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor; sceneCamera.backgroundColor = Hex(0x0d1825);
            sceneCamera.transform.position = new Vector3(3.2f, 3.4f, 7.8f); sceneCamera.transform.LookAt(new Vector3(0, 1, 0));
            cameraObject.AddComponent<AudioListener>();
            GameObject sunObject = new GameObject("Warm window light"); sunObject.transform.SetParent(transform, false);
            Light sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.color = Hex(0xffd3a0); sun.intensity = 1.1f;
            sun.shadows = LightShadows.Hard; sun.shadowStrength = .55f; sunObject.transform.rotation = Quaternion.Euler(55, -28, 0);
            GameObject lampObject = new GameObject("Desk lamp"); lampObject.transform.SetParent(transform, false); lampObject.transform.localPosition = new Vector3(-3.7f, 2.4f, -2.8f);
            Light lamp = lampObject.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = Hex(0xffbf71); lamp.intensity = 2; lamp.range = 6; lamp.shadows = LightShadows.None;

            Box(transform, 11.2f, .22f, 10, floor, 0, -.14f, 0, "Floor");
            Box(transform, 11.2f, 4, .2f, wall, 0, 2, -4.95f, "Back wall");
            Box(transform, .2f, 4, 10, wall, -5.55f, 2, 0, "Left wall");
            Box(transform, .2f, 4, 10, wall, 5.55f, 2, 0, "Right wall");
            for (float x = -5.3f; x < 5.3f; x += 1.6f) Box(transform, .035f, .035f, 9.6f, trim, x, .01f, 0);
            for (int x = -5; x < 5; x += 2) Box(transform, .05f, 3.8f, .12f, trim, x, 2, -4.8f);
            Box(transform, 3, 1.8f, .03f, Mat(0x223f55, 0, 0x214458, .75f), 2.65f, 2.58f, -4.82f, "Window");
            for (int i = 0; i < 3; ++i) Box(transform, .05f, 1.9f, .07f, trim, 1.66f + i, 2.58f, -4.75f);
            Box(transform, 3.13f, .06f, .08f, brass, 2.65f, 3.48f, -4.71f);
            Box(transform, 3.13f, .07f, .08f, brass, 2.65f, 1.68f, -4.71f);
            Material rain = Mat(0x52788b, 0, 0x386376, .3f);
            for (int i = 0; i < 14; ++i)
            {
                Transform drop = Box(transform, .015f, .3f + .15f * (i % 4), .01f, rain, 1.3f + (i * 37 % 28) / 10f, 2 + (i * 17 % 12) / 10f, -4.72f);
                drop.localRotation = Quaternion.Euler(0, 0, .17f * Mathf.Rad2Deg);
            }
            Box(transform, 4.15f, .17f, 1.42f, wood, -2.55f, .95f, -3.23f, "Desk");
            foreach (float x in new[] { -4.35f, -.8f }) foreach (float z in new[] { -3.72f, -2.75f }) Box(transform, .12f, .94f, .12f, darkWood, x, .46f, z);
            Box(transform, 1.93f, 1.2f, .24f, trim, -2.4f, 1.69f, -3.23f, "Old computer");
            Box(transform, 1.68f, .98f, .017f, screen, -2.4f, 1.73f, -3.09f, "Monitor — remains off until E");
            Box(transform, .17f, .26f, .16f, brass, -2.4f, 1, -3.26f);
            Box(transform, .85f, .04f, .42f, trim, -2.4f, .98f, -2.58f);
            Box(transform, 1.05f, .04f, .33f, darkWood, -2.05f, 1.06f, -2.47f, "Keyboard");
            for (int i = 0; i < 9; ++i) for (int j = 0; j < 3; ++j) Box(transform, .07f, .009f, .07f, brass, -2.48f + i * .11f, 1.089f, -2.57f + j * .09f);
            Cylinder(transform, .035f, .035f, 1.2f, brass, -4.15f, 1.65f, -3.3f);
            Sphere(transform, .16f, Mat(0xf7d4a0, 0, 0xf7d4a0), -4.15f, 2.22f, -3.3f);
            Box(transform, 1.05f, .11f, .9f, darkWood, -2.05f, .46f, -1.6f, "Chair");
            Box(transform, 1.03f, .82f, .1f, wood, -2.05f, .9f, -1.13f);
            foreach (float x in new[] { -2.42f, -1.68f }) foreach (float z in new[] { -1.87f, -1.34f }) Box(transform, .1f, .48f, .1f, darkWood, x, .19f, z);
            Box(transform, 1.15f, 1.9f, .58f, darkWood, 4.66f, .98f, -2, "Shelf");
            foreach (float y in new[] { .6f, 1.15f, 1.7f }) Box(transform, 1.13f, .07f, .57f, brass, 4.66f, y, -2);
            Material[] books = { brass, paper, teal };
            for (int i = 0; i < 11; ++i) Box(transform, .15f, .18f + (i % 3) * .085f, .29f, books[i % 3], 4.26f + (i % 4) * .26f, .76f + (i / 4) * .55f, -1.96f);
            for (int i = 0; i < 9; ++i) Box(transform, .12f + (i % 3) * .08f, .07f, .1f, trim, -4.2f + (i % 4) * .23f, 1.06f, -2.8f + (i % 2) * .16f);
            Box(transform, 1.28f, .065f, .8f, darkWood, 2.52f, .41f, -.68f, "Bag table");
            bag = new GameObject("Old bag — 間久部").transform; bag.SetParent(transform, false); bag.localPosition = new Vector3(2.62f, .5f, -.58f);
            Box(bag, .96f, .67f, .22f, paper, 0, .15f, 0);
            Box(bag, .12f, .12f, .28f, brass, 0, .53f, 0);
            WorldLabel(bag, "間久部", new Vector3(0, .15f, .122f), new Vector2(.71f, .37f), false);
            player = MakePerson(.87f, false); player.root.localPosition = new Vector3(0, 0, 2.3f);
            elder = MakePerson(1.02f, true); elder.root.localPosition = elderStart; elder.root.localRotation = Quaternion.Euler(0, -.5f * Mathf.Rad2Deg, 0);
            Box(transform, 4.8f, .012f, 2.3f, Mat(0x604b4c), -.1f, .018f, 1.84f, "Carpet");
            for (int i = 0; i < 7; ++i) Box(transform, 4.6f, .008f, .012f, brass, -.1f, .031f, .81f + i * .34f);
            bagMarker = WorldLabel(transform, "旧袋子", new Vector3(2.62f, 1.7f, -.58f), new Vector2(1.15f, .43f), true);
            chairMarker = WorldLabel(transform, "坐下", new Vector3(-2.05f, 1.65f, -1.55f), new Vector2(1.15f, .43f), true);
            pcMarker = WorldLabel(transform, "按键开机", new Vector3(-2.05f, 1.9f, -2.48f), new Vector2(1.15f, .43f), true);
            chairMarker.gameObject.SetActive(false); pcMarker.gameObject.SetActive(false);
        }

        public Character CreateImportedSceneActor(bool isElder)
        {
            // Reuse the already approved phase-one placeholder rig; no duplicate room is built.
            if(!cloth){cloth=Mat(0x294d65);elderCloth=Mat(0x91795f);skin=Mat(0xdfb99f);silver=Mat(0xd3dce0);hair=Mat(0x3a4a52);darkWood=Mat(0x4e443c);trim=Mat(0x192733,.24f);}
            return MakePerson(isElder?1.02f:.87f,isElder);
        }
        private Character MakePerson(float scale, bool isElder)
        {
            Character c = new Character(); c.root = new GameObject(isElder ? "源水 — 临时占位角色" : "马克贝斯 — 临时占位角色").transform;
            c.root.SetParent(transform, false); c.root.localScale = Vector3.one * scale;
            Cylinder(c.root, .22f, .26f, .63f, isElder ? elderCloth : cloth, 0, .94f, 0);
            Cylinder(c.root, .09f, .09f, .15f, skin, 0, 1.3f, 0);
            c.head = new GameObject("Head and face pose pivot").transform;
            c.head.SetParent(c.root, false); c.head.localPosition = new Vector3(0, 1.55f, 0);
            Sphere(c.head, .31f, skin, 0, 0, 0, "Head");
            Transform headHair = Sphere(c.head, .327f, isElder ? silver : hair, 0, .12f, -.035f);
            headHair.localScale = Vector3.Scale(headHair.localScale, new Vector3(1.04f, .52f, .96f));
            if (isElder)
            {
                Transform beard = Sphere(c.head, .18f, silver, 0, -.19f, .28f);
                beard.localScale = Vector3.Scale(beard.localScale, new Vector3(.95f, .65f, .48f));
            }
            else
            {
                for (int i = 0; i < 5; ++i)
                {
                    Transform tuft = Cylinder(c.head, 0, .115f, .25f, hair, -.23f + i * .115f, .28f, .15f);
                    tuft.localRotation = Quaternion.Euler(0, 0, (i - 2) * .3f * Mathf.Rad2Deg);
                }
                Material glasses = Mat(0x2b303d, .65f);
                foreach (float x in new[] { -.145f, .145f })
                {
                    GameObject ring = new GameObject("Glasses rim"); ring.transform.SetParent(c.head, false); ring.transform.localPosition = new Vector3(x, 0, .279f);
                    LineRenderer line = ring.AddComponent<LineRenderer>(); line.sharedMaterial = glasses; line.useWorldSpace = false;
                    line.loop = true; line.widthMultiplier = .025f; line.positionCount = 20; line.numCornerVertices = 1;
                    for (int i = 0; i < 20; ++i) { float a = i * Mathf.PI * 2 / 20; line.SetPosition(i, new Vector3(Mathf.Cos(a) * .135f, Mathf.Sin(a) * .135f, 0)); }
                }
                Box(c.head, .12f, .025f, .018f, glasses, 0, 0, .31f);
            }
            foreach (float x in new[] { -.29f, .29f })
            {
                Transform arm = Cylinder(c.root, .075f, .07f, .52f, isElder ? elderCloth : cloth, x, .96f, 0);
                arm.localRotation = Quaternion.Euler(0, 0, (x < 0 ? -.12f : .12f) * Mathf.Rad2Deg); c.arms.Add(arm);
                c.standing.Add(Cylinder(c.root, .07f, .06f, .57f, isElder ? darkWood : trim, x * .56f, .4f, 0).gameObject);
            }
            c.standing.Add(Box(c.root, .16f, .1f, .32f, trim, -.15f, .065f, .1f).gameObject);
            c.standing.Add(Box(c.root, .16f, .1f, .32f, trim, .15f, .065f, .1f).gameObject);
            foreach (float x in new[] { -.15f, .15f })
            {
                c.sitting.Add(Box(c.root, .17f, .13f, .5f, trim, x, .52f, .22f).gameObject);
                c.sitting.Add(Box(c.root, .15f, .37f, .15f, trim, x, .28f, .46f).gameObject);
                c.sitting.Add(Box(c.root, .18f, .1f, .27f, trim, x, .08f, .55f).gameObject);
            }
            foreach (GameObject item in c.sitting) item.SetActive(false);
            return c;
        }

        private RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = anchorMin; r.anchorMax = anchorMax; r.offsetMin = offsetMin; r.offsetMax = offsetMax; return r;
        }
        private Image Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax, Color color)
        {
            RectTransform r = Rect(name, parent, min, max, insetMin, insetMax); Image image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private Text Label(string name, Transform parent, string content, int size, Color color, TextAnchor alignment, Vector2 min, Vector2 max, Vector2 insetMin, Vector2 insetMax)
        {
            RectTransform r = Rect(name, parent, min, max, insetMin, insetMax); Text text = r.gameObject.AddComponent<Text>();
            text.font = font; text.text = content; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false; return text;
        }
        private Transform WorldLabel(Transform parent, string content, Vector3 position, Vector2 size, bool billboard)
        {
            GameObject go = new GameObject(content, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            Canvas canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = sceneCamera;
            RectTransform r = go.GetComponent<RectTransform>(); r.sizeDelta = new Vector2(256, 96); r.localScale = new Vector3(size.x / 256, size.y / 96, .004f);
            if (!billboard) r.localRotation = Quaternion.Euler(0, 180, 0);
            Panel("Label background", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, billboard ? new Color(.075f, .12f, .18f, .9f) : Hex(0xbcaa84));
            Label("Label", r, content, 35, billboard ? Hex(0xf1e4d2) : Hex(0x282a2e), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
            if (billboard) markers.Add(r); return r;
        }

        private void BuildHud()
        {
            GameObject canvasObject = new GameObject("Chapter 01 HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); canvasObject.transform.SetParent(transform, false);
            hud = canvasObject.GetComponent<Canvas>(); hud.renderMode = RenderMode.ScreenSpaceOverlay; hud.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            Color gold = Hex(0xddb98a), ink = new Color(.035f, .06f, .095f, .86f), cream = Hex(0xf1e4d2);
            Label("Chapter", hud.transform, "第一章 · 名字", 35, cream, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -100), new Vector2(680, -44));
            Label("Prototype disclosure", hud.transform, "临时占位场景 · 人物 / 动画待正式制作", 23, gold, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, -143), new Vector2(800, -104));
            objectivePanel = Panel("Objective panel", hud.transform, Vector2.one, Vector2.one, new Vector2(-710, -188), new Vector2(-56, -44), ink).gameObject;
            objectivePanel.AddComponent<CanvasGroup>();
            objectiveText = Label("Objective", objectivePanel.transform, "", 27, cream, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(26, 47), new Vector2(-25, -18));
            progressText = Label("Progress", objectivePanel.transform, "", 20, gold, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(28, 18), new Vector2(-30, -94));
            Image track = Panel("Progress track", objectivePanel.transform, Vector2.zero, new Vector2(1, 0), new Vector2(126, 25), new Vector2(-28, 29), new Color(1, 1, 1, .15f));
            progressFill = Panel("Progress fill", track.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, gold);
            Label("Controls", hud.transform, "WASD / 方向键 移动     Shift 快走     拖动鼠标 视角     E 互动", 24, cream, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(1, 0), new Vector2(40, 24), new Vector2(-40, 66));
            interactPanel = Panel("Interaction", hud.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-350, 112), new Vector2(350, 180), ink).gameObject;
            interactText = Label("Interaction text", interactPanel.transform, "", 30, cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            interactPanel.SetActive(false);
            topBar = Panel("Cinema top", hud.transform, new Vector2(0, 1), Vector2.one, new Vector2(0, -58), Vector2.zero, Color.black).gameObject;
            bottomBar = Panel("Cinema bottom", hud.transform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 82), Color.black).gameObject;
            topBar.SetActive(false); bottomBar.SetActive(false);
            subtitlePanel = Panel("Dialogue", hud.transform, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(-690, 108), new Vector2(690, 282), ink).gameObject;
            speakerText = Label("Speaker", subtitlePanel.transform, "", 25, gold, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(35, 112), new Vector2(-35, -20));
            lineText = Label("Dialogue line", subtitlePanel.transform, "", 33, cream, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(35, 20), new Vector2(-35, -60));
            subtitlePanel.SetActive(false);
            endingPanel = Panel("Chapter complete", hud.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(.035f, .06f, .09f, .91f)).gameObject;
            Label("Complete title", endingPanel.transform, "第一章 · 名字\n记忆已点亮", 50, cream, TextAnchor.MiddleCenter, new Vector2(.1f, .45f), new Vector2(.9f, .65f), Vector2.zero, Vector2.zero);
            Label("Return hint", endingPanel.transform, "正在返回记忆轨迹……", 28, gold, TextAnchor.MiddleCenter, new Vector2(.1f, .3f), new Vector2(.9f, .44f), Vector2.zero, Vector2.zero);
            endingPanel.SetActive(false);
        }

        private void OnApplicationFocus(bool hasFocus) { if (!hasFocus) dragging = false; }
        private void OnDestroy()
        {
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
