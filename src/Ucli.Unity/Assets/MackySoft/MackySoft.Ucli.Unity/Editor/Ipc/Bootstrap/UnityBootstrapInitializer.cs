using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using UnityEditor;
using UnityEngine;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Schedules uCLI bootstrap during editor initialization. </summary>
    [InitializeOnLoad]
    internal static class UnityBootstrapInitializer
    {
        private static bool isStarted;
        private static BootstrapStartupKind startupKind;
        private static IpcBatchmodeBootstrapArguments batchmodeBootstrapArguments = default!;
        private static IpcGuiBootstrapArguments guiBootstrapArguments;

        static UnityBootstrapInitializer ()
        {
            var args = Environment.GetCommandLineArgs();
            if (Application.isBatchMode)
            {
                if (!IpcBatchmodeBootstrapArgumentsCodec.TryParse(
                        args,
                        out batchmodeBootstrapArguments,
                        out _))
                {
                    return;
                }

                startupKind = BootstrapStartupKind.Batchmode;
            }
            else
            {
                if (!TryResolveGuiBootstrapArguments(
                    args,
                    out guiBootstrapArguments,
                    out var error))
                {
                    Debug.LogError($"uCLI GUI bootstrap arguments are invalid. {error.Message}");
                    return;
                }

                startupKind = BootstrapStartupKind.Gui;
            }

            EditorApplication.update += StartOnEditorUpdate;
        }

        /// <summary> Resolves optional GUI bootstrap arguments while keeping absent marker semantics distinct from invalid marker values. </summary>
        /// <param name="args"> The Unity command-line token list. </param>
        /// <param name="arguments"> The parsed CLI-owned GUI bootstrap arguments, or <see langword="null" /> for user-owned GUI sessions. </param>
        /// <param name="error"> The parse error when resolution fails; otherwise the no-error value. </param>
        /// <returns> <see langword="true" /> when bootstrap may continue; otherwise <see langword="false" />. </returns>
        internal static bool TryResolveGuiBootstrapArguments (
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
            switch (startupKind)
            {
                case BootstrapStartupKind.Batchmode:
                    StartBatchmodeBootstrap();
                    break;

                case BootstrapStartupKind.Gui:
                    _ = UnityGuiBootstrap.StartAsync(guiBootstrapArguments);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(startupKind), startupKind, null);
            }
        }

        private static void StartBatchmodeBootstrap ()
        {
            switch (batchmodeBootstrapArguments)
            {
                case IpcDaemonBootstrapArguments daemonBootstrapArguments:
                    _ = UnityDaemonBootstrap.StartAsync(daemonBootstrapArguments);
                    break;

                case IpcOneshotBootstrapArguments oneshotBootstrapArguments:
                    _ = UnityOneshotBootstrap.StartAsync(oneshotBootstrapArguments);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(batchmodeBootstrapArguments), batchmodeBootstrapArguments, null);
            }
        }

        private enum BootstrapStartupKind
        {
            None = 0,
            Batchmode = 1,
            Gui = 2,
        }
    }
}
