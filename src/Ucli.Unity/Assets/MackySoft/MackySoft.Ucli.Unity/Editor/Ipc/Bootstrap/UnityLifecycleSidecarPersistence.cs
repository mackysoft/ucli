using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists Unity lifecycle observations for CLI reads while the IPC endpoint is unavailable. </summary>
    internal sealed class UnityLifecycleSidecarPersistence : IUnityLifecycleSidecarPersistence
    {
        private static readonly TimeSpan SidecarLockAcquireTimeout = TimeSpan.FromSeconds(1);

        private readonly object ownershipSyncRoot = new object();

        private readonly string path;

        private readonly string lockPath;

        private readonly int processId;

        private readonly DateTimeOffset processStartedAtUtc;

        private readonly Guid editorInstanceId;

        private readonly string serverVersion;

        private string lastSuccessfulContents;

        private string currentAttemptContents;

        /// <summary> Initializes persistence with the Editor identity captured for the host generation. </summary>
        /// <param name="storageRoot"> The shared storage root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this Editor. </param>
        /// <param name="editorInstanceId"> The non-empty Editor process identity captured for this host generation. </param>
        /// <param name="serverVersion"> The uCLI server version written to lifecycle observations. </param>
        /// <exception cref="ArgumentException"> Thrown when a required value is empty. </exception>
        public UnityLifecycleSidecarPersistence (
            string storageRoot,
            string projectFingerprint,
            Guid editorInstanceId,
            string serverVersion)
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

            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
            }

            using var currentProcess = Process.GetCurrentProcess();
            path = UcliStoragePathResolver.ResolveDaemonLifecyclePath(storageRoot, projectFingerprint);
            lockPath = path + ".lock";
            processId = currentProcess.Id;
            processStartedAtUtc = currentProcess.StartTime.ToUniversalTime();
            this.editorInstanceId = editorInstanceId;
            this.serverVersion = serverVersion;
        }

        /// <inheritdoc />
        public async Task WriteAsync (
            UnityEditorLifecycleSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var contract = new DaemonLifecycleJsonContract(
                ProcessId: processId,
                ProcessStartedAtUtc: processStartedAtUtc,
                EditorMode: ContractLiteralCodec.ToValue(snapshot.EditorMode),
                LifecycleState: snapshot.LifecycleState,
                BlockingReason: snapshot.BlockingReason,
                CompileState: snapshot.CompileState,
                CompileGeneration: snapshot.CompileGeneration,
                DomainReloadGeneration: snapshot.DomainReloadGeneration,
                ObservedAtUtc: snapshot.ObservedAtUtc ?? DateTimeOffset.UtcNow,
                ActionRequired: snapshot.ActionRequired,
                PrimaryDiagnostic: snapshot.PrimaryDiagnostic)
            {
                ServerVersion = serverVersion,
                CanAcceptExecutionRequests = snapshot.CanAcceptExecutionRequests,
                EditorInstanceId = editorInstanceId.ToString("N"),
                PlayMode = snapshot.PlayMode,
            };
            var contents = DaemonLifecycleJsonContractSerializer.Serialize(contract);
            lock (ownershipSyncRoot)
            {
                currentAttemptContents = contents;
            }

            using var sidecarLock = await FileExclusiveLock.AcquireAsync(
                    lockPath,
                    SidecarLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            await FileUtilities.WriteAllTextAtomicallyAsync(path, contents, cancellationToken)
                .ConfigureAwait(false);
            lock (ownershipSyncRoot)
            {
                lastSuccessfulContents = contents;
                currentAttemptContents = null;
            }
        }

        /// <inheritdoc />
        public async Task DeleteIfOwnedAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string expectedSuccessfulContents;
            string expectedAttemptContents;
            lock (ownershipSyncRoot)
            {
                expectedSuccessfulContents = lastSuccessfulContents;
                expectedAttemptContents = currentAttemptContents;
            }

            if (expectedSuccessfulContents == null && expectedAttemptContents == null)
            {
                return;
            }

            using var sidecarLock = await FileExclusiveLock.AcquireAsync(
                    lockPath,
                    SidecarLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            var persistedContents = await FileUtilities.ReadAllTextOrNullAsync(path, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(
                    persistedContents,
                    expectedSuccessfulContents,
                    StringComparison.Ordinal)
                && !string.Equals(
                    persistedContents,
                    expectedAttemptContents,
                    StringComparison.Ordinal))
            {
                return;
            }

            FileUtilities.DeleteIfExists(path);
        }
    }
}
