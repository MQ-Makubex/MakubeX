using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MakubeX.Chapter01Integration
{
    // Added to the runtime instance by the Editor verifier; absent from the saved test scene.
    public sealed class BridgeWalkProbe : MonoBehaviour
    {
        [Serializable] public sealed class Sample
        {
            public int frame, leg;
            public Vector3 position;
            public bool grounded;
            public int collisionFlags;
        }
        [Serializable] public sealed class Result
        {
            public string status = "running", error, unityVersion, graphicsDevice;
            public string method = "Play Mode, real frame deltaTime, shared controller Step + CharacterController.Move; no teleport on traversal";
            public bool enteredHut, returnedToBridge, railBlocked;
            public int movementFrames, groundedFrames, respawns, wallContacts, completedWaypoints;
            public float minimumFootY = 1000, maximumFootY = -1000, realSeconds;
            public List<Sample> trajectory = new List<Sample>();
            public List<string> screenshots = new List<string>();
            public List<string> runtimeErrors = new List<string>();
        }
        public Chapter01WalkController player;
        public Vector3[] route;
        public string output;
        public bool Complete { get; private set; }
        public bool Passed => result.status == "passed";
        Result result = new Result();
        int leg;
        float started;
        Exception failure;

        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            result.unityVersion = Application.unityVersion;
            result.graphicsDevice = SystemInfo.graphicsDeviceName;
            player.verificationMode = true;
            Application.logMessageReceived += OnLog;
            started = Time.realtimeSinceStartup;
            IEnumerator work = Traverse();
            while (true)
            {
                bool more = false;
                try { more = work.MoveNext(); }
                catch (Exception e) { failure = e; }
                if (failure != null || !more) break;
                yield return work.Current;
            }
            Application.logMessageReceived -= OnLog;
            result.respawns = player.Respawns;
            result.realSeconds = Time.realtimeSinceStartup - started;
            result.status = failure == null && result.runtimeErrors.Count == 0 ? "passed" : "failed";
            result.error = failure?.ToString();
            File.WriteAllText(Path.Combine(output, "playmode_walk.json"), JsonUtility.ToJson(result, true));
            Complete = true;
            Debug.Log("CHAPTER01_WALK_" + result.status.ToUpperInvariant() + " " + result.error);
        }

        IEnumerator Traverse()
        {
            for (int i = 0; i < 45; i++) { Move(Vector2.zero); yield return null; }
            Require(player.Body.isGrounded, "Spawn did not settle on the bridge");
            Capture("01-bridge-spawn");
            for (int i = 1; i < route.Length; i++)
            {
                leg = i;
                float distanceStart = FlatDistance(route[i]);
                float segmentStart = Time.realtimeSinceStartup;
                while (FlatDistance(route[i]) > .10f)
                {
                    Require(Time.realtimeSinceStartup-segmentStart < distanceStart/player.walkSpeed*2+10, "Movement stalled on outbound waypoint " + i);
                    player.Face(route[i] - player.transform.position);
                    Move(Vector2.up);
                    yield return null;
                }
                result.completedWaypoints++;
                if (i == 2) Capture("02-platform-approach");
            }
            result.enteredHut = FlatDistance(route[route.Length-1]) <= .11f && player.transform.position.y >= .14f;
            Require(result.enteredHut, "Hut floor was not reached");
            Capture("03-inside-hut");
            for (int i = route.Length - 2; i >= 0; i--)
            {
                leg++;
                float distanceStart = FlatDistance(route[i]);
                float segmentStart = Time.realtimeSinceStartup;
                while (FlatDistance(route[i]) > .10f)
                {
                    Require(Time.realtimeSinceStartup-segmentStart < distanceStart/player.walkSpeed*2+10, "Movement stalled on return waypoint " + i);
                    player.Face(route[i] - player.transform.position);
                    Move(Vector2.up);
                    yield return null;
                }
                result.completedWaypoints++;
            }
            result.returnedToBridge = FlatDistance(route[0]) <= .11f;
            Require(result.returnedToBridge, "Return to bridge failed");
            Capture("04-returned-to-bridge");
            // Walk perpendicular to bridge centreline until the real rail mesh blocks the capsule.
            player.Face(route[1] - route[0]);
            Vector3 before = player.transform.position;
            float duration = 0;
            bool sideHit = false;
            while (duration < 1.5f)
            {
                Move(Vector2.right);
                sideHit |= (player.LastFlags & CollisionFlags.Sides) != 0;
                duration += Mathf.Min(Time.deltaTime, .05f);
                yield return null;
            }
            float displacement = Vector3.Distance(before, player.transform.position);
            result.railBlocked = sideHit && displacement < 1.5f && player.Body.isGrounded;
            Require(result.railBlocked, "Bridge rail failed: displacement=" + displacement + " sideHit=" + sideHit);
            Capture("05-rail-collision");
            Require(player.Respawns == 0, "Traversal used respawn");
            Require(result.minimumFootY > -.1f, "Player fell below the walk surface");
        }

        float FlatDistance(Vector3 target) { Vector3 d = target-player.transform.position; d.y = 0; return d.magnitude; }
        void Move(Vector2 input)
        {
            player.Step(input, false, Time.deltaTime);
            result.movementFrames++;
            if (player.Body.isGrounded) result.groundedFrames++;
            if ((player.LastFlags & CollisionFlags.Sides) != 0) result.wallContacts++;
            result.minimumFootY = Mathf.Min(result.minimumFootY, player.transform.position.y);
            result.maximumFootY = Mathf.Max(result.maximumFootY, player.transform.position.y);
            if (result.movementFrames % 5 == 0) result.trajectory.Add(new Sample { frame=Time.frameCount, leg=leg, position=player.transform.position, grounded=player.Body.isGrounded, collisionFlags=(int)player.LastFlags });
            Require(player.transform.position.y > -.5f, "Player fell off walkable geometry");
            Require(Time.realtimeSinceStartup-started < 180f, "Runtime traversal exceeded 180 seconds");
        }
        void Capture(string name)
        {
            string path = Path.Combine(output, name + ".png");
            var camera = player.view;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var rt = new RenderTexture(1200, 840, 24);
            var image = new Texture2D(1200, 840, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0,0,1200,840),0,0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG()); result.screenshots.Add(path);
            }
            finally { camera.targetTexture=oldTarget; RenderTexture.active=oldActive; rt.Release(); Destroy(rt); Destroy(image); }
        }
        void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        void OnLog(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) result.runtimeErrors.Add(message+"\n"+stack);
        }
    }
}
