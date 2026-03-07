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
        private static bool isRegistered;

        private static bool isStarted;

        static UnityDaemonBootstrapInitializer ()
        {
            if (!ShouldScheduleBootstrap(Environment.GetCommandLineArgs(), Application.isBatchMode))
            {
                return;
            }

            RegisterUpdateCallback();
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

        private static void RegisterUpdateCallback ()
        {
            if (isRegistered)
            {
                return;
            }

            EditorApplication.update += StartOnEditorUpdate;
            isRegistered = true;
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
            UnityDaemonBootstrap.Start();
        }

        private static void UnregisterUpdateCallback ()
        {
            if (!isRegistered)
            {
                return;
            }

            EditorApplication.update -= StartOnEditorUpdate;
            isRegistered = false;
        }
    }
}
