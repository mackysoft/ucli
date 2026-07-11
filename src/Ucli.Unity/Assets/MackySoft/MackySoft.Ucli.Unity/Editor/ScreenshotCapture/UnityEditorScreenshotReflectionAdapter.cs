using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Isolates version-sensitive Unity Editor presentation-surface members. </summary>
    internal sealed class UnityEditorScreenshotReflectionAdapter
    {
        private const BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private const BindingFlags StaticMembers =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        private readonly Assembly unityEditorAssembly = typeof(EditorWindow).Assembly;

        /// <summary> Resolves the existing main GameView presentation source. </summary>
        public bool TryGetGameViewSource (
            out GameViewSource source,
            out string errorMessage)
        {
            source = null;
            if (!UnityScreenshotRuntimeCapabilityPolicy.TryValidateCurrentEnvironment(out errorMessage))
            {
                return false;
            }

            try
            {
                var playModeViewType = unityEditorAssembly.GetType("UnityEditor.PlayModeView");
                var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
                var getMainPlayModeView = playModeViewType?.GetMethod(
                    "GetMainPlayModeView",
                    StaticMembers,
                    binder: null,
                    Type.EmptyTypes,
                    modifiers: null);
                if (playModeViewType == null || gameViewType == null || getMainPlayModeView == null)
                {
                    errorMessage = "Unity GameView adapter members are unavailable.";
                    return false;
                }

                var viewObject = getMainPlayModeView.Invoke(obj: null, parameters: null);
                if (viewObject is not EditorWindow gameView
                    || gameView == null
                    || viewObject.GetType() != gameViewType)
                {
                    errorMessage = "An existing main GameView is required; another PlayModeView cannot be substituted.";
                    return false;
                }

                if (!TryGetSelectedHostState(
                    gameView,
                    out _,
                    out var backingScale,
                    out var hdrActive,
                    out errorMessage))
                {
                    return false;
                }

                if (hdrActive)
                {
                    errorMessage = "HDR GameView presentation is not supported by the SDR screenshot backend.";
                    return false;
                }

                var gizmosField = FindField(gameViewType, "m_Gizmos");
                if (gizmosField == null || gizmosField.GetValue(gameView) is not bool gizmosEnabled)
                {
                    errorMessage = "GameView Gizmos state could not be resolved.";
                    return false;
                }

                if (gizmosEnabled)
                {
                    errorMessage = "GameView Editor Gizmos are enabled and cannot be removed without changing the observed presentation.";
                    return false;
                }

                var renderTextureField = FindField(gameViewType, "m_RenderTexture");
                var targetRenderSizeProperty = FindProperty(gameViewType, "targetRenderSize");
                var targetDisplayProperty = FindProperty(gameViewType, "targetDisplay");
                var targetInViewProperty = FindProperty(gameViewType, "targetInView");
                var deviceFlippedTargetInViewProperty = FindProperty(
                    gameViewType,
                    "deviceFlippedTargetInView");
                if (renderTextureField?.GetValue(gameView) is not RenderTexture renderTexture
                    || renderTexture == null
                    || !renderTexture.IsCreated()
                    || targetRenderSizeProperty?.GetValue(gameView) is not Vector2 targetRenderSize
                    || targetDisplayProperty?.GetValue(gameView) is not int targetDisplay
                    || targetDisplay < 0
                    || targetInViewProperty?.GetValue(gameView) is not Rect targetInView
                    || deviceFlippedTargetInViewProperty?.GetValue(gameView)
                        is not Rect deviceFlippedTargetInView)
                {
                    errorMessage =
                        "GameView does not currently expose a completed presentation texture and mapping.";
                    return false;
                }

                var targetWidth = Mathf.RoundToInt(targetRenderSize.x);
                var targetHeight = Mathf.RoundToInt(targetRenderSize.y);
                if (targetWidth <= 0
                    || targetHeight <= 0
                    || renderTexture.width != targetWidth
                    || renderTexture.height != targetHeight)
                {
                    errorMessage = "GameView target resolution and presentation texture dimensions do not match.";
                    return false;
                }

                if (!UnityScreenshotGameViewPresentationMapping.TryResolveSourceUvTransform(
                    targetInView,
                    deviceFlippedTargetInView,
                    out var sourceUvTransform,
                    out errorMessage))
                {
                    return false;
                }

                source = new GameViewSource(
                    gameView,
                    renderTexture,
                    targetWidth,
                    targetHeight,
                    backingScale,
                    targetDisplay,
                    targetInView,
                    deviceFlippedTargetInView,
                    sourceUvTransform);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Unity GameView adapter failed. {UnwrapReflectionException(exception).Message}";
                return false;
            }
        }

        /// <summary> Confirms that a previously acquired GameView source still represents the same visible surface. </summary>
        public bool TryValidateGameViewSource (
            GameViewSource source,
            out string errorMessage)
        {
            if (source == null || source.View == null || source.RenderTexture == null)
            {
                errorMessage = "GameView source is no longer alive.";
                return false;
            }

            if (!TryGetGameViewSource(out var current, out errorMessage))
            {
                return false;
            }

            if (!ReferenceEquals(source.View, current.View)
                || !ReferenceEquals(source.RenderTexture, current.RenderTexture)
                || source.Width != current.Width
                || source.Height != current.Height
                || !Mathf.Approximately(source.BackingScale, current.BackingScale)
                || source.TargetDisplay != current.TargetDisplay
                || source.TargetInView != current.TargetInView
                || source.DeviceFlippedTargetInView != current.DeviceFlippedTargetInView
                || source.SourceUvTransform != current.SourceUvTransform)
            {
                errorMessage = "GameView presentation source changed during screenshot capture.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Synchronously completes one GameView repaint through its selected HostView. </summary>
        public bool TryRepaintGameViewImmediately (
            EditorWindow gameView,
            out string errorMessage)
        {
            if (gameView == null)
            {
                errorMessage = "GameView was destroyed before its presentation could be repainted.";
                return false;
            }

            var previousActive = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
                var playModeViewType = unityEditorAssembly.GetType("UnityEditor.PlayModeView");
                var repaintImmediately = FindMethod(
                    gameViewType,
                    "RepaintImmediately",
                    Type.EmptyTypes);
                var renderViewCallNeededProperty = FindProperty(
                    playModeViewType,
                    "renderViewCallNeededInOnGUI",
                    StaticMembers);
                if (gameViewType == null
                    || playModeViewType == null
                    || gameView.GetType() != gameViewType
                    || repaintImmediately == null
                    || renderViewCallNeededProperty?.GetValue(obj: null)
                        is not bool renderViewCallNeeded)
                {
                    errorMessage = "Unity GameView immediate-repaint adapter is unavailable.";
                    return false;
                }

                if (!renderViewCallNeeded)
                {
                    errorMessage =
                        "Unity GameView render path is not ready to produce a fresh presentation frame.";
                    return false;
                }

                if (!TryGetSelectedHostState(
                        gameView,
                        out _,
                        out _,
                        out _,
                        out errorMessage))
                {
                    return false;
                }

                repaintImmediately.Invoke(gameView, parameters: null);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"Unity GameView immediate repaint failed. {UnwrapReflectionException(exception).Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
        }

        /// <summary> Resolves the visible active SceneView and its physical content rectangle. </summary>
        public bool TryGetSceneViewSource (
            out SceneViewSource source,
            out string errorMessage)
        {
            source = null;
            if (!UnityScreenshotRuntimeCapabilityPolicy.TryValidateCurrentEnvironment(out errorMessage))
            {
                return false;
            }

            try
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    errorMessage = "An active SceneView is required for scene screenshot capture.";
                    return false;
                }

                if (!sceneView.hasFocus)
                {
                    errorMessage = "The active SceneView must have focus so Unity can capture its final window framebuffer.";
                    return false;
                }

                if (!TryGetSelectedHostState(
                    sceneView,
                    out var hostView,
                    out var backingScale,
                    out var hdrActive,
                    out errorMessage))
                {
                    return false;
                }

                if (hdrActive)
                {
                    errorMessage = "HDR SceneView presentation is not supported by the SDR screenshot backend.";
                    return false;
                }

                var windowRect = sceneView.position;
                var contentRect = sceneView.cameraViewport;
                var grabPixelsMethod = FindMethod(
                    hostView.GetType(),
                    "GrabPixels",
                    new[] { typeof(RenderTexture), typeof(Rect) });
                if (grabPixelsMethod == null || grabPixelsMethod.ReturnType != typeof(void))
                {
                    errorMessage = "SceneView HostView does not expose the required window framebuffer capture member.";
                    return false;
                }

                if (!TryHasDisplayedSceneOverlay(
                    sceneView,
                    out var hasDisplayedOverlay,
                    out errorMessage))
                {
                    return false;
                }

                if (hasDisplayedOverlay)
                {
                    errorMessage = "Displayed SceneView Overlay panels cannot be excluded from the final window framebuffer without changing Editor state.";
                    return false;
                }

                if (!IsFinitePositive(windowRect.width)
                    || !IsFinitePositive(windowRect.height)
                    || !IsFinitePositive(contentRect.width)
                    || !IsFinitePositive(contentRect.height)
                    || contentRect.x < 0f
                    || contentRect.y < 0f
                    || contentRect.xMax > windowRect.width + 0.01f
                    || contentRect.yMax > windowRect.height + 0.01f)
                {
                    errorMessage = "SceneView content rectangle could not be mapped to its window framebuffer.";
                    return false;
                }

                var framebufferWidth = Mathf.RoundToInt(windowRect.width * backingScale);
                var framebufferHeight = Mathf.RoundToInt(windowRect.height * backingScale);
                var contentX = Mathf.RoundToInt(contentRect.x * backingScale);
                var contentTop = Mathf.RoundToInt(contentRect.y * backingScale);
                var contentRight = Mathf.RoundToInt(contentRect.xMax * backingScale);
                var contentBottomFromTop = Mathf.RoundToInt(contentRect.yMax * backingScale);
                var contentWidth = contentRight - contentX;
                var contentHeight = contentBottomFromTop - contentTop;
                var contentBottom = framebufferHeight - contentBottomFromTop;
                if (framebufferWidth <= 0
                    || framebufferHeight <= 0
                    || contentWidth <= 0
                    || contentHeight <= 0
                    || contentX < 0
                    || contentBottom < 0
                    || contentX + contentWidth > framebufferWidth
                    || contentBottom + contentHeight > framebufferHeight)
                {
                    errorMessage = "SceneView physical content rectangle is outside its framebuffer.";
                    return false;
                }

                var sourceUvTransform = SystemInfo.graphicsUVStartsAtTop
                    ? new Vector4(
                        contentWidth / (float)framebufferWidth,
                        -contentHeight / (float)framebufferHeight,
                        contentX / (float)framebufferWidth,
                        contentBottomFromTop / (float)framebufferHeight)
                    : new Vector4(
                        contentWidth / (float)framebufferWidth,
                        contentHeight / (float)framebufferHeight,
                        contentX / (float)framebufferWidth,
                        contentBottom / (float)framebufferHeight);
                source = new SceneViewSource(
                    sceneView,
                    hostView,
                    grabPixelsMethod,
                    framebufferWidth,
                    framebufferHeight,
                    contentWidth,
                    contentHeight,
                    backingScale,
                    windowRect,
                    contentRect,
                    sourceUvTransform);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Unity SceneView adapter failed. {UnwrapReflectionException(exception).Message}";
                return false;
            }
        }

        /// <summary> Captures one SceneView HostView framebuffer with an explicit physical-pixel rectangle. </summary>
        public bool TryCaptureSceneViewFramebuffer (
            SceneViewSource source,
            RenderTexture destination,
            out string errorMessage)
        {
            if (source == null || source.View == null)
            {
                errorMessage = "SceneView source is no longer alive.";
                return false;
            }

            if (destination == null || !destination.IsCreated())
            {
                errorMessage = "SceneView framebuffer destination is unavailable.";
                return false;
            }

            var previousActive = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                source.GrabPixelsMethod.Invoke(
                    source.HostView,
                    new object[]
                    {
                        destination,
                        new Rect(0f, 0f, source.FramebufferWidth, source.FramebufferHeight),
                    });

                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"SceneView window framebuffer capture failed. {UnwrapReflectionException(exception).Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
        }

        /// <summary> Confirms that a previously acquired SceneView mapping remains stable. </summary>
        public bool TryValidateSceneViewSource (
            SceneViewSource source,
            out string errorMessage)
        {
            if (source == null || source.View == null)
            {
                errorMessage = "SceneView source is no longer alive.";
                return false;
            }

            if (!TryGetSceneViewSource(out var current, out errorMessage))
            {
                return false;
            }

            if (!ReferenceEquals(source.View, current.View)
                || !ReferenceEquals(source.HostView, current.HostView)
                || source.GrabPixelsMethod.MetadataToken != current.GrabPixelsMethod.MetadataToken
                || !ReferenceEquals(source.GrabPixelsMethod.Module, current.GrabPixelsMethod.Module)
                || source.FramebufferWidth != current.FramebufferWidth
                || source.FramebufferHeight != current.FramebufferHeight
                || source.ContentWidth != current.ContentWidth
                || source.ContentHeight != current.ContentHeight
                || !Mathf.Approximately(source.BackingScale, current.BackingScale)
                || source.WindowRect != current.WindowRect
                || source.ContentRect != current.ContentRect
                || source.SourceUvTransform != current.SourceUvTransform)
            {
                errorMessage = "SceneView presentation mapping changed during screenshot capture.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Starts an unsaved, request-owned GameView fixed-resolution transaction. </summary>
        /// <param name="gameView"> The selected main GameView whose resolution may be changed. </param>
        /// <param name="width"> The requested fixed-resolution width in pixels. </param>
        /// <param name="height"> The requested fixed-resolution height in pixels. </param>
        /// <param name="lease">
        /// The transaction owner when request-owned state was created. This value can be non-null when the method
        /// returns <see langword="false" /> if activation failed after creating that state. The caller must complete
        /// restoration for every non-null value, regardless of the return value.
        /// </param>
        /// <param name="errorMessage"> The failure reason when the transaction was not activated. </param>
        /// <returns>
        /// <see langword="true" /> only when the temporary resolution was selected; otherwise,
        /// <see langword="false" />. A false result does not transfer away a non-null <paramref name="lease" />.
        /// </returns>
        public bool TryBeginGameViewResolution (
            EditorWindow gameView,
            int width,
            int height,
            out GameViewResolutionLease lease,
            out string errorMessage)
        {
            lease = null;
            if (gameView == null || width < 10 || height < 10)
            {
                errorMessage = "Requested GameView resolution is outside Unity's fixed-resolution range.";
                return false;
            }

            try
            {
                var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
                var gameViewSizesType = unityEditorAssembly.GetType("UnityEditor.GameViewSizes");
                var gameViewSizeType = unityEditorAssembly.GetType("UnityEditor.GameViewSize");
                var gameViewSizeKindType = unityEditorAssembly.GetType("UnityEditor.GameViewSizeType");
                if (gameViewType == null
                    || gameViewSizesType == null
                    || gameViewSizeType == null
                    || gameViewSizeKindType == null
                    || gameView.GetType() != gameViewType)
                {
                    errorMessage = "Unity GameView resolution adapter types are unavailable.";
                    return false;
                }

                var instanceProperty = FindProperty(gameViewSizesType, "instance", StaticMembers);
                var currentGroupProperty = FindProperty(gameViewSizesType, "currentGroup");
                var currentGroupTypeProperty = FindProperty(gameViewSizesType, "currentGroupType");
                var selectedSizeIndexProperty = FindProperty(gameViewType, "selectedSizeIndex");
                var selectionCallback = FindMethod(
                    gameViewType,
                    "SizeSelectionCallback",
                    new[] { typeof(int), typeof(object) });
                var fixedResolutionKind = Enum.Parse(gameViewSizeKindType, "FixedResolution");
                var sizeConstructor = gameViewSizeType.GetConstructor(
                    InstanceMembers,
                    binder: null,
                    new[] { gameViewSizeKindType, typeof(int), typeof(int), typeof(string) },
                    modifiers: null);
                var sizesInstance = instanceProperty?.GetValue(obj: null);
                var group = sizesInstance == null ? null : currentGroupProperty?.GetValue(sizesInstance);
                var currentGroupType = sizesInstance == null
                    ? null
                    : currentGroupTypeProperty?.GetValue(sizesInstance);
                if (group == null
                    || currentGroupType == null
                    || currentGroupProperty == null
                    || currentGroupTypeProperty == null
                    || selectedSizeIndexProperty == null
                    || selectionCallback == null
                    || sizeConstructor == null)
                {
                    errorMessage = "Unity GameView resolution adapter members are unavailable.";
                    return false;
                }

                var groupType = group.GetType();
                var getTotalCount = FindMethod(groupType, "GetTotalCount", Type.EmptyTypes);
                var getGameViewSize = FindMethod(groupType, "GetGameViewSize", new[] { typeof(int) });
                var addCustomSize = FindMethod(groupType, "AddCustomSize", new[] { gameViewSizeType });
                var removeCustomSize = FindMethod(groupType, "RemoveCustomSize", new[] { typeof(int) });
                if (getTotalCount == null
                    || getGameViewSize == null
                    || addCustomSize == null
                    || removeCustomSize == null
                    || selectedSizeIndexProperty.GetValue(gameView) is not int originalIndex
                    || getTotalCount.Invoke(group, parameters: null) is not int originalCount
                    || originalIndex < 0
                    || originalIndex >= originalCount
                    || getGameViewSize.Invoke(group, new object[] { originalIndex }) is not object originalSize)
                {
                    errorMessage = "Unity GameView resolution group members are unavailable.";
                    return false;
                }

                var temporaryLabel = UnityScreenshotResolutionLeaseRegistry.CreateLabel();
                var ownedResolution = new UnityScreenshotResolutionLeaseRegistry.OwnedResolution(
                    temporaryLabel,
                    width,
                    height,
                    currentGroupType.ToString(),
                    originalIndex);
                UnityScreenshotResolutionLeaseRegistry.Register(ownedResolution);
                object temporarySize = null;
                try
                {
                    temporarySize = sizeConstructor.Invoke(new object[]
                    {
                        fixedResolutionKind,
                        width,
                        height,
                        temporaryLabel,
                    });
                    addCustomSize.Invoke(group, new[] { temporarySize });
                    if (getTotalCount.Invoke(group, parameters: null) is not int nextCount
                        || nextCount != originalCount + 1)
                    {
                        TryRollbackTemporaryResolutionRegistration(
                            group,
                            temporarySize,
                            originalIndex,
                            originalCount,
                            originalSize,
                            getTotalCount,
                            getGameViewSize,
                            removeCustomSize,
                            temporaryLabel);
                        errorMessage = "Unity did not add the temporary GameView resolution exactly once.";
                        return false;
                    }

                    lease = new GameViewResolutionLease(
                        gameView,
                        sizesInstance,
                        currentGroupProperty,
                        currentGroupTypeProperty,
                        group,
                        currentGroupType.ToString(),
                        temporarySize,
                        temporaryLabel,
                        temporaryIndex: originalCount,
                        originalIndex,
                        originalCount,
                        originalSize,
                        selectedSizeIndexProperty,
                        selectionCallback,
                        getTotalCount,
                        getGameViewSize,
                        removeCustomSize);
                }
                catch
                {
                    if (lease == null)
                    {
                        TryRollbackTemporaryResolutionRegistration(
                            group,
                            temporarySize,
                            originalIndex,
                            originalCount,
                            originalSize,
                            getTotalCount,
                            getGameViewSize,
                            removeCustomSize,
                            temporaryLabel);
                    }

                    throw;
                }

                selectionCallback.Invoke(gameView, new object[] { originalCount, null });
                gameView.Repaint();
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"Unity GameView resolution transaction failed. {UnwrapReflectionException(exception).Message}";
                return false;
            }
        }

        private bool TryGetSelectedHostState (
            EditorWindow window,
            out object hostView,
            out float backingScale,
            out bool hdrActive,
            out string errorMessage)
        {
            hostView = null;
            backingScale = 0f;
            hdrActive = false;
            var parentField = FindField(window.GetType(), "m_Parent");
            var parent = parentField?.GetValue(window);
            if (parent == null)
            {
                errorMessage = "Target Editor window is not attached to a HostView.";
                return false;
            }

            var actualViewProperty = FindProperty(parent.GetType(), "actualView");
            var hdrActiveProperty = FindProperty(parent.GetType(), "hdrActive");
            var getBackingScaleFactor = FindMethod(
                parent.GetType(),
                "GetBackingScaleFactor",
                Type.EmptyTypes);
            if (actualViewProperty?.GetValue(parent) is not EditorWindow actualView
                || !ReferenceEquals(actualView, window))
            {
                errorMessage = "Target Editor window is not the selected tab in its HostView.";
                return false;
            }

            if (hdrActiveProperty?.GetValue(parent) is not bool resolvedHdrActive
                || getBackingScaleFactor?.Invoke(parent, parameters: null) is not float resolvedBackingScale
                || !IsFinitePositive(resolvedBackingScale))
            {
                errorMessage = "Target Editor window backing scale or HDR state could not be resolved.";
                return false;
            }

            backingScale = resolvedBackingScale;
            hdrActive = resolvedHdrActive;
            hostView = parent;
            errorMessage = null;
            return true;
        }

        private static bool TryHasDisplayedSceneOverlay (
            SceneView sceneView,
            out bool hasDisplayedOverlay,
            out string errorMessage)
        {
            hasDisplayedOverlay = false;
            var canvas = sceneView.overlayCanvas;
            if (canvas == null)
            {
                errorMessage = "SceneView Overlay canvas is unavailable.";
                return false;
            }

            var overlaysEnabledProperty = FindProperty(canvas.GetType(), "overlaysEnabled");
            var overlaysProperty = FindProperty(canvas.GetType(), "overlays");
            if (overlaysEnabledProperty?.GetValue(canvas) is not bool overlaysEnabled
                || overlaysProperty?.GetValue(canvas) is not IEnumerable overlays)
            {
                errorMessage = "SceneView Overlay visibility could not be resolved.";
                return false;
            }

            var canvasRootVisualElementProperty = FindProperty(
                canvas.GetType(),
                "rootVisualElement");
            var cameraViewVisualElementProperty = FindProperty(
                sceneView.GetType(),
                "cameraViewVisualElement");
            if (canvasRootVisualElementProperty?.GetValue(canvas)
                    is not VisualElement canvasRootVisualElement
                || cameraViewVisualElementProperty?.GetValue(sceneView)
                    is not VisualElement cameraViewVisualElement)
            {
                errorMessage = "SceneView Overlay chrome bounds could not be resolved.";
                return false;
            }

            var contentBounds = cameraViewVisualElement.worldBound;
            if (!IsFinite(contentBounds.x)
                || !IsFinite(contentBounds.y)
                || !IsFinitePositive(contentBounds.width)
                || !IsFinitePositive(contentBounds.height))
            {
                errorMessage = "SceneView content bounds are invalid for Overlay chrome exclusion.";
                return false;
            }

            if (!TryHasDisplayedOverlayChrome(
                    canvasRootVisualElement,
                    contentBounds,
                    out var hasDisplayedChrome,
                    out errorMessage))
            {
                return false;
            }

            if (hasDisplayedChrome)
            {
                hasDisplayedOverlay = true;
                errorMessage = null;
                return true;
            }

            if (!overlaysEnabled)
            {
                errorMessage = null;
                return true;
            }

            foreach (var overlay in overlays)
            {
                if (overlay == null)
                {
                    continue;
                }

                var displayedProperty = FindProperty(overlay.GetType(), "displayed");
                if (displayedProperty?.GetValue(overlay) is not bool displayed)
                {
                    errorMessage = "SceneView Overlay item visibility could not be resolved.";
                    return false;
                }

                if (displayed)
                {
                    if (string.Equals(
                        overlay.GetType().Name,
                        "SceneOrientationGizmo",
                        StringComparison.Ordinal))
                    {
                        // Scene orientation is part of the public SceneView gizmo presentation.
                        continue;
                    }

                    hasDisplayedOverlay = true;
                    errorMessage = null;
                    return true;
                }
            }

            errorMessage = null;
            return true;
        }

        private static bool TryHasDisplayedOverlayChrome (
            VisualElement canvasRoot,
            Rect contentBounds,
            out bool hasDisplayedChrome,
            out string errorMessage)
        {
            hasDisplayedChrome = false;
            var candidates = new List<VisualElement>();
            CollectOverlayChromeCandidates(canvasRoot, candidates);
            foreach (var candidate in candidates)
            {
                if (!IsEffectivelyDisplayed(candidate, canvasRoot))
                {
                    continue;
                }

                var bounds = candidate.worldBound;
                if (!IsFinite(bounds.x)
                    || !IsFinite(bounds.y)
                    || !IsFinitePositive(bounds.width)
                    || !IsFinitePositive(bounds.height))
                {
                    errorMessage = "Displayed SceneView Overlay chrome bounds are unavailable.";
                    return false;
                }

                if (bounds.Overlaps(contentBounds))
                {
                    hasDisplayedChrome = true;
                    errorMessage = null;
                    return true;
                }
            }

            errorMessage = null;
            return true;
        }

        private static void CollectOverlayChromeCandidates (
            VisualElement element,
            ICollection<VisualElement> candidates)
        {
            if (IsOverlayChromeIdentifier(element))
            {
                candidates.Add(element);
            }

            for (var index = 0; index < element.childCount; index++)
            {
                CollectOverlayChromeCandidates(element[index], candidates);
            }
        }

        private static bool IsOverlayChromeIdentifier (VisualElement element)
        {
            return HasVisualElementIdentifier(element, "overlay-menu-btn")
                || HasVisualElementIdentifier(element, "overlay-menu-icon");
        }

        private static bool HasVisualElementIdentifier (
            VisualElement element,
            string identifier)
        {
            return string.Equals(element.name, identifier, StringComparison.Ordinal)
                || element.ClassListContains(identifier);
        }

        private static bool IsEffectivelyDisplayed (
            VisualElement element,
            VisualElement canvasRoot)
        {
            for (var current = element; current != null; current = current.parent)
            {
                var style = current.resolvedStyle;
                if (style.display == DisplayStyle.None
                    || style.visibility == Visibility.Hidden
                    || style.opacity <= 0f)
                {
                    return false;
                }

                if (ReferenceEquals(current, canvasRoot))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFinitePositive (float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative (float value)
        {
            return value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite (float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static FieldInfo FindField (Type type, string name)
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

        private static Exception UnwrapReflectionException (Exception exception)
        {
            return exception is TargetInvocationException { InnerException: not null } invocationException
                ? invocationException.InnerException
                : exception;
        }

        private static bool TryRollbackTemporaryResolutionRegistration (
            object group,
            object temporarySize,
            int originalIndex,
            int originalCount,
            object originalSize,
            MethodInfo getTotalCount,
            MethodInfo getGameViewSize,
            MethodInfo removeCustomSize,
            string temporaryLabel)
        {
            try
            {
                if (getTotalCount.Invoke(group, parameters: null) is not int count)
                {
                    return false;
                }

                if (count == originalCount + 1)
                {
                    if (temporarySize == null
                        || getGameViewSize.Invoke(
                            group,
                            new object[] { originalCount }) is not object candidate
                        || !ReferenceEquals(candidate, temporarySize))
                    {
                        return false;
                    }

                    removeCustomSize.Invoke(group, new object[] { originalCount });
                }
                else if (count != originalCount)
                {
                    return false;
                }

                if (getTotalCount.Invoke(group, parameters: null) is not int restoredCount
                    || restoredCount != originalCount
                    || originalIndex < 0
                    || originalIndex >= restoredCount
                    || getGameViewSize.Invoke(
                        group,
                        new object[] { originalIndex }) is not object restoredOriginalSize
                    || !ReferenceEquals(restoredOriginalSize, originalSize))
                {
                    return false;
                }

                if (temporarySize != null)
                {
                    for (var index = 0; index < restoredCount; index++)
                    {
                        if (ReferenceEquals(
                            getGameViewSize.Invoke(group, new object[] { index }),
                            temporarySize))
                        {
                            return false;
                        }
                    }
                }

                return UnityScreenshotResolutionLeaseRegistry.TryUnregister(
                    temporaryLabel,
                    out _);
            }
            catch
            {
                // The ownership marker is intentionally retained for later fail-closed orphan cleanup.
                return false;
            }
        }

        /// <summary> Describes one GameView source surface and its single authoritative UV transform. </summary>
        internal sealed record GameViewSource (
            EditorWindow View,
            RenderTexture RenderTexture,
            int Width,
            int Height,
            float BackingScale,
            int TargetDisplay,
            Rect TargetInView,
            Rect DeviceFlippedTargetInView,
            Vector4 SourceUvTransform);

        /// <summary> Describes one SceneView window framebuffer and content crop. </summary>
        internal sealed record SceneViewSource (
            SceneView View,
            object HostView,
            MethodInfo GrabPixelsMethod,
            int FramebufferWidth,
            int FramebufferHeight,
            int ContentWidth,
            int ContentHeight,
            float BackingScale,
            Rect WindowRect,
            Rect ContentRect,
            Vector4 SourceUvTransform);

        /// <summary> Owns one unsaved temporary GameView resolution and restores it on every exit path. </summary>
        internal sealed class GameViewResolutionLease : IDisposable
        {
            private readonly EditorWindow gameView;
            private readonly object sizesInstance;
            private readonly PropertyInfo currentGroupProperty;
            private readonly PropertyInfo currentGroupTypeProperty;
            private readonly object group;
            private readonly string groupTypeName;
            private readonly object temporarySize;
            private readonly string temporaryLabel;
            private readonly int temporaryIndex;
            private readonly int originalIndex;
            private readonly int originalCount;
            private readonly object originalSize;
            private readonly PropertyInfo selectedSizeIndexProperty;
            private readonly MethodInfo selectionCallback;
            private readonly MethodInfo getTotalCount;
            private readonly MethodInfo getGameViewSize;
            private readonly MethodInfo removeCustomSize;

            private bool isRestoring;

            private bool cleanupComplete;

            private bool restoreSucceeded;

            private bool selectionRestoredByLease;

            private bool temporaryRemovedByLease;

            private bool selectionConflictObserved;

            private string restoreErrorMessage;

            public GameViewResolutionLease (
                EditorWindow gameView,
                object sizesInstance,
                PropertyInfo currentGroupProperty,
                PropertyInfo currentGroupTypeProperty,
                object group,
                string groupTypeName,
                object temporarySize,
                string temporaryLabel,
                int temporaryIndex,
                int originalIndex,
                int originalCount,
                object originalSize,
                PropertyInfo selectedSizeIndexProperty,
                MethodInfo selectionCallback,
                MethodInfo getTotalCount,
                MethodInfo getGameViewSize,
                MethodInfo removeCustomSize)
            {
                this.gameView = gameView;
                this.sizesInstance = sizesInstance;
                this.currentGroupProperty = currentGroupProperty;
                this.currentGroupTypeProperty = currentGroupTypeProperty;
                this.group = group;
                this.groupTypeName = groupTypeName;
                this.temporarySize = temporarySize;
                this.temporaryLabel = temporaryLabel;
                this.temporaryIndex = temporaryIndex;
                this.originalIndex = originalIndex;
                this.originalCount = originalCount;
                this.originalSize = originalSize;
                this.selectedSizeIndexProperty = selectedSizeIndexProperty;
                this.selectionCallback = selectionCallback;
                this.getTotalCount = getTotalCount;
                this.getGameViewSize = getGameViewSize;
                this.removeCustomSize = removeCustomSize;
                AssemblyReloadEvents.beforeAssemblyReload += RestoreBestEffort;
                EditorApplication.quitting += RestoreBestEffort;
            }

            /// <summary> Gets the GameView whose selected resolution is leased. </summary>
            public EditorWindow GameView => gameView;

            /// <summary> Gets whether a later attempt can still complete restoration. </summary>
            public bool CanRetryRestore => !cleanupComplete && !restoreSucceeded;

            /// <summary> Restores the user's prior selection and removes only this request's temporary entry. </summary>
            public void Dispose ()
            {
                RestoreBestEffort();
            }

            /// <summary> Restores and synchronously verifies the temporary resolution bookkeeping. </summary>
            public bool TryRestore (out string errorMessage)
            {
                if (restoreSucceeded || cleanupComplete)
                {
                    errorMessage = restoreErrorMessage;
                    return restoreSucceeded;
                }

                if (isRestoring)
                {
                    errorMessage = "GameView resolution restoration is already running.";
                    return false;
                }

                isRestoring = true;
                try
                {
                    var currentGroup = currentGroupProperty.GetValue(sizesInstance);
                    var currentGroupType = currentGroupTypeProperty.GetValue(sizesInstance)?.ToString();
                    var isOriginalGroup = ReferenceEquals(currentGroup, group)
                        && string.Equals(currentGroupType, groupTypeName, StringComparison.Ordinal);
                    if (!isOriginalGroup)
                    {
                        if (!TryRemoveOwnedTemporary(out restoreErrorMessage)
                            || !TryValidateOriginalGroup(out restoreErrorMessage)
                            || !TryClearOwnershipMarker(out restoreErrorMessage))
                        {
                            errorMessage = restoreErrorMessage;
                            return false;
                        }

                        cleanupComplete = true;
                        Unsubscribe();
                        restoreErrorMessage =
                            "Current GameView size group changed during capture; its selection was left untouched.";
                        errorMessage = restoreErrorMessage;
                        return false;
                    }

                    if (gameView == null)
                    {
                        if (TryRemoveOwnedTemporary(out _)
                            && TryValidateOriginalGroup(out _)
                            && TryClearOwnershipMarker(out _))
                        {
                            cleanupComplete = true;
                            Unsubscribe();
                        }

                        restoreErrorMessage = "GameView was destroyed before its resolution could be restored.";
                        errorMessage = restoreErrorMessage;
                        return false;
                    }

                    if (!TryInspectTemporaryState(
                        out var temporaryPresent,
                        out restoreErrorMessage))
                    {
                        errorMessage = restoreErrorMessage;
                        return false;
                    }

                    if (temporaryPresent)
                    {
                        if (selectedSizeIndexProperty.GetValue(gameView) is not int selectedIndex)
                        {
                            restoreErrorMessage = "Current GameView resolution selection is unavailable.";
                            errorMessage = restoreErrorMessage;
                            return false;
                        }

                        if (selectionConflictObserved)
                        {
                            if (selectedIndex == temporaryIndex)
                            {
                                restoreErrorMessage =
                                    "GameView resolution selection changed again after a restoration conflict; no selection or collection was modified.";
                                errorMessage = restoreErrorMessage;
                                return false;
                            }

                            return TryCompleteSelectionConflictCleanup(out errorMessage);
                        }

                        if (!selectionRestoredByLease)
                        {
                            if (selectedIndex != temporaryIndex)
                            {
                                return TryCompleteSelectionConflictCleanup(out errorMessage);
                            }

                            selectionCallback.Invoke(gameView, new object[] { originalIndex, null });
                            if (selectedSizeIndexProperty.GetValue(gameView) is not int restoredIndex
                                || restoredIndex != originalIndex)
                            {
                                restoreErrorMessage =
                                    "GameView did not apply the request-owned selection restoration exactly.";
                                errorMessage = restoreErrorMessage;
                                return false;
                            }

                            selectionRestoredByLease = true;
                        }
                        else if (selectedIndex != originalIndex)
                        {
                            restoreErrorMessage =
                                "GameView resolution selection changed after request-owned restoration; no further selection was applied.";
                            errorMessage = restoreErrorMessage;
                            return false;
                        }
                    }
                    else if (selectionConflictObserved && temporaryRemovedByLease)
                    {
                        return TryFinalizeSelectionConflictCleanup(out errorMessage);
                    }
                    else if (!selectionRestoredByLease || !temporaryRemovedByLease)
                    {
                        restoreErrorMessage =
                            "Temporary GameView resolution state changed outside the request-owned restoration path.";
                        errorMessage = restoreErrorMessage;
                        return false;
                    }

                    if (!TryRemoveOwnedTemporary(out restoreErrorMessage)
                        || !TryValidateRestoredStateCore(out restoreErrorMessage)
                        || !TryClearOwnershipMarker(out restoreErrorMessage))
                    {
                        errorMessage = restoreErrorMessage;
                        return false;
                    }

                    gameView.Repaint();
                    restoreSucceeded = true;
                    cleanupComplete = true;
                    restoreErrorMessage = null;
                    Unsubscribe();
                    errorMessage = null;
                    return true;
                }
                catch (Exception exception)
                {
                    restoreErrorMessage =
                        $"GameView resolution restoration failed. {UnwrapReflectionException(exception).Message}";
                    errorMessage = restoreErrorMessage;
                    return false;
                }
                finally
                {
                    isRestoring = false;
                }
            }

            /// <summary> Verifies that the original selection and size collection were restored. </summary>
            public bool TryValidateRestoredState (out string errorMessage)
            {
                if (!restoreSucceeded)
                {
                    errorMessage = restoreErrorMessage ?? "GameView resolution restoration has not run.";
                    return false;
                }

                try
                {
                    return TryValidateRestoredStateCore(out errorMessage);
                }
                catch (Exception exception)
                {
                    errorMessage =
                        $"GameView resolution restoration could not be verified. {UnwrapReflectionException(exception).Message}";
                    return false;
                }
            }

            private bool TryRemoveOwnedTemporary (out string errorMessage)
            {
                if (getTotalCount.Invoke(group, parameters: null) is not int count)
                {
                    errorMessage = "GameView resolution collection count is unavailable.";
                    return false;
                }

                if (count == originalCount + 1)
                {
                    if (temporaryIndex < 0
                        || temporaryIndex >= count
                        || getGameViewSize.Invoke(
                            group,
                            new object[] { temporaryIndex }) is not object candidate
                        || !ReferenceEquals(candidate, temporarySize))
                    {
                        errorMessage =
                            "Temporary GameView resolution identity changed before restoration; no unrelated entry was removed.";
                        return false;
                    }

                    removeCustomSize.Invoke(group, new object[] { temporaryIndex });
                    temporaryRemovedByLease = true;
                }
                else if (count != originalCount)
                {
                    errorMessage =
                        "GameView resolution collection changed unexpectedly; no unrelated entry was removed.";
                    return false;
                }
                else if (!temporaryRemovedByLease)
                {
                    errorMessage =
                        "Temporary GameView resolution disappeared outside the request-owned restoration path.";
                    return false;
                }

                return TryValidateOriginalGroup(out errorMessage);
            }

            private bool TryInspectTemporaryState (
                out bool temporaryPresent,
                out string errorMessage)
            {
                temporaryPresent = false;
                if (getTotalCount.Invoke(group, parameters: null) is not int count
                    || originalIndex < 0
                    || originalIndex >= count
                    || getGameViewSize.Invoke(
                        group,
                        new object[] { originalIndex }) is not object inspectedOriginalSize
                    || !ReferenceEquals(inspectedOriginalSize, originalSize))
                {
                    errorMessage =
                        "GameView resolution collection changed before request-owned restoration; no selection was modified.";
                    return false;
                }

                if (count == originalCount + 1
                    && temporaryIndex >= 0
                    && temporaryIndex < count
                    && getGameViewSize.Invoke(
                        group,
                        new object[] { temporaryIndex }) is object candidate
                    && ReferenceEquals(candidate, temporarySize))
                {
                    temporaryPresent = true;
                    errorMessage = null;
                    return true;
                }

                if (count == originalCount)
                {
                    for (var index = 0; index < count; index++)
                    {
                        if (ReferenceEquals(
                            getGameViewSize.Invoke(group, new object[] { index }),
                            temporarySize))
                        {
                            errorMessage =
                                "Temporary GameView resolution moved before request-owned restoration; no selection was modified.";
                            return false;
                        }
                    }

                    errorMessage = null;
                    return true;
                }

                errorMessage =
                    "GameView resolution collection changed before request-owned restoration; no selection was modified.";
                return false;
            }

            private bool TryCompleteSelectionConflictCleanup (out string errorMessage)
            {
                selectionConflictObserved = true;
                if (!TryRemoveOwnedTemporary(out restoreErrorMessage)
                    || !TryValidateOriginalGroup(out restoreErrorMessage))
                {
                    errorMessage = restoreErrorMessage;
                    return false;
                }

                gameView.Repaint();
                return TryFinalizeSelectionConflictCleanup(out errorMessage);
            }

            private bool TryFinalizeSelectionConflictCleanup (out string errorMessage)
            {
                if (!TryClearOwnershipMarker(out restoreErrorMessage))
                {
                    errorMessage = restoreErrorMessage;
                    return false;
                }

                cleanupComplete = true;
                Unsubscribe();
                restoreErrorMessage =
                    "GameView resolution selection changed during capture; the user selection was left untouched.";
                errorMessage = restoreErrorMessage;
                return false;
            }

            private bool TryValidateOriginalGroup (out string errorMessage)
            {
                if (getTotalCount.Invoke(group, parameters: null) is not int count
                    || count != originalCount
                    || originalIndex < 0
                    || originalIndex >= count
                    || getGameViewSize.Invoke(
                        group,
                        new object[] { originalIndex }) is not object selectedSize
                    || !ReferenceEquals(selectedSize, originalSize))
                {
                    errorMessage = "GameView resolution size collection was not restored exactly.";
                    return false;
                }

                for (var index = 0; index < count; index++)
                {
                    if (ReferenceEquals(
                        getGameViewSize.Invoke(group, new object[] { index }),
                        temporarySize))
                    {
                        errorMessage = "Temporary GameView resolution remains after restoration.";
                        return false;
                    }
                }

                errorMessage = null;
                return true;
            }

            private bool TryValidateRestoredStateCore (out string errorMessage)
            {
                var currentGroup = currentGroupProperty.GetValue(sizesInstance);
                var currentGroupType = currentGroupTypeProperty.GetValue(sizesInstance)?.ToString();
                if (!ReferenceEquals(currentGroup, group)
                    || !string.Equals(currentGroupType, groupTypeName, StringComparison.Ordinal)
                    || gameView == null
                    || selectedSizeIndexProperty.GetValue(gameView) is not int selectedIndex
                    || selectedIndex != originalIndex
                    || !TryValidateOriginalGroup(out _))
                {
                    errorMessage = "GameView resolution selection or size collection was not restored exactly.";
                    return false;
                }

                errorMessage = null;
                return true;
            }

            private bool TryClearOwnershipMarker (out string errorMessage)
            {
                if (!UnityScreenshotResolutionLeaseRegistry.TryUnregister(
                    temporaryLabel,
                    out errorMessage))
                {
                    return false;
                }

                errorMessage = null;
                return true;
            }

            private void Unsubscribe ()
            {
                AssemblyReloadEvents.beforeAssemblyReload -= RestoreBestEffort;
                EditorApplication.quitting -= RestoreBestEffort;
            }

            private void RestoreBestEffort ()
            {
                string errorMessage = null;
                for (var attempt = 0; attempt < 3 && CanRetryRestore; attempt++)
                {
                    if (TryRestore(out errorMessage))
                    {
                        return;
                    }
                }

                if (!restoreSucceeded)
                {
                    Debug.LogError($"uCLI could not fully restore its temporary GameView resolution. {errorMessage}");
                }
            }
        }
    }
}
