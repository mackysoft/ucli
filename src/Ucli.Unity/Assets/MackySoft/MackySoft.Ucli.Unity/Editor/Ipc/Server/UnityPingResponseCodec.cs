using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Encodes ping response payload values for Unity IPC server. </summary>
    internal static class UnityPingResponseCodec
    {
        /// <summary> Creates one ping response payload from runtime environment values. </summary>
        /// <param name="unityVersion"> The Unity editor version string. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="snapshot"> The normalized editor lifecycle snapshot. </param>
        /// <returns> The ping response payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" /> or <paramref name="serverVersion" /> is empty or whitespace. </exception>
        public static IpcPingResponse CreatePayload (
            string unityVersion,
            string serverVersion,
            UnityEditorLifecycleSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(unityVersion))
            {
                throw new ArgumentException("unityVersion must not be empty.", nameof(unityVersion));
            }

            if (string.IsNullOrWhiteSpace(serverVersion))
            {
                throw new ArgumentException("serverVersion must not be empty.", nameof(serverVersion));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new IpcPingResponse(
                ServerVersion: serverVersion,
                Runtime: snapshot.Runtime,
                UnityVersion: unityVersion,
                CompileState: snapshot.CompileState,
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests);
        }
    }
}
