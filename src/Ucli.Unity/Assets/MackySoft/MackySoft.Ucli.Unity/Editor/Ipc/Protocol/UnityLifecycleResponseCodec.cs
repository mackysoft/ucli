using System;
using System.Globalization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Encodes lifecycle-bearing response payload values for Unity IPC protocol responses. </summary>
    internal static class UnityLifecycleResponseCodec
    {
        /// <summary> Creates one ping response payload from Unity editor environment values. </summary>
        /// <param name="unityVersion"> The Unity editor version string. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="projectFingerprint"> The Unity project fingerprint served by this IPC host. </param>
        /// <param name="snapshot"> The normalized editor lifecycle snapshot. </param>
        /// <returns> The ping response payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" />, <paramref name="serverVersion" />, or <paramref name="projectFingerprint" /> is empty or whitespace. </exception>
        public static IpcPingResponse CreatePingPayload (
            string unityVersion,
            string serverVersion,
            string projectFingerprint,
            UnityEditorLifecycleSnapshot snapshot)
        {
            ValidateInputs(unityVersion, serverVersion, projectFingerprint, snapshot);

            return new IpcPingResponse(
                ServerVersion: serverVersion,
                EditorMode: ContractLiteralCodec.ToValue(snapshot.EditorMode),
                UnityVersion: unityVersion,
                ProjectFingerprint: projectFingerprint,
                CompileState: ContractLiteralCodec.ToValue(snapshot.CompileState),
                LifecycleState: ContractLiteralCodec.ToValue(snapshot.LifecycleState),
                BlockingReason: ToBlockingReasonLiteral(snapshot.BlockingReason),
                CompileGeneration: snapshot.CompileGeneration.ToString(CultureInfo.InvariantCulture),
                DomainReloadGeneration: snapshot.DomainReloadGeneration.ToString(CultureInfo.InvariantCulture),
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic,
                PlayMode: CreatePlayModeSnapshot(snapshot.PlayMode));
        }

        /// <summary> Creates one Play Mode lifecycle snapshot from Unity editor environment values. </summary>
        /// <param name="unityVersion"> The Unity editor version string. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="projectFingerprint"> The Unity project fingerprint served by this IPC host. </param>
        /// <param name="snapshot"> The normalized editor lifecycle snapshot. </param>
        /// <returns> The Play Mode lifecycle snapshot payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" />, <paramref name="serverVersion" />, or <paramref name="projectFingerprint" /> is empty or whitespace. </exception>
        public static IpcPlayLifecycleSnapshot CreatePlayLifecycleSnapshot (
            string unityVersion,
            string serverVersion,
            string projectFingerprint,
            UnityEditorLifecycleSnapshot snapshot)
        {
            ValidateInputs(unityVersion, serverVersion, projectFingerprint, snapshot);

            return new IpcPlayLifecycleSnapshot(
                ServerVersion: serverVersion,
                EditorMode: ContractLiteralCodec.ToValue(snapshot.EditorMode),
                UnityVersion: unityVersion,
                ProjectFingerprint: projectFingerprint,
                LifecycleState: ContractLiteralCodec.ToValue(snapshot.LifecycleState),
                BlockingReason: ToBlockingReasonLiteral(snapshot.BlockingReason),
                CompileState: ContractLiteralCodec.ToValue(snapshot.CompileState),
                CompileGeneration: snapshot.CompileGeneration.ToString(CultureInfo.InvariantCulture),
                DomainReloadGeneration: snapshot.DomainReloadGeneration.ToString(CultureInfo.InvariantCulture),
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic,
                PlayMode: CreatePlayModeSnapshot(snapshot.PlayMode));
        }

        /// <summary> Creates one build lifecycle snapshot from Unity editor environment values. </summary>
        /// <param name="unityVersion"> The Unity editor version string. </param>
        /// <param name="serverVersion"> The daemon server version string. </param>
        /// <param name="projectFingerprint"> The Unity project fingerprint served by this IPC host. </param>
        /// <param name="snapshot"> The normalized editor lifecycle snapshot. </param>
        /// <returns> The build lifecycle snapshot payload. </returns>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="unityVersion" />, <paramref name="serverVersion" />, or <paramref name="projectFingerprint" /> is empty or whitespace. </exception>
        public static IpcBuildLifecycleSnapshot CreateBuildLifecycleSnapshot (
            string unityVersion,
            string serverVersion,
            string projectFingerprint,
            UnityEditorLifecycleSnapshot snapshot)
        {
            ValidateInputs(unityVersion, serverVersion, projectFingerprint, snapshot);

            return new IpcBuildLifecycleSnapshot(
                ServerVersion: serverVersion,
                EditorMode: ContractLiteralCodec.ToValue(snapshot.EditorMode),
                UnityVersion: unityVersion,
                ProjectFingerprint: projectFingerprint,
                LifecycleState: ContractLiteralCodec.ToValue(snapshot.LifecycleState),
                BlockingReason: ToBlockingReasonLiteral(snapshot.BlockingReason),
                CompileState: ContractLiteralCodec.ToValue(snapshot.CompileState),
                CompileGeneration: snapshot.CompileGeneration.ToString(CultureInfo.InvariantCulture),
                DomainReloadGeneration: snapshot.DomainReloadGeneration.ToString(CultureInfo.InvariantCulture),
                CanAcceptExecutionRequests: snapshot.CanAcceptExecutionRequests,
                ObservedAtUtc: snapshot.ObservedAtUtc,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic,
                PlayMode: CreatePlayModeSnapshot(snapshot.PlayMode),
                AssetRefreshGeneration: snapshot.AssetRefreshGeneration.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary> Projects one typed runtime Play Mode snapshot to its IPC representation. </summary>
        public static IpcPlayModeSnapshot CreatePlayModeSnapshot (UnityEditorPlayModeSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new IpcPlayModeSnapshot(
                State: ContractLiteralCodec.ToValue(snapshot.State),
                Transition: ContractLiteralCodec.ToValue(snapshot.Transition),
                IsPlaying: snapshot.IsPlaying,
                IsPlayingOrWillChangePlaymode: snapshot.IsPlayingOrWillChangePlaymode,
                Generation: snapshot.Generation.ToString(CultureInfo.InvariantCulture));
        }

        private static string ToBlockingReasonLiteral (IpcEditorBlockingReason? blockingReason)
        {
            return blockingReason.HasValue
                ? ContractLiteralCodec.ToValue(blockingReason.Value)
                : null;
        }

        private static void ValidateInputs (
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
        }
    }
}
