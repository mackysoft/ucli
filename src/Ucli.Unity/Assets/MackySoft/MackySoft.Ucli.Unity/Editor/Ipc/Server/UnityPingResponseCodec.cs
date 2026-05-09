using System;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Encodes ping response payload values for Unity IPC server. </summary>
    internal static class UnityPingResponseCodec
    {
        /// <summary> Creates one ping response payload from Unity editor environment values. </summary>
        /// <param name="unityVersion"> The Unity editor version string. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="projectFingerprint"> The Unity project fingerprint served by this IPC host. </param>
        /// <param name="snapshot"> The normalized editor lifecycle snapshot. </param>
        /// <returns> The ping response payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" />, <paramref name="serverVersion" />, or <paramref name="projectFingerprint" /> is empty or whitespace. </exception>
        public static IpcPingResponse CreatePayload (
            string unityVersion,
            string serverVersion,
            string projectFingerprint,
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

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("projectFingerprint must not be empty.", nameof(projectFingerprint));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new IpcPingResponse(
                ServerVersion: serverVersion,
                EditorMode: DaemonEditorModeCodec.ToValue(snapshot.EditorMode),
                UnityVersion: unityVersion,
                ProjectFingerprint: projectFingerprint,
                CompileState: snapshot.CompileState,
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests);
        }
    }
}
