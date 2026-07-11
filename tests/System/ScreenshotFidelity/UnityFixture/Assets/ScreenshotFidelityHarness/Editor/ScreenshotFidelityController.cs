using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using DisplayStyle = UnityEngine.UIElements.DisplayStyle;
using Object = UnityEngine.Object;
using Visibility = UnityEngine.UIElements.Visibility;
using VisualElement = UnityEngine.UIElements.VisualElement;

namespace MackySoft.Ucli.ScreenshotFidelity
{
    /// <summary> Owns test-only fixture state and a file control channel for the external system-test runner. </summary>
    internal static class ScreenshotFidelityController
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private const string PatternShaderName = "Hidden/uCLI/ScreenshotFidelityPattern";

        private const string GameWindowTitlePrefix = "uCLI Fidelity Game ";

        private const string SceneWindowTitlePrefix = "uCLI Fidelity Scene ";

        private const int StabilizationUpdateCount = 12;

        private static string runDirectory;

        private static string controlPath;

        private static string responseDirectory;

        private static bool started;

        private static int lastSequence;

        private static PendingControl pendingControl;

        private static EditorWindow gameView;

        private static SceneView sceneView;

        private static Camera baseCamera;

        private static Transform patternTransform;

        private static FidelityFixtureBehaviour fixtureBehaviour;

        private static FixtureTarget activeTarget;

        private static object displayedConfigurableOverlay;

        /// <summary> Starts one idempotent controller for the supplied test-run directory. </summary>
        public static void Start (string directory)
        {
            if (started)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
            {
                throw new ArgumentException("Screenshot fidelity run directory must be absolute.", nameof(directory));
            }

            runDirectory = Path.GetFullPath(directory);
            controlPath = Path.Combine(runDirectory, "control.json");
            responseDirectory = Path.Combine(runDirectory, "responses");
            Directory.CreateDirectory(responseDirectory);

            started = true;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui -= DrawSceneFixtureHandles;
            SceneView.duringSceneGui += DrawSceneFixtureHandles;
            AssemblyReloadEvents.beforeAssemblyReload -= StopForReload;
            AssemblyReloadEvents.beforeAssemblyReload += StopForReload;
            EditorApplication.quitting -= StopForQuit;
            EditorApplication.quitting += StopForQuit;

            WriteJsonAtomic(
                Path.Combine(runDirectory, "unity-environment.json"),
                CreateEnvironmentSnapshot());
            WriteJsonAtomic(
                Path.Combine(runDirectory, "bootstrap-ready.json"),
                new BootstrapResponse
                {
                    status = "ready",
                    processId = GetCurrentProcessId(),
                    observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                });
        }

        private static void OnEditorUpdate ()
        {
            try
            {
                RefreshFixture();
                if (pendingControl != null)
                {
                    RepaintFixtureWindows();
                    pendingControl.remainingUpdates--;
                    if (pendingControl.remainingUpdates <= 0)
                    {
                        CompletePendingControl();
                    }

                    return;
                }

                TryBeginNextControl();
            }
            catch (Exception exception)
            {
                if (pendingControl != null)
                {
                    WriteControlFailure(pendingControl.request, exception);
                    lastSequence = pendingControl.request.sequence;
                    pendingControl = null;
                }

                Debug.LogException(exception);
            }
        }

