using System;
using UnityEditor;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Requests Unity Editor Play Mode transitions through UnityEditor APIs. </summary>
    internal sealed class UnityEditorPlayModeController : IUnityPlayModeController
    {
        /// <inheritdoc />
        public void EnterPlayMode ()
        {
            try
            {
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception)
            {
                throw new UnityPlayModeTransitionException(
                    "Unity rejected the Play Mode enter request.",
                    exception);
            }
        }

        /// <inheritdoc />
        public void ExitPlayMode ()
        {
            try
            {
                EditorApplication.ExitPlaymode();
            }
            catch (Exception exception)
            {
                throw new UnityPlayModeTransitionException(
                    "Unity rejected the Play Mode exit request.",
                    exception);
            }
        }
    }
}
