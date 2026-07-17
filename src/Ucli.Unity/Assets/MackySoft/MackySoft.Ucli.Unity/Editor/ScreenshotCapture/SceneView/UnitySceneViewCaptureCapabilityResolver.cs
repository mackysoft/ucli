using System;
using System.Reflection;
using MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals;
using MackySoft.Ucli.Unity.ScreenshotCapture.Pixels;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Resolves the complete independently validated SceneView framebuffer capability tuple. </summary>
    internal static class UnitySceneViewCaptureCapabilityResolver
    {
        /// <summary> Resolves the current SceneView source only when every validated capability input is present. </summary>
        public static bool TryResolveCurrent (
            UnityEngine.Object hostView,
            float backingScale,
            Rect windowRect,
            Rect contentRect,
            out UnitySceneViewCaptureCapability capability,
            out string errorMessage)
        {
            capability = null;
            if (hostView == null)
            {
                errorMessage = "SceneView HostView is unavailable for capture capability resolution.";
                return false;
            }

            var grabPixelsMethod = UnityEditorReflection.FindMethod(
                hostView.GetType(),
                "GrabPixels",
                new[] { typeof(RenderTexture), typeof(Rect) });
            if (!UnityScreenshotSourceFormatPolicy.TryResolveSceneFramebufferFormat(
                out var framebufferGraphicsFormat,
                out errorMessage))
            {
                return false;
            }

            if (!UnitySceneViewPresentationMapping.TryResolve(
                windowRect,
                contentRect,
                backingScale,
                out var mapping,
                out errorMessage))
            {
                return false;
            }

            return TryCreateCapability(
                framebufferGraphicsFormat,
                grabPixelsMethod,
                mapping,
                out capability,
                out errorMessage);
        }

        /// <summary> Creates a capability from explicit observations for deterministic contract tests. </summary>
        internal static bool TryCreateCapability (
            GraphicsFormat framebufferGraphicsFormat,
            MethodInfo grabPixelsMethod,
            UnitySceneViewPresentationMapping mapping,
            out UnitySceneViewCaptureCapability capability,
            out string errorMessage)
        {
            capability = null;
            if (!UnityScreenshotSourceFormatPolicy.TryValidateSceneFramebufferFormat(
                framebufferGraphicsFormat,
                out errorMessage))
            {
                return false;
            }

            if (!HasExactGrabPixelsShape(grabPixelsMethod))
            {
                errorMessage =
                    "SceneView HostView does not expose the exact instance GrabPixels(RenderTexture, Rect) capture member.";
                return false;
            }

            if (mapping == null)
            {
                errorMessage = "SceneView physical presentation mapping is unavailable.";
                return false;
            }

            capability = new UnitySceneViewCaptureCapability(
                grabPixelsMethod,
                framebufferGraphicsFormat,
                mapping);
            errorMessage = null;
            return true;
        }

        private static bool HasExactGrabPixelsShape (MethodInfo method)
        {
            if (method == null
                || method.IsStatic
                || method.IsGenericMethod
                || method.ContainsGenericParameters
                || !string.Equals(method.Name, "GrabPixels", StringComparison.Ordinal)
                || method.ReturnType != typeof(void))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 2
                && !parameters[0].ParameterType.IsByRef
                && parameters[0].ParameterType == typeof(RenderTexture)
                && !parameters[1].ParameterType.IsByRef
                && parameters[1].ParameterType == typeof(Rect);
        }
    }
}
