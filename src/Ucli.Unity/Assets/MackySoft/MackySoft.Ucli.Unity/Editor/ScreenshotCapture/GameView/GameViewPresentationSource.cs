using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.GameView
{
    /// <summary> Describes one GameView presentation texture and its authoritative device mapping. </summary>
    internal sealed record GameViewPresentationSource (
        EditorWindow View,
        RenderTexture RenderTexture,
        int Width,
        int Height,
        float BackingScale,
        int TargetDisplay,
        Rect TargetInView,
        Rect DeviceFlippedTargetInView,
        Vector4 SourceUvTransform);
}
