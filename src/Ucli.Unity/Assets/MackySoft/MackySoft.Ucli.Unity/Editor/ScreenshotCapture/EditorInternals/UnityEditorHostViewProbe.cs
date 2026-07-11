using UnityEditor;
using Object = UnityEngine.Object;

namespace MackySoft.Ucli.Unity.ScreenshotCapture.EditorInternals
{
    /// <summary> Resolves the selected HostView and presentation properties shared by Editor windows. </summary>
    internal static class UnityEditorHostViewProbe
    {
        public static bool TryGetSelectedState (
            EditorWindow window,
            out Object hostView,
            out float backingScale,
            out bool hdrActive,
            out string errorMessage)
        {
            hostView = null;
            backingScale = 0f;
            hdrActive = false;
            if (window == null)
            {
                errorMessage = "Target Editor window is unavailable.";
                return false;
            }

            var parentField = UnityEditorReflection.FindField(window.GetType(), "m_Parent");
            if (parentField?.GetValue(window) is not Object parent || parent == null)
            {
                errorMessage = "Target Editor window is not attached to a HostView.";
                return false;
            }

            var actualViewProperty = UnityEditorReflection.FindProperty(parent.GetType(), "actualView");
            var hdrActiveProperty = UnityEditorReflection.FindProperty(parent.GetType(), "hdrActive");
            var getBackingScaleFactor = UnityEditorReflection.FindMethod(
                parent.GetType(),
                "GetBackingScaleFactor",
                System.Type.EmptyTypes);
            if (actualViewProperty?.GetValue(parent) is not EditorWindow actualView
                || actualView == null
                || actualView != window)
            {
                errorMessage = "Target Editor window is not the selected tab in its HostView.";
                return false;
            }

            if (hdrActiveProperty?.GetValue(parent) is not bool resolvedHdrActive
                || getBackingScaleFactor?.Invoke(parent, parameters: null) is not float resolvedBackingScale
                || !IsFinitePositive(resolvedBackingScale))
            {
                errorMessage = "Target Editor window backing scale or HDR state could not be resolved.";
                return false;
            }

            backingScale = resolvedBackingScale;
            hdrActive = resolvedHdrActive;
            hostView = parent;
            errorMessage = null;
            return true;
        }

        private static bool IsFinitePositive (float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
