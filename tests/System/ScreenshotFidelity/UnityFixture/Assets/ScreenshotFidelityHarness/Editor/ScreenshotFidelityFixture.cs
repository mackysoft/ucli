using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace MackySoft.Ucli.ScreenshotFidelity
{
    /// <summary> Hosts a visible cube scene and addressable GameView and SceneView windows for screenshot tests. </summary>
    public static class ScreenshotFidelityFixture
    {
        private const string FixtureRootName = "uCLI Screenshot Fidelity Fixture";

        private const string FixtureCameraName = "uCLI Screenshot Fidelity Camera";

        private const string FixtureCubeName = "uCLI Screenshot Fidelity Cube";

        private const string GameWindowTitlePrefix = "uCLI Screenshot Game ";

        private const string SceneWindowTitlePrefix = "uCLI Screenshot Scene ";

        private const int StabilizationUpdateCount = 12;

        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static string runDirectory;

        private static string controlPath;

        private static string responseDirectory;

        private static bool started;

        private static int lastSequence;

        private static PendingControl pendingControl;

        private static EditorWindow gameView;

        private static SceneView sceneView;

        private static GameObject fixtureRoot;

        private static Camera fixtureCamera;

        private static GameObject fixtureCube;

        private static FixtureTarget activeTarget;

        /// <summary> Starts the file-based controller for one isolated system-test directory. </summary>
        /// <param name="directory"> Absolute directory reserved by the system-test runner. </param>
        /// <returns> <see langword="true" /> when the controller starts. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="directory" /> is not absolute. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when another controller directory is active. </exception>
        public static bool Start (string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
            {
                throw new ArgumentException(
                    "Screenshot fidelity run directory must be absolute.",
                    nameof(directory));
            }

            var normalizedDirectory = Path.GetFullPath(directory);
            if (started)
            {
                if (string.Equals(runDirectory, normalizedDirectory, StringComparison.Ordinal))
                {
                    return false;
                }

                throw new InvalidOperationException(
                    $"Screenshot fidelity fixture is already running for another directory: {runDirectory}");
            }

            runDirectory = normalizedDirectory;
            controlPath = Path.Combine(runDirectory, "control.json");
            responseDirectory = Path.Combine(runDirectory, "responses");
            Directory.CreateDirectory(responseDirectory);
            lastSequence = 0;
            pendingControl = null;
            activeTarget = FixtureTarget.None;

            started = true;
            try
            {
                EditorApplication.update -= OnEditorUpdate;
                EditorApplication.update += OnEditorUpdate;
                AssemblyReloadEvents.beforeAssemblyReload -= StopForReload;
                AssemblyReloadEvents.beforeAssemblyReload += StopForReload;

                WriteJsonAtomic(
                    Path.Combine(runDirectory, "unity-environment.json"),
                    CreateEnvironmentSnapshot());
                WriteJsonAtomic(
                    Path.Combine(runDirectory, "fixture-ready.json"),
                    new FixtureReadyResponse
                    {
                        status = "ready",
                        processId = GetCurrentProcessId(),
                        observedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    });
                return true;
            }
            catch
            {
                StopSubscriptions();
                throw;
            }
        }

        private static void OnEditorUpdate ()
        {
            try
            {
                if (pendingControl == null)
                {
                    TryBeginNextControl();
                    return;
                }

                if (pendingControl.repaintWhileWaiting)
                {
                    RepaintFixtureWindows();
                }

                pendingControl.remainingUpdates--;
                if (pendingControl.remainingUpdates <= 0)
                {
                    CompletePendingControl();
                }
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
                var repaintWhileWaiting = true;
                switch (request.action)
                {
                    case "prepareGameCurrent":
                    case "prepareGameRequested":
                        PrepareGameFixture(request);
                        break;
                    case "snapshotGame":
                        RequireWindow(gameView, "GameView");
                        activeTarget = FixtureTarget.Game;
                        break;
                    case "prepareSceneCurrent":
                        PrepareSceneFixture(request);
                        break;
                    case "snapshotScene":
                        RequireWindow(sceneView, "SceneView");
                        activeTarget = FixtureTarget.Scene;
                        break;
                    case "prepareWindowsGameA":
                        PrepareWindowsGameFixture(request, FixtureVisualVariant.A);
                        repaintWhileWaiting = false;
                        break;
                    case "prepareWindowsGameB":
                        RequireWindow(gameView, "GameView");
                        ApplyGameVisualVariant(FixtureVisualVariant.B);
                        activeTarget = FixtureTarget.Game;
                        repaintWhileWaiting = false;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown screenshot fidelity action: {request.action}");
                }

                pendingControl = new PendingControl(
                    request,
                    StabilizationUpdateCount,
                    repaintWhileWaiting);
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
            var response = CreateControlResponse(request);
            ValidateResponse(response);
            response.status = "ready";
            WriteJsonAtomic(ResponsePath(request.sequence), response);
            lastSequence = request.sequence;
            pendingControl = null;
        }

        private static void PrepareGameFixture (ControlRequest request)
        {
            EnsureFixtureObjects();
            OpenGameView(request, requestRepaint: true, showAsStandalone: false);
        }

        private static void PrepareWindowsGameFixture (
            ControlRequest request,
            FixtureVisualVariant variant)
        {
            EnsureFixtureObjects();
            ApplyGameVisualVariant(variant);
            OpenGameView(request, requestRepaint: false, showAsStandalone: true);
        }

        private static void OpenGameView (
            ControlRequest request,
            bool requestRepaint,
            bool showAsStandalone)
        {
            CloseWindow(ref sceneView);
            var gameViewType = ResolveEditorType("UnityEditor.GameView");
            CloseAllWindowsOfType(gameViewType);

            if (showAsStandalone)
            {
                gameView = ScriptableObject.CreateInstance(gameViewType) as EditorWindow;
                if (gameView == null)
                {
                    throw new InvalidOperationException("Could not create the Windows GameView utility window.");
                }
            }
            else
            {
                gameView = EditorWindow.GetWindow(
                    gameViewType,
                    utility: false,
                    title: GameWindowTitlePrefix + request.nonce,
                    focus: true);
            }

            var targetPosition = new Rect(90f, 90f, 760f, 520f);
            gameView.titleContent = new GUIContent(GameWindowTitlePrefix + request.nonce);
            gameView.position = targetPosition;
            gameView.Show();
            if (showAsStandalone)
            {
                // Standalone windows can restore type-specific geometry when shown.
                gameView.position = targetPosition;
            }

            activeTarget = FixtureTarget.Game;
            if (requestRepaint)
            {
                RepaintFixtureWindows();
            }
        }

        private static void PrepareSceneFixture (ControlRequest request)
        {
            EnsureFixtureObjects();
            CloseWindow(ref gameView);
            CloseAllWindowsOfType(typeof(SceneView));

            sceneView = EditorWindow.GetWindow<SceneView>(
                utility: false,
                title: SceneWindowTitlePrefix + request.nonce,
                focus: true);
            sceneView.titleContent = new GUIContent(SceneWindowTitlePrefix + request.nonce);
            sceneView.position = new Rect(90f, 90f, 760f, 520f);
            sceneView.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
            sceneView.in2DMode = false;
            sceneView.LookAtDirect(
                fixtureCube.transform.position,
                Quaternion.Euler(20f, -35f, 0f),
                4f);
            sceneView.orthographic = false;
            sceneView.Show();
            activeTarget = FixtureTarget.Scene;
            RepaintFixtureWindows();
        }

        private static void EnsureFixtureObjects ()
        {
            if (fixtureRoot == null)
            {
                fixtureRoot = GameObject.Find(FixtureRootName);
            }

            if (fixtureRoot != null)
            {
                fixtureCamera = fixtureRoot.GetComponentInChildren<Camera>(includeInactive: true);
                var cubeTransform = fixtureRoot.transform.Find(FixtureCubeName);
                fixtureCube = cubeTransform == null ? null : cubeTransform.gameObject;
            }

            if (fixtureRoot != null && fixtureCamera != null && fixtureCube != null)
            {
                return;
            }

            if (fixtureRoot != null)
            {
                Object.DestroyImmediate(fixtureRoot);
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            fixtureRoot = new GameObject(FixtureRootName);
            SceneManager.MoveGameObjectToScene(fixtureRoot, scene);

            var cameraObject = new GameObject(FixtureCameraName);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.SetParent(fixtureRoot.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -6f);
            cameraObject.transform.rotation = Quaternion.identity;
            fixtureCamera = cameraObject.AddComponent<Camera>();
            fixtureCamera.clearFlags = CameraClearFlags.SolidColor;
            fixtureCamera.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 1f);
            fixtureCamera.fieldOfView = 45f;

            fixtureCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fixtureCube.name = FixtureCubeName;
            SceneManager.MoveGameObjectToScene(fixtureCube, scene);
            fixtureCube.transform.SetParent(fixtureRoot.transform);
            fixtureCube.transform.localPosition = Vector3.zero;
            fixtureCube.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
            Object.DestroyImmediate(fixtureCube.GetComponent<Collider>());

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader == null || !shader.isSupported)
            {
                throw new InvalidOperationException(
                    "The fixture could not resolve a supported unlit shader for the visible cube.");
            }

            var material = new Material(shader)
            {
                name = "uCLI Screenshot Fidelity Cube Material",
                color = new Color(0.95f, 0.35f, 0.08f, 1f),
            };
            fixtureCube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void ApplyGameVisualVariant (FixtureVisualVariant variant)
        {
            RequireFixtureObjects();
            var material = fixtureCube.GetComponent<MeshRenderer>().sharedMaterial;
            if (material == null)
            {
                throw new InvalidOperationException(
                    "The screenshot fidelity cube does not have a material.");
            }

            switch (variant)
            {
                case FixtureVisualVariant.A:
                    fixtureCamera.backgroundColor = new Color(0.04f, 0.07f, 0.12f, 1f);
                    material.color = new Color(0.95f, 0.35f, 0.08f, 1f);
                    fixtureCube.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
                    break;
                case FixtureVisualVariant.B:
                    fixtureCamera.backgroundColor = new Color(0.14f, 0.03f, 0.09f, 1f);
                    material.color = new Color(0.08f, 0.82f, 0.92f, 1f);
                    fixtureCube.transform.localRotation = Quaternion.Euler(-25f, 60f, 12f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(variant), variant, null);
            }
        }

        private static void RequireFixtureObjects ()
        {
            if (fixtureCamera == null || fixtureCube == null)
            {
                throw new InvalidOperationException(
                    "The screenshot fidelity scene objects have not been prepared.");
            }
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

            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void ValidateResponse (ControlResponse response)
        {
            if (string.IsNullOrEmpty(response.windowTitle)
                || response.windowWidth <= 0f
                || response.windowHeight <= 0f)
            {
                throw new InvalidOperationException(
                    "The fixture target window does not expose a visible presentation rectangle.");
            }

            if (activeTarget == FixtureTarget.Game
                && (response.gameTargetWidth <= 0 || response.gameTargetHeight <= 0))
            {
                throw new InvalidOperationException(
                    "The GameView has not produced a positive presentation size.");
            }

            if (activeTarget == FixtureTarget.Scene)
            {
                var viewport = sceneView.cameraViewport;
                if (!IsFinitePositive(viewport.width) || !IsFinitePositive(viewport.height))
                {
                    throw new InvalidOperationException(
                        "The SceneView has not produced a positive content rectangle.");
                }
            }
        }

        private static void RequireWindow (EditorWindow window, string name)
        {
            if (window == null)
            {
                throw new InvalidOperationException($"{name} fixture has not been prepared.");
            }
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
            };

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
            }

            if (gameView != null)
            {
                PopulateGameViewState(gameView, response);
            }

            return response;
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
                    message = $"{exception.Message} Fixture observation also failed: {observationException.Message}",
                };
            }

            response.status = "error";
            response.message ??= exception.Message;
            WriteJsonAtomic(ResponsePath(request.sequence), response);
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
            var pipelinePackage = pipeline == null
                ? null
                : UnityEditor.PackageManager.PackageInfo.FindForAssembly(pipeline.GetType().Assembly);
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
                renderPipelinePackageName = pipelinePackage?.name,
                renderPipelinePackageVersion = pipelinePackage?.version,
            };
        }

        private static int GetCurrentProcessId ()
        {
            using var process = Process.GetCurrentProcess();
            return process.Id;
        }

        private static bool IsFinitePositive (float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }

        private static Type ResolveEditorType (string fullName)
        {
            return typeof(EditorWindow).Assembly.GetType(fullName)
                ?? throw new TypeLoadException(
                    $"Unity Editor type could not be resolved: {fullName}");
        }

        private static PropertyInfo FindProperty (
            Type type,
            string name,
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
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
            if (window == null)
            {
                return;
            }

            window.Close();
            window = null;
        }

        private static string ResponsePath (int sequence)
        {
            return Path.Combine(responseDirectory, $"{sequence:D4}.json");
        }

        private static void WriteJsonAtomic<T> (string path, T value)
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

        private static void StopSubscriptions ()
        {
            EditorApplication.update -= OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= StopForReload;
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
        private sealed class FixtureReadyResponse
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

            public int gameSelectedSizeIndex;

            public int gameSizeCount;

            public int gameTargetWidth;

            public int gameTargetHeight;
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

            public string renderPipelinePackageName;

            public string renderPipelinePackageVersion;
        }

        private sealed class PendingControl
        {
            public PendingControl (
                ControlRequest request,
                int remainingUpdates,
                bool repaintWhileWaiting)
            {
                this.request = request;
                this.remainingUpdates = remainingUpdates;
                this.repaintWhileWaiting = repaintWhileWaiting;
            }

            public ControlRequest request;

            public int remainingUpdates;

            public bool repaintWhileWaiting;
        }

        private enum FixtureVisualVariant
        {
            A = 0,
            B = 1,
        }

        private enum FixtureTarget
        {
            None = 0,
            Game = 1,
            Scene = 2,
        }
    }
}
