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
            string serverVersion,
            UnityEditorObservation snapshot)
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
                processId: currentProcess.Id,
                processStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                state: snapshot.State,
                observedAtUtc: snapshot.ObservedAtUtc,
                actionRequired: snapshot.ActionRequired,
                primaryDiagnostic: snapshot.PrimaryDiagnostic,
                serverVersion: serverVersion,
                editorInstanceId: UnityEditorSessionStateStore.GetOrCreateEditorInstanceId());
            FileUtilities.WriteAllTextAtomically(path, DaemonLifecycleJsonContractSerializer.Serialize(contract));
        }
    }
}
