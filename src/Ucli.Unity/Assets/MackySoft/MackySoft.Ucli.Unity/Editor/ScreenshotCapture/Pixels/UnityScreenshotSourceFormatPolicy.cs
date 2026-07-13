using System;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.Pixels
{
    /// <summary> Validates target presentation formats supported by native capture and normalization. </summary>
    internal static class UnityScreenshotSourceFormatPolicy
    {
        /// <summary> Validates one GameView render texture against the supported source contract. </summary>
        public static bool TryValidateGameViewSource (
            RenderTexture source,
            IpcScreenshotColorSpace colorSpace,
            out string errorMessage)
        {
            if (source == null)
            {
                errorMessage = "GameView presentation texture is unavailable.";
                return false;
            }

            return TryValidateGameViewSource(
                source.graphicsFormat,
                source.dimension,
                source.antiAliasing,
                source.useMipMap,
                colorSpace,
                out errorMessage);
        }

        /// <summary> Resolves and validates the native SceneView window-capture format before invoking Unity. </summary>
        public static bool TryResolveSceneFramebufferFormat (
            out GraphicsFormat graphicsFormat,
            out string errorMessage)
        {
            graphicsFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
            if (!TryValidateSceneFramebufferFormat(graphicsFormat, out errorMessage))
            {
                return false;
            }

            if (!SystemInfo.IsFormatSupported(graphicsFormat, GraphicsFormatUsage.Render)
                || !SystemInfo.IsFormatSupported(graphicsFormat, GraphicsFormatUsage.Sample))
            {
                errorMessage =
                    $"SceneView native framebuffer format does not support render and sample usage: {graphicsFormat}.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Validates explicit GameView source-format properties. </summary>
        internal static bool TryValidateGameViewSource (
            GraphicsFormat graphicsFormat,
            TextureDimension dimension,
            int antiAliasing,
            bool useMipMap,
            IpcScreenshotColorSpace colorSpace,
            out string errorMessage)
        {
            if (dimension != TextureDimension.Tex2D
                || antiAliasing != 1
                || useMipMap)
            {
                errorMessage = "GameView source must be a non-MSAA, non-mipmapped 2D presentation texture.";
                return false;
            }

            if (colorSpace != IpcScreenshotColorSpace.Linear)
            {
                errorMessage = $"GameView source color space is unsupported: {colorSpace}.";
                return false;
            }

            if (graphicsFormat != GraphicsFormat.R8G8B8A8_SRGB
                && graphicsFormat != GraphicsFormat.B8G8R8A8_SRGB)
            {
                errorMessage =
                    $"GameView source is not a compatible 8-bit SDR sRGB presentation format: {graphicsFormat}.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary> Validates an explicit SceneView native window-capture format. </summary>
        internal static bool TryValidateSceneFramebufferFormat (
            GraphicsFormat graphicsFormat,
            out string errorMessage)
        {
            // CaptureSceneView/GrabPixels creates an internal temporary texture from the destination
            // descriptor. On macOS Metal, passing the canonical RGBA staging descriptor can crash the
            // native window-copy path. Only the native BGRA LDR framebuffer contract reaches that API.
            if (graphicsFormat != GraphicsFormat.B8G8R8A8_SRGB)
            {
                errorMessage =
                    $"SceneView native framebuffer format is incompatible with window capture: {graphicsFormat}.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
