using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Converts daemon bootstrap transport literals to Unity IPC transport enums. </summary>
    internal static class UnityIpcTransportKindCodec
    {
        /// <summary> Tries to parse transport kind from persisted daemon session value. </summary>
        /// <param name="value"> The transport kind literal value. </param>
        /// <param name="transportKind"> The parsed transport kind enum. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            string value,
            out IpcTransportKind transportKind)
        {
            if (string.Equals(value, IpcTransportKindValues.NamedPipe, StringComparison.Ordinal))
            {
                transportKind = IpcTransportKind.NamedPipe;
                return true;
            }

            if (string.Equals(value, IpcTransportKindValues.UnixDomainSocket, StringComparison.Ordinal))
            {
                transportKind = IpcTransportKind.UnixDomainSocket;
                return true;
            }

            transportKind = default;
            return false;
        }
    }
}
