using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists GUI supervisor metadata for CLI-side rebootstrap attach. </summary>
    internal static class UnityGuiSupervisorPersistence
    {
        private static readonly TimeSpan ManifestLockAcquireTimeout = TimeSpan.FromSeconds(1);

        public static async ValueTask<PublicationLease> AcquirePublicationLeaseAsync (
            string storageRoot,
            string projectFingerprint,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
            }

            var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
                storageRoot,
                projectFingerprint);
            var manifestLock = await FileExclusiveLock.AcquireAsync(
                    manifestLockPath,
                    ManifestLockAcquireTimeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return new PublicationLease(storageRoot, projectFingerprint, manifestLock);
        }

        public static void Delete (
            string storageRoot,
            string projectFingerprint,
            IpcSessionToken expectedSessionToken)
        {
            if (expectedSessionToken == null)
            {
                throw new ArgumentNullException(nameof(expectedSessionToken));
            }

            var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
                storageRoot,
                projectFingerprint);
            using var manifestLock = FileExclusiveLock.Acquire(
                manifestLockPath,
                ManifestLockAcquireTimeout,
                CancellationToken.None);
            DeleteWhileLockIsHeld(storageRoot, projectFingerprint, expectedSessionToken);
        }

        private static void DeleteWhileLockIsHeld (
            string storageRoot,
            string projectFingerprint,
            IpcSessionToken expectedSessionToken)
        {
            var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(storageRoot, projectFingerprint);
            var json = FileUtilities.ReadAllTextOrNull(manifestPath);
            if (json == null)
            {
                return;
            }

            GuiSupervisorManifestJsonContract manifest;
            try
            {
                manifest = GuiSupervisorManifestJsonContractSerializer.Deserialize(json);
            }
            catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException)
            {
                return;
            }

            if (manifest == null
                || !expectedSessionToken.Matches(manifest.SessionToken))
            {
                return;
            }

            FileUtilities.DeleteIfExists(manifestPath);
        }

        private static DateTimeOffset? TryGetProcessStartedAtUtc (Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            try
            {
                return new DateTimeOffset(process.StartTime).ToUniversalTime();
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
            {
                return null;
            }
        }

        internal sealed class PublicationLease : IDisposable
        {
            private readonly string storageRoot;

            private readonly string projectFingerprint;

            private FileExclusiveLock manifestLock;

            public PublicationLease (
                string storageRoot,
                string projectFingerprint,
                FileExclusiveLock manifestLock)
            {
                this.storageRoot = storageRoot;
                this.projectFingerprint = projectFingerprint;
                this.manifestLock = manifestLock ?? throw new ArgumentNullException(nameof(manifestLock));
            }

            public async ValueTask<GuiSupervisorManifestJsonContract> PublishAsync (
                IpcEndpoint endpoint,
                IpcSessionToken sessionToken,
                DateTimeOffset issuedAtUtc,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (manifestLock == null)
                {
                    throw new ObjectDisposedException(nameof(PublicationLease));
                }

                if (endpoint == null)
                {
                    throw new ArgumentNullException(nameof(endpoint));
                }

                if (sessionToken == null)
                {
                    throw new ArgumentNullException(nameof(sessionToken));
                }

                using var currentProcess = Process.GetCurrentProcess();
                var manifest = new GuiSupervisorManifestJsonContract(
                    SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
                    SessionToken: sessionToken.GetEncodedValue(),
                    ProjectFingerprint: projectFingerprint,
                    EndpointTransportKind: ContractLiteralCodec.ToValue(endpoint.TransportKind),
                    EndpointAddress: endpoint.Address,
                    ProcessId: currentProcess.Id,
                    ProcessStartedAtUtc: TryGetProcessStartedAtUtc(currentProcess),
                    IssuedAtUtc: issuedAtUtc);
                var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
                    storageRoot,
                    projectFingerprint);
                var manifestDirectory = Path.GetDirectoryName(manifestPath);
                if (string.IsNullOrEmpty(manifestDirectory))
                {
                    throw new InvalidOperationException(
                        $"GUI supervisor manifest directory could not be resolved. Path={manifestPath}");
                }

                FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectory);
                await FileUtilities.WriteAllTextAtomicallyAsync(
                        manifestPath,
                        GuiSupervisorManifestJsonContractSerializer.Serialize(manifest) + Environment.NewLine,
                        cancellationToken)
                    .ConfigureAwait(false);
                FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
                return manifest;
            }

            public void DeleteIfOwned (IpcSessionToken expectedSessionToken)
            {
                if (manifestLock == null)
                {
                    throw new ObjectDisposedException(nameof(PublicationLease));
                }

                if (expectedSessionToken == null)
                {
                    throw new ArgumentNullException(nameof(expectedSessionToken));
                }

                DeleteWhileLockIsHeld(storageRoot, projectFingerprint, expectedSessionToken);
            }

            public void Dispose ()
            {
                var ownedLock = Interlocked.Exchange(ref manifestLock, null);
                ownedLock?.Dispose();
            }
        }
    }
}