        private static void TryBeginNextControl ()
        {
            if (!File.Exists(controlPath))
            {
                return;
            }

            ControlRequest request;
            try
            {
                request = JsonUtility.FromJson<ControlRequest>(File.ReadAllText(controlPath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Screenshot fidelity control file is not ready: {exception.Message}");
                return;
            }

            if (request == null || request.sequence <= lastSequence)
            {
                return;
            }

            try
            {
                switch (request.action)
                {
                    case "prepareGameCurrent":
                    case "prepareGameRequested":
                        PrepareGameFixture(request);
                        break;

                    case "snapshotGame":
                        if (gameView == null)
                        {
                            throw new InvalidOperationException("GameView fixture has not been prepared.");
                        }

                        break;

                    case "prepareSceneCurrent":
                        PrepareSceneFixture(request);
                        break;

                    case "snapshotScene":
                        if (sceneView == null)
                        {
                            throw new InvalidOperationException("SceneView fixture has not been prepared.");
                        }

                        break;

                    case "prepareSceneOverlayMenu":
                        PrepareSceneFixture(request);
                        ShowSceneOverlayMenu(sceneView);
                        break;

                    case "prepareSceneConfigurableOverlay":
                        PrepareSceneFixture(request);
                        displayedConfigurableOverlay = ShowConfigurableOverlayPanel(sceneView);
                        break;

                    case "quit":
                        WriteControlSuccess(request);
                        lastSequence = request.sequence;
                        EditorApplication.delayCall += () => EditorApplication.Exit(0);
                        return;

                    default:
                        throw new InvalidOperationException($"Unknown screenshot fidelity action: {request.action}");
                }

                pendingControl = new PendingControl(request, StabilizationUpdateCount);
            }
            catch (Exception exception)
            {
                WriteControlFailure(request, exception);
                lastSequence = request.sequence;
                Debug.LogException(exception);
            }
        }

        private static void CompletePendingControl ()
        {
            var request = pendingControl.request;
            ValidateSceneControlPostcondition();
            WriteControlSuccess(request);
            lastSequence = request.sequence;
            pendingControl = null;
        }

        private static void PrepareGameFixture (ControlRequest request)
        {
            EnsureFixtureScene();
            CloseWindow(ref sceneView);
            CloseAllWindowsOfType(ResolveEditorType("UnityEditor.GameView"));

            var gameViewType = ResolveEditorType("UnityEditor.GameView");
            gameView = EditorWindow.GetWindow(
                gameViewType,
                utility: false,
                title: GameWindowTitlePrefix + request.nonce,
                focus: true);
            gameView.titleContent = new GUIContent(GameWindowTitlePrefix + request.nonce);
            gameView.position = new Rect(90f, 90f, 760f, 520f);
            SetBooleanField(gameView, "m_Gizmos", value: false);
            SetBooleanField(gameView, "m_Stats", value: false);
            gameView.Show();
            gameView.Focus();

            activeTarget = FixtureTarget.Game;
            RefreshFixture();
            RepaintFixtureWindows();
        }

        private static void PrepareSceneFixture (ControlRequest request)
        {
            EnsureFixtureScene();
            CloseWindow(ref gameView);
            CloseAllWindowsOfType(typeof(SceneView));

            sceneView = EditorWindow.GetWindow<SceneView>(
                utility: false,
                title: SceneWindowTitlePrefix + request.nonce,
                focus: true);
            sceneView.titleContent = new GUIContent(SceneWindowTitlePrefix + request.nonce);
            sceneView.position = new Rect(90f, 90f, 760f, 520f);
            sceneView.showGrid = true;
            sceneView.sceneLighting = false;
            sceneView.LookAtDirect(Vector3.zero, Quaternion.identity, 5f);
            sceneView.orthographic = true;
            Selection.activeTransform = patternTransform;
            Tools.current = Tool.Move;
            sceneView.Show();
            sceneView.Focus();

            displayedConfigurableOverlay = null;
            HideConfigurableOverlays(sceneView);

            activeTarget = FixtureTarget.Scene;
            RefreshFixture();
            RepaintFixtureWindows();
        }

        private static void EnsureFixtureScene ()
        {
            if (fixtureBehaviour != null && baseCamera != null && patternTransform != null)
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var patternShader = Shader.Find(PatternShaderName);
            if (patternShader == null || !patternShader.isSupported)
            {
                throw new InvalidOperationException($"Fixture shader is unavailable: {PatternShaderName}");
            }

            var baseCameraObject = new GameObject("Fidelity Base Camera");
            SceneManager.MoveGameObjectToScene(baseCameraObject, scene);
            baseCamera = baseCameraObject.AddComponent<Camera>();
            baseCamera.orthographic = true;
            baseCamera.orthographicSize = 5f;
            baseCamera.transform.position = new Vector3(0f, 0f, -10f);
            baseCamera.transform.rotation = Quaternion.identity;
            baseCamera.clearFlags = CameraClearFlags.SolidColor;
            baseCamera.backgroundColor = Color.black;
            baseCamera.allowHDR = false;
            baseCamera.allowMSAA = false;
            baseCamera.cullingMask = ~(1 << 30);
            baseCamera.depth = 0f;

            var patternObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            patternObject.name = "Fidelity Presentation Pattern";
            SceneManager.MoveGameObjectToScene(patternObject, scene);
            patternObject.transform.position = Vector3.zero;
            patternTransform = patternObject.transform;
            Object.DestroyImmediate(patternObject.GetComponent<Collider>());
            var patternMaterial = new Material(patternShader)
            {
                name = "Fidelity Pattern Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            patternObject.GetComponent<MeshRenderer>().sharedMaterial = patternMaterial;

            var overlayCameraObject = new GameObject("Fidelity Overlay Camera");
            SceneManager.MoveGameObjectToScene(overlayCameraObject, scene);
            var overlayCamera = overlayCameraObject.AddComponent<Camera>();
            overlayCamera.orthographic = true;
            overlayCamera.orthographicSize = 5f;
            overlayCamera.transform.position = new Vector3(0f, 0f, -10f);
            overlayCamera.transform.rotation = Quaternion.identity;
            overlayCamera.clearFlags = CameraClearFlags.Nothing;
            overlayCamera.allowHDR = false;
            overlayCamera.allowMSAA = false;
            overlayCamera.cullingMask = 1 << 30;
            overlayCamera.depth = 1f;

            var baseCameraData = baseCamera.GetUniversalAdditionalCameraData();
            baseCameraData.renderType = CameraRenderType.Base;
            baseCameraData.renderPostProcessing = true;
            var overlayCameraData = overlayCamera.GetUniversalAdditionalCameraData();
            overlayCameraData.renderType = CameraRenderType.Overlay;
            baseCameraData.cameraStack.Clear();
            baseCameraData.cameraStack.Add(overlayCamera);

            var overlayMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlayMarker.name = "Fidelity Camera Stack Marker";
            SceneManager.MoveGameObjectToScene(overlayMarker, scene);
            overlayMarker.layer = 30;
            overlayMarker.transform.position = new Vector3(0f, 3.55f, -0.1f);
            overlayMarker.transform.localScale = new Vector3(1.35f, 0.36f, 1f);
            Object.DestroyImmediate(overlayMarker.GetComponent<Collider>());
            var overlayMaterial = new Material(patternShader)
            {
                name = "Fidelity Overlay Marker Material",
                hideFlags = HideFlags.HideAndDontSave,
            };
            overlayMaterial.SetFloat("_UseSolid", 1f);
            overlayMaterial.SetColor("_SolidColor", new Color(1f, 0f, 0.72f, 1f));
            overlayMarker.GetComponent<MeshRenderer>().sharedMaterial = overlayMaterial;

            CreatePostProcessVolume(scene);
            var resolutionBits = CreatePresentationCanvas(scene);

            var behaviourObject = new GameObject("Fidelity Runtime IMGUI Marker");
            SceneManager.MoveGameObjectToScene(behaviourObject, scene);
            fixtureBehaviour = behaviourObject.AddComponent<FidelityFixtureBehaviour>();
            fixtureBehaviour.Configure(baseCamera, patternTransform, resolutionBits);

            Selection.activeGameObject = overlayMarker;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void CreatePostProcessVolume (Scene scene)
        {
            var volumeObject = new GameObject("Fidelity Post Process Volume");
            SceneManager.MoveGameObjectToScene(volumeObject, scene);
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile.hideFlags = HideFlags.HideAndDontSave;

            var colorAdjustments = volume.sharedProfile.Add<ColorAdjustments>(overrides: true);
            colorAdjustments.postExposure.Override(0.25f);
            colorAdjustments.contrast.Override(8f);
            colorAdjustments.colorFilter.Override(new Color(1f, 0.96f, 0.9f, 1f));

            var whiteBalance = volume.sharedProfile.Add<WhiteBalance>(overrides: true);
            whiteBalance.temperature.Override(12f);
            whiteBalance.tint.Override(-4f);
        }

        private static Image[] CreatePresentationCanvas (Scene scene)
        {
            var canvasObject = new GameObject(
                "Fidelity Presentation UI",
                typeof(Canvas),
                typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            CreateCanvasColorSamples(canvas.transform);
            CreateImage(canvas.transform, "Top Left Sentinel", Color.red, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, 16f), Vector2.zero);
            CreateImage(canvas.transform, "Top Right Sentinel", Color.green, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(16f, 16f), Vector2.zero);
            CreateImage(canvas.transform, "Bottom Left Sentinel", Color.blue, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), Vector2.zero);
            CreateImage(canvas.transform, "Bottom Right Sentinel", Color.yellow, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(16f, 16f), Vector2.zero);
            CreateImage(canvas.transform, "Top Border", Color.cyan, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -0.5f));
            CreateImage(canvas.transform, "Bottom Border", Color.magenta, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            CreateImage(canvas.transform, "Left Border", new Color(1f, 0.5f, 0f, 1f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0.5f, 0f));
            CreateImage(canvas.transform, "Right Border", new Color(0.45f, 0f, 1f, 1f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-0.5f, 0f));

            CreateImage(canvas.transform, "Resolution Anchor", Color.yellow, Vector2.zero, Vector2.zero, new Vector2(6f, 10f), new Vector2(31f, 18f));
            var bits = new Image[20];
            for (var index = 0; index < bits.Length; index++)
            {
                var cellIndex = index + (index >= 10 ? 1 : 0);
                bits[index] = CreateImage(
                    canvas.transform,
                    $"Resolution Bit {index:00}",
                    Color.magenta,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(6f, 10f),
                    new Vector2(39f + cellIndex * 8f, 18f));
            }

            return bits;
        }

        private static void CreateCanvasColorSamples (Transform parent)
        {
            const int grayStepCount = 17;
            for (var index = 0; index < grayStepCount; index++)
            {
                var gray = index / (grayStepCount - 1f);
                CreateImage(
                    parent,
                    $"Gray Step {index:00}",
                    new Color(gray, gray, gray, 1f),
                    new Vector2(index / (float)grayStepCount, 0.41f),
                    new Vector2((index + 1f) / grayStepCount, 0.59f),
                    Vector2.zero,
                    Vector2.zero);
            }

            CreateImage(parent, "Warm Color Sample", new Color(0.62f, 0.11f, 0.055f, 1f), new Vector2(0.12f, 0.22f), new Vector2(0.22f, 0.36f), Vector2.zero, Vector2.zero);
            CreateImage(parent, "Green Color Sample", new Color(0.08f, 0.48f, 0.16f, 1f), new Vector2(0.30f, 0.22f), new Vector2(0.40f, 0.36f), Vector2.zero, Vector2.zero);
            CreateImage(parent, "Skin Color Sample", new Color(0.55f, 0.25f, 0.16f, 1f), new Vector2(0.48f, 0.22f), new Vector2(0.58f, 0.36f), Vector2.zero, Vector2.zero);
            CreateImage(parent, "Blue Color Sample", new Color(0.07f, 0.18f, 0.68f, 1f), new Vector2(0.66f, 0.22f), new Vector2(0.76f, 0.36f), Vector2.zero, Vector2.zero);
            CreateImage(parent, "Gradient Sample A", new Color(0.2528f, 0.4856f, 0.4066f, 1f), new Vector2(0.24f, 0.72f), new Vector2(0.30f, 0.80f), Vector2.zero, Vector2.zero);
            CreateImage(parent, "Gradient Sample B", new Color(0.5472f, 0.4856f, 0.2134f, 1f), new Vector2(0.70f, 0.72f), new Vector2(0.76f, 0.80f), Vector2.zero, Vector2.zero);
        }

        private static Image CreateImage (
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, worldPositionStays: false);
            var rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = sizeDelta;
            rectTransform.anchoredPosition = anchoredPosition;
            var image = imageObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void RefreshFixture ()
        {
            if (fixtureBehaviour == null || patternTransform == null)
            {
                return;
            }

            fixtureBehaviour.RefreshPresentationMarkers();
            if (activeTarget == FixtureTarget.Scene && sceneView != null && sceneView.camera != null)
            {
                var camera = sceneView.camera;
                var height = camera.orthographicSize * 2f;
                patternTransform.localScale = new Vector3(height * camera.aspect, height, 1f);
            }

            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void RepaintFixtureWindows ()
        {
            if (gameView != null)
            {
                gameView.Repaint();
            }

            if (sceneView != null)
            {
                sceneView.Repaint();
            }
        }

        private static void DrawSceneFixtureHandles (SceneView view)
        {
            if (activeTarget != FixtureTarget.Scene || sceneView == null || view != sceneView)
            {
                return;
            }

            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var previousColor = Handles.color;
            try
            {
                Handles.color = Color.white;
                Handles.DrawWireDisc(new Vector3(2.4f, 2.4f, -0.2f), Vector3.forward, 0.42f, 3f);
                Handles.color = Color.cyan;
                Handles.ArrowHandleCap(
                    controlID: 0,
                    position: new Vector3(-2.2f, 2.2f, -0.2f),
                    rotation: Quaternion.identity,
                    size: 0.7f,
                    eventType: EventType.Repaint);
            }
            finally
            {
                Handles.color = previousColor;
            }

            DrawSceneScreenFixture();
        }

        private static void DrawSceneScreenFixture ()
        {
            Handles.BeginGUI();
            try
            {
                var viewport = ResolveCurrentGuiClipRect();
                const float sentinelSize = 16f;
                EditorGUI.DrawRect(
                    new Rect(viewport.xMin, viewport.yMin, sentinelSize, sentinelSize),
                    Color.cyan);
                EditorGUI.DrawRect(
                    new Rect(
                        viewport.xMin,
                        viewport.yMax - sentinelSize,
                        sentinelSize,
                        sentinelSize),
                    Color.magenta);
                EditorGUI.DrawRect(
                    new Rect(
                        viewport.xMax - sentinelSize,
                        viewport.yMax - sentinelSize,
                        sentinelSize,
                        sentinelSize),
                    Color.yellow);

                const int grayStepCount = 17;
                for (var index = 0; index < grayStepCount; index++)
                {
                    var gray = index / (grayStepCount - 1f);
                    DrawNormalizedSceneRect(
                        viewport,
                        new Rect(
                            index / (float)grayStepCount,
                            0.41f,
                            1f / grayStepCount,
                            0.18f),
                        new Color(gray, gray, gray, 1f));
                }

                DrawNormalizedSceneRect(viewport, new Rect(0.12f, 0.64f, 0.10f, 0.14f), new Color(0.62f, 0.11f, 0.055f, 1f));
                DrawNormalizedSceneRect(viewport, new Rect(0.30f, 0.64f, 0.10f, 0.14f), new Color(0.08f, 0.48f, 0.16f, 1f));
                DrawNormalizedSceneRect(viewport, new Rect(0.48f, 0.64f, 0.10f, 0.14f), new Color(0.55f, 0.25f, 0.16f, 1f));
                DrawNormalizedSceneRect(viewport, new Rect(0.66f, 0.64f, 0.10f, 0.14f), new Color(0.07f, 0.18f, 0.68f, 1f));
                DrawNormalizedSceneRect(viewport, new Rect(0.24f, 0.20f, 0.06f, 0.08f), new Color(0.2528f, 0.4856f, 0.4066f, 1f));
                DrawNormalizedSceneRect(viewport, new Rect(0.70f, 0.20f, 0.06f, 0.08f), new Color(0.5472f, 0.4856f, 0.2134f, 1f));
            }
            finally
            {
                Handles.EndGUI();
            }
        }

        private static void DrawNormalizedSceneRect (
            Rect viewport,
            Rect normalizedRect,
            Color color)
        {
            EditorGUI.DrawRect(
                new Rect(
                    viewport.x + normalizedRect.x * viewport.width,
                    viewport.y + normalizedRect.y * viewport.height,
                    normalizedRect.width * viewport.width,
                    normalizedRect.height * viewport.height),
                color);
        }

        private static Rect ResolveCurrentGuiClipRect ()
        {
            var guiClipType = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip");
            var visibleRectProperty = guiClipType == null
                ? null
                : FindProperty(guiClipType, "visibleRect", StaticMembers);
            if (visibleRectProperty?.GetValue(null) is not Rect visibleRect
                || !IsFinite(visibleRect.x)
                || !IsFinite(visibleRect.y)
                || !IsFinitePositive(visibleRect.width)
                || !IsFinitePositive(visibleRect.height))
            {
                throw new InvalidOperationException(
                    "The active OnSceneGUI clip rectangle cannot be observed by this fixture environment.");
            }

            return visibleRect;
        }

        private static void WriteControlSuccess (ControlRequest request)
        {
            var response = CreateControlResponse(request);
            response.status = "ready";
            WriteJsonAtomic(ResponsePath(request.sequence), response);
        }

        private static void WriteControlFailure (ControlRequest request, Exception exception)
        {
            ControlResponse response;
            try
            {
                response = CreateControlResponse(request);
            }
            catch (Exception observationException)
            {
                response = new ControlResponse
                {
                    sequence = request.sequence,
                    action = request.action,
                    nonce = request.nonce,
                    processId = GetCurrentProcessId(),
                    observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    displayedExcludedOverlays = Array.Empty<string>(),
                    message = $"{exception.Message} Fixture state observation also failed: {observationException.Message}",
                };
            }

            response.status = "error";
            response.message ??= exception.Message;
            WriteJsonAtomic(ResponsePath(request.sequence), response);
        }

        private static ControlResponse CreateControlResponse (ControlRequest request)
        {
            var response = new ControlResponse
            {
                sequence = request.sequence,
                action = request.action,
                nonce = request.nonce,
                processId = GetCurrentProcessId(),
                observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                overlayMenuDisplayed = IsOverlayMenuDisplayed(),
            };

            if (activeTarget == FixtureTarget.Scene && sceneView != null)
            {
                response.displayedExcludedOverlays = GetDisplayedExcludedOverlayNames(sceneView);
                response.displayedExcludedOverlayCount = response.displayedExcludedOverlays.Length;
                response.sceneTool = Tools.current.ToString();
                response.sceneSelectionInstanceId = Selection.activeInstanceID;
            }
            else
            {
                response.displayedExcludedOverlays = Array.Empty<string>();
            }

            var window = activeTarget == FixtureTarget.Game
                ? gameView
                : sceneView;
            if (window != null)
            {
                response.windowTitle = window.titleContent.text;
                response.windowInstanceId = window.GetInstanceID();
                response.windowX = window.position.x;
                response.windowY = window.position.y;
                response.windowWidth = window.position.width;
                response.windowHeight = window.position.height;
                TryResolveHostState(window, out response.backingScale, out response.hdrActive);
            }

            if (gameView != null)
            {
                PopulateGameViewState(gameView, response);
            }

            if (baseCamera != null)
            {
                response.fixturePixelWidth = baseCamera.pixelWidth;
                response.fixturePixelHeight = baseCamera.pixelHeight;
            }

            return response;
        }

        private static void PopulateGameViewState (
            EditorWindow window,
            ControlResponse response)
        {
            var gameViewType = window.GetType();
            var selectedSizeIndexProperty = FindProperty(gameViewType, "selectedSizeIndex");
            var targetRenderSizeProperty = FindProperty(gameViewType, "targetRenderSize");
            if (selectedSizeIndexProperty?.GetValue(window) is int selectedSizeIndex)
            {
                response.gameSelectedSizeIndex = selectedSizeIndex;
            }

            if (targetRenderSizeProperty?.GetValue(window) is Vector2 targetRenderSize)
            {
                response.gameTargetWidth = Mathf.RoundToInt(targetRenderSize.x);
                response.gameTargetHeight = Mathf.RoundToInt(targetRenderSize.y);
            }

            var gameViewSizesType = ResolveEditorType("UnityEditor.GameViewSizes");
            var instanceProperty = FindProperty(gameViewSizesType, "instance", StaticMembers);
            var currentGroupProperty = FindProperty(gameViewSizesType, "currentGroup");
            var instance = instanceProperty?.GetValue(null);
            var group = instance == null ? null : currentGroupProperty?.GetValue(instance);
            var getTotalCount = group == null
                ? null
                : FindMethod(group.GetType(), "GetTotalCount", Type.EmptyTypes);
            if (group != null && getTotalCount?.Invoke(group, null) is int totalCount)
            {
                response.gameSizeCount = totalCount;
            }
        }

        private static EnvironmentSnapshot CreateEnvironmentSnapshot ()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            return new EnvironmentSnapshot
            {
                observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                processId = GetCurrentProcessId(),
                unityVersion = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                platform = Application.platform.ToString(),
                graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                graphicsMemorySizeMb = SystemInfo.graphicsMemorySize,
                maxTextureSize = SystemInfo.maxTextureSize,
                colorSpace = QualitySettings.activeColorSpace.ToString(),
                renderPipelineType = pipeline == null ? "built-in" : pipeline.GetType().FullName,
                renderPipelineAssemblyVersion = pipeline == null
                    ? null
                    : pipeline.GetType().Assembly.GetName().Version?.ToString(),
            };
        }

        private static int GetCurrentProcessId ()
        {
            using var process = Process.GetCurrentProcess();
            return process.Id;
        }

        private static bool TryResolveHostState (
            EditorWindow window,
            out float backingScale,
            out bool hdrActive)
        {
            backingScale = 0f;
            hdrActive = false;
            var parentField = FindField(window.GetType(), "m_Parent");
            var parent = parentField?.GetValue(window);
            if (parent == null)
            {
                return false;
            }

            var hdrActiveProperty = FindProperty(parent.GetType(), "hdrActive");
            var backingScaleMethod = FindMethod(parent.GetType(), "GetBackingScaleFactor", Type.EmptyTypes);
            if (hdrActiveProperty?.GetValue(parent) is not bool resolvedHdr
                || backingScaleMethod?.Invoke(parent, null) is not float resolvedScale)
            {
                return false;
            }

            backingScale = resolvedScale;
            hdrActive = resolvedHdr;
            return true;
        }

        private static bool IsOverlayMenuDisplayed ()
        {
            if (sceneView == null)
            {
                return false;
            }

            var canvas = sceneView.overlayCanvas;
            var canvasRootVisualElementProperty = canvas == null
                ? null
                : FindProperty(canvas.GetType(), "rootVisualElement");
            if (canvasRootVisualElementProperty?.GetValue(canvas)
                    is VisualElement canvasRootVisualElement
                && FindDisplayedOverlayMenuControl(canvasRootVisualElement) != null)
            {
                return true;
            }

            if (FindDisplayedOverlayMenuControl(sceneView.rootVisualElement) != null)
            {
                return true;
            }

            var parentField = FindField(sceneView.GetType(), "m_Parent");
            var parent = parentField?.GetValue(sceneView);
            var visualTreeProperty = parent == null
                ? null
                : FindProperty(parent.GetType(), "visualTree");
            return visualTreeProperty?.GetValue(parent) is VisualElement visualTree
                && FindDisplayedOverlayMenuControl(visualTree) != null;
        }

        private static void HideConfigurableOverlays (SceneView targetSceneView)
        {
            var canvas = targetSceneView.overlayCanvas;
            var includedOrientationGizmo = ResolveIncludedOrientationGizmo(targetSceneView);
            var overlaysEnabledProperty = FindProperty(canvas.GetType(), "overlaysEnabled");
            var overlaysProperty = FindProperty(canvas.GetType(), "overlays");
            if (overlaysEnabledProperty == null
                || overlaysProperty?.GetValue(canvas) is not IEnumerable overlays)
            {
                throw new InvalidOperationException("SceneView configurable Overlay collection could not be resolved.");
            }

            SetOverlaysEnabled(canvas, overlaysEnabledProperty, enabled: true);

            foreach (var overlay in overlays)
            {
                if (overlay == null)
                {
                    continue;
                }

                var displayedProperty = FindProperty(overlay.GetType(), "displayed");
                if (displayedProperty == null
                    || !displayedProperty.CanWrite
                    || displayedProperty.GetValue(overlay) is not bool)
                {
                    throw new InvalidOperationException(
                        $"SceneView Overlay visibility could not be controlled: {overlay.GetType().FullName}");
                }

                var expectedDisplayed = ReferenceEquals(overlay, includedOrientationGizmo);
                displayedProperty.SetValue(overlay, expectedDisplayed);
                if (displayedProperty.GetValue(overlay) is not bool displayed
                    || displayed != expectedDisplayed)
                {
                    throw new InvalidOperationException(
                        $"SceneView Overlay could not enter its fixture state: {GetOverlayIdentity(overlay)}");
                }
            }
        }

        private static object ShowConfigurableOverlayPanel (SceneView targetSceneView)
        {
            var canvas = targetSceneView.overlayCanvas;
            var includedOrientationGizmo = ResolveIncludedOrientationGizmo(targetSceneView);
            var overlaysEnabledProperty = FindProperty(canvas.GetType(), "overlaysEnabled");
            var overlaysProperty = FindProperty(canvas.GetType(), "overlays");
            if (overlaysEnabledProperty == null
                || overlaysProperty?.GetValue(canvas) is not IEnumerable overlays)
            {
                throw new InvalidOperationException(
                    "SceneView configurable Overlay capability cannot be controlled by this fixture environment.");
            }

            SetOverlaysEnabled(canvas, overlaysEnabledProperty, enabled: true);

            var candidates = new List<object>();
            foreach (var overlay in overlays)
            {
                if (overlay != null && !ReferenceEquals(overlay, includedOrientationGizmo))
                {
                    candidates.Add(overlay);
                }
            }

            candidates.Sort((left, right) => string.CompareOrdinal(
                GetOverlayIdentity(left),
                GetOverlayIdentity(right)));
            foreach (var overlay in candidates)
            {
                var displayedProperty = FindProperty(overlay.GetType(), "displayed");
                var activeLayoutProperty = FindProperty(overlay.GetType(), "activeLayout");
                var rootVisualElementProperty = FindProperty(overlay.GetType(), "rootVisualElement");
                if (displayedProperty == null
                    || !displayedProperty.CanWrite
                    || activeLayoutProperty?.GetValue(overlay)?.ToString() != "Panel"
                    || rootVisualElementProperty?.GetValue(overlay) is not VisualElement)
                {
                    continue;
                }

                displayedProperty.SetValue(overlay, true);
                if (displayedProperty.GetValue(overlay) is bool displayed && displayed)
                {
                    targetSceneView.Repaint();
                    return overlay;
                }
            }

            throw new InvalidOperationException(
                "No configurable SceneView Overlay panel can be displayed by this fixture environment.");
        }

        private static void SetOverlaysEnabled (
            object canvas,
            PropertyInfo overlaysEnabledProperty,
            bool enabled)
        {
            var setter = overlaysEnabledProperty.GetSetMethod(nonPublic: true);
            if (setter != null)
            {
                setter.Invoke(canvas, new object[] { enabled });
            }
            else
            {
                var setOverlaysEnabledMethod = FindMethod(
                    canvas.GetType(),
                    "SetOverlaysEnabled",
                    new[] { typeof(bool) });
                if (setOverlaysEnabledMethod == null
                    || setOverlaysEnabledMethod.ReturnType != typeof(void))
                {
                    throw new InvalidOperationException(
                        "SceneView Overlay presentation cannot be controlled by this fixture environment.");
                }

                setOverlaysEnabledMethod.Invoke(canvas, new object[] { enabled });
            }

            if (overlaysEnabledProperty.GetValue(canvas) is not bool actualEnabled
                || actualEnabled != enabled)
            {
                throw new InvalidOperationException(
                    "SceneView Overlay presentation did not enter the requested fixture state.");
            }
        }

        private static string[] GetDisplayedExcludedOverlayNames (SceneView targetSceneView)
        {
            var canvas = targetSceneView.overlayCanvas;
            var includedOrientationGizmo = ResolveIncludedOrientationGizmo(targetSceneView);
            var overlaysEnabledProperty = FindProperty(canvas.GetType(), "overlaysEnabled");
            var overlaysProperty = FindProperty(canvas.GetType(), "overlays");
            if (overlaysEnabledProperty?.GetValue(canvas) is not bool overlaysEnabled
                || overlaysProperty?.GetValue(canvas) is not IEnumerable overlays)
            {
                throw new InvalidOperationException(
                    "SceneView configurable Overlay visibility cannot be observed by this fixture environment.");
            }

            if (!overlaysEnabled)
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            foreach (var overlay in overlays)
            {
                if (overlay == null || ReferenceEquals(overlay, includedOrientationGizmo))
                {
                    continue;
                }

                var displayedProperty = FindProperty(overlay.GetType(), "displayed");
                if (displayedProperty?.GetValue(overlay) is not bool displayed)
                {
                    throw new InvalidOperationException(
                        $"SceneView Overlay visibility cannot be observed: {GetOverlayIdentity(overlay)}");
                }

                if (displayed)
                {
                    names.Add(GetOverlayIdentity(overlay));
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static object ResolveIncludedOrientationGizmo (SceneView targetSceneView)
        {
            var orientationGizmoField = FindField(typeof(SceneView), "m_OrientationGizmo");
            return orientationGizmoField?.GetValue(targetSceneView)
                ?? throw new InvalidOperationException(
                    "SceneView included orientation gizmo identity could not be resolved by this fixture environment.");
        }

        private static string GetOverlayIdentity (object overlay)
        {
            var displayNameProperty = FindProperty(overlay.GetType(), "displayName");
            return displayNameProperty?.GetValue(overlay) is string displayName
                && !string.IsNullOrWhiteSpace(displayName)
                    ? $"{overlay.GetType().FullName}:{displayName}"
                    : overlay.GetType().FullName;
        }

        private static void ValidateSceneControlPostcondition ()
        {
            if (activeTarget != FixtureTarget.Scene || sceneView == null)
            {
                return;
            }

            var displayedExcludedOverlays = GetDisplayedExcludedOverlayNames(sceneView);
            if (displayedConfigurableOverlay != null)
            {
                if (displayedConfigurableOverlay == null)
                {
                    throw new InvalidOperationException(
                        "The configurable SceneView Overlay panel fixture was not selected.");
                }

                var expectedIdentity = GetOverlayIdentity(displayedConfigurableOverlay);
                var rootVisualElementProperty = FindProperty(
                    displayedConfigurableOverlay.GetType(),
                    "rootVisualElement");
                if (displayedExcludedOverlays.Length != 1
                    || !string.Equals(
                        displayedExcludedOverlays[0],
                        expectedIdentity,
                        StringComparison.Ordinal)
                    || rootVisualElementProperty?.GetValue(displayedConfigurableOverlay)
                        is not VisualElement rootVisualElement
                    || !IsDisplayed(rootVisualElement))
                {
                    throw new InvalidOperationException(
                        $"The configurable SceneView Overlay panel did not become visibly displayed: {expectedIdentity}");
                }

                return;
            }

            if (displayedExcludedOverlays.Length != 0)
            {
                throw new InvalidOperationException(
                    "The SceneView fixture unexpectedly displays excluded configurable Overlays: "
                    + string.Join(", ", displayedExcludedOverlays));
            }
        }

        private static VisualElement FindDisplayedOverlayMenuControl (VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            if (IsOverlayMenuControl(root) && IsDisplayed(root))
            {
                return root;
            }

            foreach (var child in root.Children())
            {
                var match = FindDisplayedOverlayMenuControl(child);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static bool IsOverlayMenuControl (VisualElement element)
        {
            return string.Equals(element.name, "overlay-menu", StringComparison.Ordinal)
                || element.ClassListContains("overlay-menu");
        }

        private static void ShowSceneOverlayMenu (SceneView targetSceneView)
        {
            var canvas = targetSceneView.overlayCanvas;
            var showMenuMethod = FindMethod(
                canvas.GetType(),
                "ShowMenu",
                new[] { typeof(bool), typeof(bool) });
            if (showMenuMethod == null || showMenuMethod.ReturnType != typeof(void))
            {
                throw new InvalidOperationException("SceneView Overlay Menu visibility cannot be controlled by this fixture environment.");
            }

            showMenuMethod.Invoke(canvas, new object[] { true, false });
        }

        private static bool IsDisplayed (VisualElement element)
        {
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None
                    || current.resolvedStyle.visibility == Visibility.Hidden
                    || current.resolvedStyle.opacity <= 0f)
                {
                    return false;
                }
            }

            return element.worldBound.width > 0f && element.worldBound.height > 0f;
        }

        private static bool IsFinite (float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinitePositive (float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static void SetBooleanField (
            EditorWindow window,
            string fieldName,
            bool value)
        {
            var field = FindField(window.GetType(), fieldName)
                ?? throw new MissingFieldException(window.GetType().FullName, fieldName);
            field.SetValue(window, value);
        }

        private static Type ResolveEditorType (string fullName)
        {
            return typeof(EditorWindow).Assembly.GetType(fullName)
                ?? throw new TypeLoadException($"Unity Editor type could not be resolved: {fullName}");
        }

        private static FieldInfo FindField (
            Type type,
            string name)
        {
            while (type != null)
            {
                var field = type.GetField(name, InstanceMembers);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static PropertyInfo FindProperty (
            Type type,
            string name,
            BindingFlags bindingFlags = InstanceMembers)
        {
            while (type != null)
            {
                var property = type.GetProperty(name, bindingFlags);
                if (property != null)
                {
                    return property;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethod (
            Type type,
            string name,
            Type[] parameterTypes)
        {
            while (type != null)
            {
                var method = type.GetMethod(
                    name,
                    InstanceMembers,
                    binder: null,
                    parameterTypes,
                    modifiers: null);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static void CloseAllWindowsOfType (Type type)
        {
            foreach (var windowObject in Resources.FindObjectsOfTypeAll(type))
            {
                if (windowObject is EditorWindow window && window != null)
                {
                    window.Close();
                }
            }
        }

        private static void CloseWindow<T> (ref T window)
            where T : EditorWindow
        {
            if (window != null)
            {
                window.Close();
                window = null;
            }
        }

        private static string ResponsePath (int sequence)
        {
            return Path.Combine(responseDirectory, $"{sequence:D4}.json");
        }

        private static void WriteJsonAtomic<T> (
            string path,
            T value)
        {
            var temporaryPath = path + $".tmp.{Guid.NewGuid():N}";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(value, prettyPrint: true));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static void StopForReload ()
        {
            StopSubscriptions();
        }

        private static void StopForQuit ()
        {
            StopSubscriptions();
        }

        private static void StopSubscriptions ()
        {
            EditorApplication.update -= OnEditorUpdate;
            SceneView.duringSceneGui -= DrawSceneFixtureHandles;
            AssemblyReloadEvents.beforeAssemblyReload -= StopForReload;
            EditorApplication.quitting -= StopForQuit;
            started = false;
        }

        [Serializable]
        private sealed class ControlRequest
        {
            public int sequence;

            public string action;

            public string nonce;
        }

        [Serializable]
        private sealed class BootstrapResponse
        {
            public string status;

            public int processId;

            public string observedAtUtc;
        }

        [Serializable]
        private sealed class ControlResponse
        {
            public int sequence;

            public string action;

            public string status;

            public string message;

            public string nonce;

            public int processId;

            public string observedAtUtc;

            public string windowTitle;

            public int windowInstanceId;

            public float windowX;

            public float windowY;

            public float windowWidth;

            public float windowHeight;

            public float backingScale;

            public bool hdrActive;

            public bool overlayMenuDisplayed;

            public int displayedExcludedOverlayCount;

            public string[] displayedExcludedOverlays;

            public string sceneTool;

            public int sceneSelectionInstanceId;

            public int gameSelectedSizeIndex;

            public int gameSizeCount;

            public int gameTargetWidth;

            public int gameTargetHeight;

            public int fixturePixelWidth;

            public int fixturePixelHeight;
        }

        [Serializable]
        private sealed class EnvironmentSnapshot
        {
            public string observedAtUtc;

            public int processId;

            public string unityVersion;

            public string operatingSystem;

            public string platform;

            public string graphicsDeviceType;

            public string graphicsDeviceName;

            public string graphicsDeviceVendor;

            public string graphicsDeviceVersion;

            public int graphicsMemorySizeMb;

            public int maxTextureSize;

            public string colorSpace;

            public string renderPipelineType;

            public string renderPipelineAssemblyVersion;
        }

        private sealed class PendingControl
        {
            public PendingControl (
                ControlRequest request,
                int remainingUpdates)
            {
                this.request = request;
                this.remainingUpdates = remainingUpdates;
            }

            public ControlRequest request;

            public int remainingUpdates;
        }

        private enum FixtureTarget
        {
            None = 0,
            Game = 1,
            Scene = 2,
        }
    }
}
