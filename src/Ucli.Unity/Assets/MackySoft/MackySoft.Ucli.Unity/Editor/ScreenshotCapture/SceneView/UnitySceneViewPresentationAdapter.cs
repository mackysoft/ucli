using System;
using MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Resolves and captures the selected SceneView's final window presentation. </summary>
    internal sealed class UnitySceneViewPresentationAdapter
    {
        public bool TryGetSource (
            out SceneViewPresentationSource source,
            out string errorMessage)
        {
            source = null;
            try
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    errorMessage = "An active SceneView is required for scene screenshot capture.";
                    return false;
                }

                if (!UnityEditorHostViewProbe.TryGetSelectedState(
                    sceneView,
                    out var hostView,
                    out var backingScale,
                    out errorMessage))
                {
                    return false;
                }

                var windowRect = sceneView.position;
                var contentRect = sceneView.cameraViewport;
                if (!UnitySceneViewCaptureCapabilityResolver.TryResolveCurrent(
                    hostView,
                    backingScale,
                    windowRect,
                    contentRect,
                    out var capability,
                    out errorMessage))
                {
                    return false;
                }

                source = new SceneViewPresentationSource(
                    sceneView,
                    hostView,
                    capability);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"Unity SceneView presentation adapter failed. {UnityEditorReflection.UnwrapInvocationException(exception).Message}";
                return false;
            }
        }

        public bool TryCaptureFramebuffer (
            SceneViewPresentationSource source,
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
                var mapping = source.Capability.Mapping;
                source.Capability.GrabPixelsMethod.Invoke(
                    source.HostView,
                    new object[]
                    {
                        destination,
                        new Rect(0f, 0f, mapping.FramebufferWidth, mapping.FramebufferHeight),
                    });

                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage =
                    $"SceneView window framebuffer capture failed. {UnityEditorReflection.UnwrapInvocationException(exception).Message}";
                return false;
            }
            finally
            {
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
        }

        public bool TryValidateSource (
            SceneViewPresentationSource source,
            out string errorMessage)
        {
            if (source == null || source.View == null)
            {
                errorMessage = "SceneView source is no longer alive.";
                return false;
            }

            if (!TryGetSource(out var current, out errorMessage))
            {
                return false;
            }

            if (source.View != current.View
                || source.HostView != current.HostView
                || source.Capability.GrabPixelsMethod.MetadataToken
                    != current.Capability.GrabPixelsMethod.MetadataToken
                || !ReferenceEquals(
                    source.Capability.GrabPixelsMethod.Module,
                    current.Capability.GrabPixelsMethod.Module)
                || source.Capability.FramebufferGraphicsFormat
                    != current.Capability.FramebufferGraphicsFormat
                || source.Capability.Mapping != current.Capability.Mapping)
            {
                errorMessage = "SceneView presentation mapping changed during screenshot capture.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
