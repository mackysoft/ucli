using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Schedules uCLI batchmode bootstrap during editor initialization. </summary>
    [InitializeOnLoad]
    internal static class UnityBatchmodeBootstrapInitializer
    {
        private static bool isStarted;
        private static IpcBatchmodeBootstrapArguments bootstrapArguments = default!;

        static UnityBatchmodeBootstrapInitializer ()
        {
            if (!Application.isBatchMode)
            {
                return;
            }

            if (!IpcBatchmodeBootstrapArgumentsCodec.TryParse(
                    Environment.GetCommandLineArgs(),
                    out bootstrapArguments,
                    out _))
            {
                return;
            }

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
            switch (bootstrapArguments)
            {
                case IpcDaemonBootstrapArguments daemonBootstrapArguments:
                    _ = UnityDaemonBootstrap.Start(daemonBootstrapArguments);
                    break;

                case IpcOneshotBootstrapArguments oneshotBootstrapArguments:
                    _ = UnityOneshotBootstrap.Start(oneshotBootstrapArguments);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(bootstrapArguments), bootstrapArguments, null);
            }
        }
    }
}
