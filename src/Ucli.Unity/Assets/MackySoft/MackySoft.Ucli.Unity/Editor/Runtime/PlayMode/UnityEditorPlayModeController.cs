using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Requests Unity Editor Play Mode transitions through UnityEditor APIs. </summary>
    internal sealed class UnityEditorPlayModeController : IUnityPlayModeController
    {
        /// <inheritdoc />
        public void EnterPlayMode ()
        {
            EditorApplication.EnterPlaymode();
        }

        /// <inheritdoc />
        public void ExitPlayMode ()
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
