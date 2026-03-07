using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Schedules Unity daemon bootstrap during batchmode editor initialization. </summary>
    [InitializeOnLoad]
    internal static class UnityDaemonBootstrapInitializer
    {
        private static bool isStarted;

        static UnityDaemonBootstrapInitializer ()
        {
            if (!ShouldScheduleBootstrap(Environment.GetCommandLineArgs(), Application.isBatchMode))
            {
                return;
            }

            EditorApplication.update += StartOnEditorUpdate;
        }

        internal static bool ShouldScheduleBootstrap (
            IReadOnlyList<string> args,
            bool isBatchMode)
        {
            if (!isBatchMode)
            {
                return false;
            }

            return IpcDaemonBootstrapArgumentsCodec.TryParse(args, out _, out _);
        }

        private static void StartOnEditorUpdate ()
        {
            if (isStarted)
            {
                return;
            }

            if (EditorApplication.isCompiling)
            {
                return;
            }

            isStarted = true;
            EditorApplication.update -= StartOnEditorUpdate;
            _ = UnityDaemonBootstrap.Start();
        }
    }
}
