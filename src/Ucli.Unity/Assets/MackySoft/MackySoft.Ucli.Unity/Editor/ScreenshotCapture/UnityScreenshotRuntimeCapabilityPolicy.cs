using UnityEngine;
using UnityEngine.Rendering;

namespace MackySoft.Ucli.Unity.ScreenshotCapture
{
    /// <summary> Rejects runtime graphics environments outside the current faithful capture path. </summary>
    internal static class UnityScreenshotRuntimeCapabilityPolicy
    {
        /// <summary> Probes the current platform, graphics API, and project color space. </summary>
        public static bool TryValidateCurrentEnvironment (out string errorMessage)
        {
            return TryValidateEnvironment(
                Application.platform,
                SystemInfo.graphicsDeviceType,
                QualitySettings.activeColorSpace,
                out errorMessage);
        }

        /// <summary> Validates explicit runtime graphics properties. </summary>
        internal static bool TryValidateEnvironment (
            RuntimePlatform platform,
            GraphicsDeviceType graphicsDeviceType,
            ColorSpace colorSpace,
            out string errorMessage)
        {
            if (platform != RuntimePlatform.OSXEditor)
            {
                errorMessage = $"Screenshot window capture is unavailable on this Editor platform: {platform}.";
                return false;
            }

            if (graphicsDeviceType != GraphicsDeviceType.Metal)
            {
                errorMessage = $"Screenshot normalization is unavailable for this graphics API: {graphicsDeviceType}.";
                return false;
            }

            if (colorSpace != ColorSpace.Linear)
            {
                errorMessage = $"Screenshot color normalization is unavailable for this project color space: {colorSpace}.";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}
