using System;
using System.Reflection;
using MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Resolves and validates the selected GameView presentation through capability-probed Editor members. </summary>
    internal sealed class UnityGameViewPresentationAdapter : IGameViewPresentationAdapter
    {
        private readonly Assembly unityEditorAssembly = typeof(EditorWindow).Assembly;

        public bool TryGetSource (
            out GameViewPresentationSource source,
            out string errorMessage)
        {
            source = null;
            try
            {
                var playModeViewType = unityEditorAssembly.GetType("UnityEditor.PlayModeView");
                var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
                var getMainPlayModeView = playModeViewType?.GetMethod(
                    "GetMainPlayModeView",
                    UnityEditorReflection.StaticMembers,
                    binder: null,
                    Type.EmptyTypes,
                    modifiers: null);
                if (playModeViewType == null || gameViewType == null || getMainPlayModeView == null)
                {
                    errorMessage = "Unity GameView presentation members are unavailable.";
                    return false;
                }

                var viewObject = getMainPlayModeView.Invoke(obj: null, parameters: null);
                if (viewObject is not EditorWindow gameView
                    || gameView == null
                    || viewObject.GetType() != gameViewType)
                {
                    errorMessage =
                        "An existing main GameView is required; another PlayModeView cannot be substituted.";
                    return false;
                }

                if (!UnityEditorHostViewProbe.TryGetSelectedState(
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

                var gizmosField = UnityEditorReflection.FindField(gameViewType, "m_Gizmos");
                if (gizmosField?.GetValue(gameView) is not bool gizmosEnabled)
                {
                    errorMessage = "GameView Gizmos state could not be resolved.";
                    return false;
                }

                if (gizmosEnabled)
                {
                    errorMessage =
                        "GameView Editor Gizmos are enabled and cannot be removed without changing the observed presentation.";
                    return false;
                }

                var renderTextureField = UnityEditorReflection.FindField(gameViewType, "m_RenderTexture");
                var targetRenderSizeProperty = UnityEditorReflection.FindProperty(gameViewType, "targetRenderSize");
                var targetDisplayProperty = UnityEditorReflection.FindProperty(gameViewType, "targetDisplay");
                var targetInViewProperty = UnityEditorReflection.FindProperty(gameViewType, "targetInView");
                var deviceFlippedTargetInViewProperty = UnityEditorReflection.FindProperty(
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
                    errorMessage =
                        "GameView target resolution and presentation texture dimensions do not match.";
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

                source = new GameViewPresentationSource(
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
                errorMessage =
                    $"Unity GameView presentation adapter failed. {UnityEditorReflection.UnwrapInvocationException(exception).Message}";
                return false;
            }
        }

        public bool TryValidateSource (
            GameViewPresentationSource source,
            out string errorMessage)
        {
            if (source == null || source.View == null || source.RenderTexture == null)
            {
                errorMessage = "GameView source is no longer alive.";
                return false;
            }

            if (!TryGetSource(out var current, out errorMessage))
            {
                return false;
            }

            if (source.View != current.View
                || source.RenderTexture != current.RenderTexture
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

        public bool IsCurrentTarget (EditorWindow gameView)
        {
            if (gameView == null)
            {
                return false;
            }

            try
            {
                var playModeViewType = unityEditorAssembly.GetType("UnityEditor.PlayModeView");
                var gameViewType = unityEditorAssembly.GetType("UnityEditor.GameView");
                var getMainPlayModeView = playModeViewType?.GetMethod(
                    "GetMainPlayModeView",
                    UnityEditorReflection.StaticMembers,
                    binder: null,
                    Type.EmptyTypes,
                    modifiers: null);
                return gameViewType != null
                    && getMainPlayModeView?.Invoke(obj: null, parameters: null) is EditorWindow current
                    && current == gameView
                    && current.GetType() == gameViewType;
            }
            catch
            {
                return false;
            }
        }

        public bool TryRepaintImmediately (
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
                var repaintImmediately = UnityEditorReflection.FindMethod(
                    gameViewType,
                    "RepaintImmediately",
                    Type.EmptyTypes);
                var renderViewCallNeededProperty = UnityEditorReflection.FindProperty(
                    playModeViewType,
                    "renderViewCallNeededInOnGUI",
                    UnityEditorReflection.StaticMembers);
                if (gameViewType == null
                    || playModeViewType == null
                    || gameView.GetType() != gameViewType
                    || repaintImmediately == null
                    || renderViewCallNeededProperty?.GetValue(obj: null)
                        is not bool renderViewCallNeeded)
                {
                    errorMessage = "Unity GameView immediate-repaint members are unavailable.";
                    return false;
                }

                if (!renderViewCallNeeded)
                {
                    errorMessage =
                        "Unity GameView render path is not ready to produce a fresh presentation frame.";
                    return false;
                }

                if (!UnityEditorHostViewProbe.TryGetSelectedState(
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
                    $"Unity GameView immediate repaint failed. {UnityEditorReflection.UnwrapInvocationException(exception).Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
        }
    }
}
