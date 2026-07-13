using System.Reflection;
using UnityEngine.Experimental.Rendering;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.SceneView
{
    /// <summary> Identifies one complete SceneView source capability and its physical presentation mapping. </summary>
    internal sealed record UnitySceneViewCaptureCapability (
        MethodInfo GrabPixelsMethod,
        GraphicsFormat FramebufferGraphicsFormat,
        UnitySceneViewPresentationMapping Mapping);
}
