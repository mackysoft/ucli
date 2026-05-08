using System;
using System.Collections.Generic;
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

            if (!TryResolveBootstrapArguments(
                Environment.GetCommandLineArgs(),
                out bootstrapArguments,
                out var error))
            {
                Debug.LogError($"uCLI GUI bootstrap arguments are invalid. {error.Message}");
                return;
            }

            EditorApplication.update += StartOnEditorUpdate;
        }

        /// <summary> Resolves optional GUI bootstrap arguments while keeping absent marker semantics distinct from invalid marker values. </summary>
        /// <param name="args"> The Unity command-line token list. </param>
        /// <param name="arguments"> The parsed CLI-owned GUI bootstrap arguments, or <see langword="null" /> for user-owned GUI sessions. </param>
        /// <param name="error"> The parse error when resolution fails; otherwise the no-error value. </param>
        /// <returns> <see langword="true" /> when bootstrap may continue; otherwise <see langword="false" />. </returns>
        internal static bool TryResolveBootstrapArguments (
            IReadOnlyList<string> args,
            out IpcGuiBootstrapArguments arguments,
            out IpcGuiBootstrapParseError error)
        {
            if (IpcGuiBootstrapArgumentsCodec.TryParse(args, out arguments, out error))
            {
                return true;
            }

            if (error.Kind != IpcGuiBootstrapParseErrorKind.MissingTarget)
            {
                return false;
            }

            arguments = null;
            error = IpcGuiBootstrapParseError.None;
            return true;
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
