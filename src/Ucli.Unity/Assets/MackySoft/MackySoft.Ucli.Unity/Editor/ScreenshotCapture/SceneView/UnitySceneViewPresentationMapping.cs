using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Maps one SceneView window presentation into physical framebuffer coordinates. </summary>
    internal sealed record UnitySceneViewPresentationMapping (
        int FramebufferWidth,
        int FramebufferHeight,
        int ContentWidth,
        int ContentHeight,
        float BackingScale,
        Rect WindowRect,
        Rect ContentRect,
        Vector4 SourceUvTransform)
    {
        /// <summary> Maps the bottom-origin SceneView camera viewport into the top-origin GUIView bitmap. </summary>
        public static bool TryResolve (
            Rect windowRect,
            Rect contentRect,
            float backingScale,
            out UnitySceneViewPresentationMapping mapping,
            out string errorMessage)
        {
            mapping = null;
            if (!UnityScreenshotMath.IsFinitePositive(backingScale)
                || !UnityScreenshotMath.IsFinitePositive(windowRect.width)
                || !UnityScreenshotMath.IsFinitePositive(windowRect.height)
                || !UnityScreenshotMath.IsFinitePositive(contentRect.width)
                || !UnityScreenshotMath.IsFinitePositive(contentRect.height)
                || !UnityScreenshotMath.IsFinite(contentRect.x)
                || !UnityScreenshotMath.IsFinite(contentRect.y)
                || contentRect.x < 0f
                || contentRect.y < 0f
                || contentRect.xMax > windowRect.width + 0.01f
                || contentRect.yMax > windowRect.height + 0.01f)
            {
                errorMessage =
                    "SceneView content rectangle could not be mapped to its physical window framebuffer.";
                return false;
            }

            var framebufferWidth = Mathf.RoundToInt(windowRect.width * backingScale);
            var framebufferHeight = Mathf.RoundToInt(windowRect.height * backingScale);
            var contentX = Mathf.RoundToInt(contentRect.x * backingScale);
            var contentBottom = Mathf.RoundToInt(contentRect.y * backingScale);
            var contentRight = Mathf.RoundToInt(contentRect.xMax * backingScale);
            var contentTopFromBottom = Mathf.RoundToInt(contentRect.yMax * backingScale);
            var contentWidth = contentRight - contentX;
            var contentHeight = contentTopFromBottom - contentBottom;
            var contentTop = framebufferHeight - contentTopFromBottom;
            var contentBottomFromTop = framebufferHeight - contentBottom;
            if (framebufferWidth <= 0
                || framebufferHeight <= 0
                || contentWidth <= 0
                || contentHeight <= 0
                || contentX < 0
                || contentTop < 0
                || contentX + contentWidth > framebufferWidth
                || contentBottomFromTop > framebufferHeight)
            {
                errorMessage = "SceneView physical content rectangle is outside its framebuffer.";
                return false;
            }

            mapping = new UnitySceneViewPresentationMapping(
                framebufferWidth,
                framebufferHeight,
                contentWidth,
                contentHeight,
                backingScale,
                windowRect,
                contentRect,
                new Vector4(
                    contentWidth / (float)framebufferWidth,
                    -contentHeight / (float)framebufferHeight,
                    contentX / (float)framebufferWidth,
                    contentBottomFromTop / (float)framebufferHeight));
            errorMessage = null;
            return true;
        }

    }
}
