using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Schedules uCLI GUI daemon bootstrap during non-batchmode editor initialization. </summary>
    [InitializeOnLoad]
    internal static class UnityGuiBootstrapInitializer
    {
        private static bool isStarted;
        private static IpcGuiBootstrapArguments bootstrapArguments;

        static UnityGuiBootstrapInitializer ()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            IpcGuiBootstrapArgumentsCodec.TryParse(
                Environment.GetCommandLineArgs(),
                out bootstrapArguments,
                out _);
            EditorApplication.update += StartOnEditorUpdate;
        }

        private static void StartOnEditorUpdate ()
        {
            if (isStarted)
            {
                return;
            }

            if (!UnityEditorReadinessGate.IsReadyForBootstrapStartup)
            {
                return;
            }

            isStarted = true;
            EditorApplication.update -= StartOnEditorUpdate;
            _ = UnityGuiBootstrap.Start(bootstrapArguments);
        }
    }
}
