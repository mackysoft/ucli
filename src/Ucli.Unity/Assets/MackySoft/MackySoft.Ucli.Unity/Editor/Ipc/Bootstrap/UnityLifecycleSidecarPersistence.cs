using System;
using System.Diagnostics;
using MackySoft.Ucli.Contracts.Storage;
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

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var currentProcess = Process.GetCurrentProcess();
            var path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
            var contract = new DaemonLifecycleJsonContract(
                ProcessId: currentProcess.Id,
                ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                EditorMode: MackySoft.Ucli.Contracts.Daemon.DaemonEditorModeCodec.ToValue(snapshot.EditorMode),
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileState: snapshot.CompileState,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                ObservedAtUtc: snapshot.ObservedAtUtc ?? DateTimeOffset.UtcNow,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic);
            FileUtilities.WriteAllTextAtomically(path, DaemonLifecycleJsonContractSerializer.Serialize(contract));
        }
    }
}
