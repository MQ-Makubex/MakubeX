#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MakubeX.Verification
{
    /// <summary>Editor verification only; omitted entirely from player builds.</summary>
    public sealed class VerificationCaptureBridge : MonoBehaviour
    {
        public void Capture(string path, Action<string> complete)
        {
            if (Application.isBatchMode)
            {
                CaptureOffscreen(path);
                complete(path);
                return;
            }
            StartCoroutine(CaptureFrame(path, complete));
        }

        struct CanvasState
        {
            public Canvas canvas;
            public RenderMode mode;
            public Camera camera;
            public float distance, scale, referencePixels;
        }

        // In batch mode no Game View draw assigns absoluteDepth to newly created Graphics.
        // Draw the actual canvases once before a real GraphicRaycaster hit test.
        public static void RefreshOffscreenLayout()
        {
            if (Application.isBatchMode) CaptureOffscreen(null);
        }

        /// <summary>
        /// Batch mode does not dispatch WaitForEndOfFrame. Render the current game camera at
        /// 1600x900, temporarily directing the real active overlay canvases to that camera.
        /// Every camera and canvas property is restored before the next simulation frame.
        /// </summary>
        static void CaptureOffscreen(string path)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
            {
                camera = null;
                foreach (Camera candidate in Camera.allCameras)
                    if (candidate.enabled && candidate.gameObject.activeInHierarchy && candidate.cameraType == CameraType.Game &&
                        (camera == null || candidate.depth > camera.depth)) camera = candidate;
            }
            if (camera == null) throw new InvalidOperationException("Batch offscreen capture requires a live game camera.");
            var canvasStates = new List<CanvasState>();
            RenderTexture originalTarget = camera.targetTexture;
            RenderTexture originalActive = RenderTexture.active;
            Rect originalRect = camera.rect;
            float originalAspect = camera.aspect;
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf);
            Texture2D frame = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = 1600f / 900;
                foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>())
                {
                    if (!canvas.enabled || !canvas.gameObject.activeInHierarchy || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvasStates.Add(new CanvasState { canvas = canvas, mode = canvas.renderMode, camera = canvas.worldCamera,
                        distance = canvas.planeDistance, scale = canvas.scaleFactor, referencePixels = canvas.referencePixelsPerUnit });
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + .02f;
                }
                Canvas.ForceUpdateCanvases();
                // Apply the unchanged scaler configuration to the temporary target dimensions.
                MethodInfo handle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (CanvasState state in canvasStates)
                {
                    CanvasScaler scaler = state.canvas.GetComponent<CanvasScaler>();
                    if (scaler != null && scaler.enabled && handle != null) handle.Invoke(scaler, null);
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                if (!string.IsNullOrEmpty(path))
                {
                    RenderTexture.active = target;
                    frame = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    frame.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    frame.Apply(false, false);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, frame.EncodeToPNG());
                }
            }
            finally
            {
                foreach (CanvasState state in canvasStates)
                {
                    if (state.canvas == null) continue;
                    state.canvas.renderMode = state.mode;
                    state.canvas.worldCamera = state.camera;
                    state.canvas.planeDistance = state.distance;
                    state.canvas.scaleFactor = state.scale;
                    state.canvas.referencePixelsPerUnit = state.referencePixels;
                }
                camera.targetTexture = originalTarget;
                camera.rect = originalRect;
                camera.aspect = originalAspect;
                RenderTexture.active = originalActive;
                Canvas.ForceUpdateCanvases();
                MethodInfo restoreHandle = typeof(CanvasScaler).GetMethod("Handle", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (CanvasState state in canvasStates)
                {
                    if (state.canvas == null) continue;
                    CanvasScaler scaler = state.canvas.GetComponent<CanvasScaler>();
                    if (scaler != null && scaler.enabled && restoreHandle != null) restoreHandle.Invoke(scaler, null);
                    RectTransform rootRect = state.canvas.transform as RectTransform;
                    if (rootRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                }
                Canvas.ForceUpdateCanvases();
                if (frame != null) Destroy(frame);
                target.Release();
                Destroy(target);
            }
        }

        IEnumerator CaptureFrame(string path, Action<string> complete)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, frame.EncodeToPNG());
                complete(path);
            }
            finally
            {
                if (frame != null) Destroy(frame);
            }
        }
    }
}
#endif
