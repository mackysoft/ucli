using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists Unity lifecycle observations for CLI reads while the IPC endpoint is unavailable. </summary>
    internal sealed class UnityLifecycleSidecarPersistence : IUnityLifecycleSidecarPersistence
    {
        private static readonly TimeSpan SidecarLockAcquireTimeout = TimeSpan.FromSeconds(1);

        private readonly AbsolutePath path;

        private readonly AbsolutePath lockPath;

        private readonly int processId;

        private readonly DateTimeOffset processStartedAtUtc;

        private readonly Guid editorInstanceId;

        private readonly Guid sidecarGenerationId;

        private readonly string serverVersion;

        /// <summary> Initializes persistence with the Editor identity captured for the host generation. </summary>
        /// <param name="storageRoot"> The shared storage root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this Editor. </param>
        /// <param name="editorInstanceId"> The non-empty Editor process identity captured for this host generation. </param>
        /// <param name="sidecarGenerationId"> The non-empty identity of the lifecycle sidecar writer generation. </param>
        /// <param name="serverVersion"> The uCLI server version written to lifecycle observations. </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="storageRoot" /> or <paramref name="projectFingerprint" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException"> Thrown when a required text or identifier value is empty. </exception>
        public UnityLifecycleSidecarPersistence (
            AbsolutePath storageRoot,
            ProjectFingerprint projectFingerprint,
            Guid editorInstanceId,
            Guid sidecarGenerationId,
            string serverVersion)
        {
            if (storageRoot == null)
            {
                throw new ArgumentNullException(nameof(storageRoot));
            }

            if (projectFingerprint == null)
            {
                throw new ArgumentNullException(nameof(projectFingerprint));
            }

            if (string.IsNullOrWhiteSpace(serverVersion))
            {
                throw new ArgumentException("serverVersion must not be empty.", nameof(serverVersion));
            }

            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
            }

            if (sidecarGenerationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Sidecar generation identifier must not be empty.",
                    nameof(sidecarGenerationId));
            }

            using var currentProcess = Process.GetCurrentProcess();
            path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
            lockPath = UcliStoragePathResolver.ResolveDaemonLifecycleLockPath(storageRoot, projectFingerprint);
            processId = currentProcess.Id;
            processStartedAtUtc = currentProcess.StartTime.ToUniversalTime();
            this.editorInstanceId = editorInstanceId;
            this.sidecarGenerationId = sidecarGenerationId;
            this.serverVersion = serverVersion;
        }

        /// <inheritdoc />
        public async Task WriteAsync (
            UnityEditorObservation snapshot,
            DaemonLifecycleRecoveryLease recoveryLease,
            CancellationToken cancellationToken)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var contract = new DaemonLifecycleJsonContract(
                processId: processId,
                processStartedAtUtc: processStartedAtUtc,
                state: snapshot.State,
                observedAtUtc: snapshot.ObservedAtUtc,
                actionRequired: snapshot.ActionRequired,
                primaryDiagnostic: snapshot.PrimaryDiagnostic,
                sidecarGenerationId: sidecarGenerationId,
                serverVersion: serverVersion,
                editorInstanceId: editorInstanceId,
                recoveryLease: recoveryLease);
            var contents = DaemonLifecycleJsonContractSerializer.Serialize(contract);

            using var sidecarLock = await FileExclusiveLock.AcquireAsync(
                    lockPath,
                    SidecarLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            await FileUtilities.WriteAllTextAtomicallyAsync(path, contents, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteIfOwnedAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sidecarLock = await FileExclusiveLock.AcquireAsync(
                    lockPath,
                    SidecarLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            var persistedContents = await FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (persistedContents == null)
            {
                return;
            }

            DaemonLifecycleJsonContract persistedContract;
            try
            {
                persistedContract = DaemonLifecycleJsonContractSerializer.Deserialize(persistedContents);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                return;
            }

            if (persistedContract == null
                || persistedContract.SidecarGenerationId != sidecarGenerationId)
            {
                return;
            }

            FileUtilities.DeleteIfExists(path);
        }
    }
}
