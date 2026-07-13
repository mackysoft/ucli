namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Describes one SceneView window framebuffer and its presentation-content crop. </summary>
    internal sealed record SceneViewPresentationSource (
        UnityEditor.SceneView View,
        UnityEngine.Object HostView,
        UnitySceneViewCaptureCapability Capability);
}
