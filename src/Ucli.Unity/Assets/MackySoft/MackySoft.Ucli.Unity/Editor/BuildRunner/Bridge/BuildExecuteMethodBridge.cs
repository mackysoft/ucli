using MackySoft.Ucli.Unity.Ipc;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Editor
{
    /// <summary> Provides the Unity <c>-executeMethod</c> entrypoint for uCLI executeMethod build runners. </summary>
    public static class BuildExecuteMethodBridge
    {
        /// <summary> Runs the uCLI executeMethod bridge entrypoint. </summary>
        public static void Run ()
        {
            if (UnityBootstrapInitializer.TryStartBatchmodeFromExecuteMethod(out var errorMessage))
            {
                return;
            }

            Debug.LogError(errorMessage);
            EditorApplication.Exit(1);
        }
    }
}
