using System;
using System.Diagnostics;
using System.Globalization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists Unity lifecycle observations for CLI reads while the IPC endpoint is unavailable. </summary>
    internal static class UnityLifecycleSidecarPersistence
    {
        /// <summary> Writes one lifecycle observation sidecar. </summary>
        public static void Write (
            string storageRoot,
            string projectFingerprint,
            string serverVersion,
            UnityEditorLifecycleSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("storageRoot must not be empty.", nameof(storageRoot));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("projectFingerprint must not be empty.", nameof(projectFingerprint));
            }

            if (string.IsNullOrWhiteSpace(serverVersion))
            {
                throw new ArgumentException("serverVersion must not be empty.", nameof(serverVersion));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            using var currentProcess = Process.GetCurrentProcess();
            var path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
            var contract = new DaemonLifecycleJsonContract(
                ProcessId: currentProcess.Id,
                ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                EditorMode: ContractLiteralCodec.ToValue(snapshot.EditorMode),
                LifecycleState: ContractLiteralCodec.ToValue(snapshot.LifecycleState),
                BlockingReason: snapshot.BlockingReason.HasValue
                    ? ContractLiteralCodec.ToValue(snapshot.BlockingReason.Value)
                    : null,
                CompileState: ContractLiteralCodec.ToValue(snapshot.CompileState),
                CompileGeneration: snapshot.CompileGeneration.ToString(CultureInfo.InvariantCulture),
                DomainReloadGeneration: snapshot.DomainReloadGeneration.ToString(CultureInfo.InvariantCulture),
                ObservedAtUtc: snapshot.ObservedAtUtc ?? DateTimeOffset.UtcNow,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic)
            {
                ServerVersion = serverVersion,
                CanAcceptExecutionRequests = snapshot.CanAcceptExecutionRequests,
                EditorInstanceId = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId(),
                PlayMode = UnityLifecycleResponseCodec.CreatePlayModeSnapshot(snapshot.PlayMode),
            };
            FileUtilities.WriteAllTextAtomically(path, DaemonLifecycleJsonContractSerializer.Serialize(contract));
        }
    }
}
