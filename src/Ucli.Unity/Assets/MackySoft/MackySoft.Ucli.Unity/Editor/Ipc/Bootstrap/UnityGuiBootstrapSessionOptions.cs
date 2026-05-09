using System;
using System.Diagnostics;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents normalized GUI session ownership values for bootstrap registration. </summary>
    internal sealed record UnityGuiBootstrapSessionOptions (
        string OwnerKind,
        bool CanShutdownProcess,
        int OwnerProcessId)
    {
        /// <summary> Creates session options from optional CLI GUI bootstrap arguments. </summary>
        /// <param name="arguments"> The optional CLI GUI bootstrap arguments. </param>
        /// <returns> The normalized session ownership options. </returns>
        public static UnityGuiBootstrapSessionOptions Create (IpcGuiBootstrapArguments arguments)
        {
            if (arguments == null)
            {
                return CreateUserOwned();
            }

            return new UnityGuiBootstrapSessionOptions(
                OwnerKind: DaemonSessionOwnerKindCodec.ToValue(DaemonSessionOwnerKind.Cli),
                CanShutdownProcess: arguments.CanShutdownProcess,
                OwnerProcessId: arguments.OwnerProcessId);
        }

        private static UnityGuiBootstrapSessionOptions CreateUserOwned ()
        {
            return new UnityGuiBootstrapSessionOptions(
                OwnerKind: DaemonSessionOwnerKindCodec.ToValue(DaemonSessionOwnerKind.User),
                CanShutdownProcess: false,
                OwnerProcessId: Process.GetCurrentProcess().Id);
        }
    }
}
