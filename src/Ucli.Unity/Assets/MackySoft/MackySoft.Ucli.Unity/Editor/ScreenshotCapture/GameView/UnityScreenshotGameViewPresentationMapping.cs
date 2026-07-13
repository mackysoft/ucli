using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Resolves raw texture orientation from the GameView's actual presentation rectangles. </summary>
    internal static class UnityScreenshotGameViewPresentationMapping
    {
        private const float RectTolerance = 0.01f;

        /// <summary> Resolves the single UV transform that reproduces GameView's device presentation. </summary>
        public static bool TryResolveSourceUvTransform (
            Rect targetInView,
            Rect deviceFlippedTargetInView,
            out Vector4 sourceUvTransform,
            out string errorMessage)
        {
            sourceUvTransform = default;
            if (!UnityScreenshotMath.IsFinite(targetInView.x)
                || !UnityScreenshotMath.IsFinite(targetInView.y)
                || !UnityScreenshotMath.IsFinitePositive(targetInView.width)
                || !UnityScreenshotMath.IsFinitePositive(targetInView.height)
                || !UnityScreenshotMath.IsFinite(deviceFlippedTargetInView.x)
                || !UnityScreenshotMath.IsFinite(deviceFlippedTargetInView.y)
                || !UnityScreenshotMath.IsFinitePositive(deviceFlippedTargetInView.width)
                || !UnityScreenshotMath.IsFinite(deviceFlippedTargetInView.height)
                || Mathf.Abs(deviceFlippedTargetInView.height) <= RectTolerance
                || !Approximately(targetInView.x, deviceFlippedTargetInView.x)
                || !Approximately(targetInView.width, deviceFlippedTargetInView.width))
            {
                errorMessage = "GameView presentation rectangles are invalid or inconsistent.";
                return false;
            }

            if (Approximately(targetInView.y, deviceFlippedTargetInView.y)
                && Approximately(targetInView.height, deviceFlippedTargetInView.height))
            {
                sourceUvTransform = new Vector4(1f, 1f, 0f, 0f);
                errorMessage = null;
                return true;
            }

            if (Approximately(
                    targetInView.y + targetInView.height,
                    deviceFlippedTargetInView.y)
                && Approximately(
                    -targetInView.height,
                    deviceFlippedTargetInView.height))
            {
                sourceUvTransform = new Vector4(1f, -1f, 0f, 1f);
                errorMessage = null;
                return true;
            }

            errorMessage =
                "GameView device presentation does not expose a recognized vertical orientation.";
            return false;
        }

        private static bool Approximately (float left, float right)
        {
            return Mathf.Abs(left - right) <= RectTolerance;
        }

    }
}
