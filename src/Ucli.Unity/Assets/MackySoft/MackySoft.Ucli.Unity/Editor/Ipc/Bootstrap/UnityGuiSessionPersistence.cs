using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists Unity GUI daemon session registrations. </summary>
    internal static class UnityGuiSessionPersistence
    {
        /// <summary> Writes one GUI daemon session registration to shared local storage. </summary>
        /// <param name="storageRoot"> The shared storage root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this GUI Editor. </param>
        /// <param name="endpoint"> The resolved daemon IPC endpoint. </param>
        /// <param name="sessionOptions"> The normalized session ownership options. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by bootstrap lifecycle. </param>
        /// <returns> The persisted session registration. </returns>
        public static async Task<UnityGuiSessionRegistration> Write (
            string storageRoot,
            string projectFingerprint,
            IpcEndpoint endpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("storageRoot must not be empty.", nameof(storageRoot));
            }

            if (string.IsNullOrWhiteSpace(projectFingerprint))
            {
                throw new ArgumentException("projectFingerprint must not be empty.", nameof(projectFingerprint));
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (sessionOptions == null)
            {
                throw new ArgumentNullException(nameof(sessionOptions));
            }

            var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
            var issuedAtUtc = DateTimeOffset.UtcNow;
            var sessionToken = CreateSessionToken();
            var sessionContract = new DaemonSessionJsonContract(
                SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                SessionToken: sessionToken,
                ProjectFingerprint: projectFingerprint,
                IssuedAtUtc: issuedAtUtc,
                EditorMode: DaemonEditorModeCodec.ToValue(DaemonEditorMode.Gui),
                OwnerKind: sessionOptions.OwnerKind,
                CanShutdownProcess: sessionOptions.CanShutdownProcess,
                EndpointTransportKind: IpcTransportKindCodec.ToValue(endpoint.TransportKind),
                EndpointAddress: endpoint.Address,
                ProcessId: Process.GetCurrentProcess().Id,
                OwnerProcessId: sessionOptions.OwnerProcessId);
            var json = DaemonSessionJsonContractSerializer.Serialize(sessionContract) + Environment.NewLine;
            var sessionDirectoryPath = Path.GetDirectoryName(sessionPath)
                ?? throw new InvalidOperationException($"GUI session directory path could not be resolved: {sessionPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(sessionDirectoryPath);
            await FileUtilities.WriteAllTextAtomically(sessionPath, json, cancellationToken).ConfigureAwait(false);
            FileSystemAccessBoundary.EnsureSecureFile(sessionPath);
            return new UnityGuiSessionRegistration(
                sessionPath,
                issuedAtUtc,
                endpoint,
                sessionOptions.CanShutdownProcess);
        }

        /// <summary> Deletes one GUI daemon session registration and endpoint residue. </summary>
        /// <param name="registration"> The session registration to delete. </param>
        public static void Delete (UnityGuiSessionRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            FileUtilities.DeleteIfExists(registration.SessionPath);
            if (registration.Endpoint.TransportKind != IpcTransportKind.UnixDomainSocket)
            {
                return;
            }

            FileUtilities.DeleteIfExists(registration.Endpoint.Address);
            UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                registration.Endpoint.Address,
                UcliIpcEndpointNames.DaemonAddressPrefix);
        }

        private static string CreateSessionToken ()
        {
            var tokenBuffer = new byte[32];
            using var randomNumberGenerator = RandomNumberGenerator.Create();
            randomNumberGenerator.GetBytes(tokenBuffer);
            return Base64UrlCodec.Encode(tokenBuffer);
        }
    }
}
