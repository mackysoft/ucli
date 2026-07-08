using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    internal enum UnityGuiSessionReplacementScope
    {
        EquivalentCurrentProcessSession,
        AnyCurrentProcessSession,
    }

    /// <summary> Persists Unity GUI daemon session registrations. </summary>
    internal static class UnityGuiSessionPersistence
    {
        /// <summary> Writes one GUI daemon session registration to shared local storage. </summary>
        /// <param name="storageRoot"> The shared storage root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this GUI Editor. </param>
        /// <param name="endpoint"> The resolved daemon IPC endpoint. </param>
        /// <param name="sessionOptions"> The normalized session ownership options. </param>
        /// <param name="sessionReplacementScope"> The scope of existing current-process GUI sessions that may be replaced. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by bootstrap lifecycle. </param>
        /// <returns> The persisted session registration. </returns>
        public static async Task<UnityGuiSessionRegistration> WriteAsync (
            string storageRoot,
            string projectFingerprint,
            IpcEndpoint endpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSessionReplacementScope(sessionReplacementScope);
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
            var sessionDirectoryPath = Path.GetDirectoryName(sessionPath)
                ?? throw new InvalidOperationException($"GUI session directory path could not be resolved: {sessionPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(sessionDirectoryPath);

            using var currentProcess = Process.GetCurrentProcess();
            var currentProcessId = currentProcess.Id;
            var currentProcessStartedAtUtc = currentProcess.StartTime.ToUniversalTime();
            var currentEditorInstanceId = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();
            DeleteCurrentProcessGuiSessionIfPresent(
                sessionPath,
                projectFingerprint,
                endpoint,
                sessionOptions,
                currentProcessId,
                currentProcessStartedAtUtc,
                currentEditorInstanceId,
                sessionReplacementScope);

            var issuedAtUtc = DateTimeOffset.UtcNow;
            var sessionToken = CreateSessionToken();
            var sessionContract = new DaemonSessionJsonContract(
                SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                SessionToken: sessionToken,
                ProjectFingerprint: projectFingerprint,
                IssuedAtUtc: issuedAtUtc,
                EditorMode: ContractLiteralCodec.ToValue(DaemonEditorMode.Gui),
                OwnerKind: sessionOptions.OwnerKind,
                CanShutdownProcess: sessionOptions.CanShutdownProcess,
                EndpointTransportKind: ContractLiteralCodec.ToValue(endpoint.TransportKind),
                EndpointAddress: endpoint.Address,
                ProcessId: currentProcessId,
                ProcessStartedAtUtc: currentProcessStartedAtUtc,
                OwnerProcessId: sessionOptions.OwnerProcessId)
            {
                EditorInstanceId = currentEditorInstanceId,
            };
            var json = DaemonSessionJsonContractSerializer.Serialize(sessionContract) + Environment.NewLine;
            await WriteSessionJsonCreateNewAsync(sessionPath, json, cancellationToken).ConfigureAwait(false);
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

            if (!File.Exists(registration.SessionPath))
            {
                return;
            }

            var json = File.ReadAllText(registration.SessionPath);
            var sessionContract = DaemonSessionJsonContractSerializer.Deserialize(json);
            if ((sessionContract == null) || !MatchesRegistration(registration, sessionContract))
            {
                return;
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

        private static void ValidateSessionReplacementScope (UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            switch (sessionReplacementScope)
            {
                case UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession:
                case UnityGuiSessionReplacementScope.AnyCurrentProcessSession:
                    return;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sessionReplacementScope), sessionReplacementScope, null);
            }
        }

        private static async Task WriteSessionJsonCreateNewAsync (
            string sessionPath,
            string json,
            CancellationToken cancellationToken)
        {
            try
            {
                await WriteSessionJsonCreateNewCoreAsync(sessionPath, json, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception) when (File.Exists(sessionPath))
            {
                throw new InvalidOperationException($"GUI session already exists: {sessionPath}", exception);
            }
        }

        private static void DeleteCurrentProcessGuiSessionIfPresent (
            string sessionPath,
            string projectFingerprint,
            IpcEndpoint expectedEndpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            int currentProcessId,
            DateTimeOffset currentProcessStartedAtUtc,
            string currentEditorInstanceId,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            if (!File.Exists(sessionPath))
            {
                return;
            }

            DaemonSessionJsonContract sessionContract;
            try
            {
                sessionContract = DaemonSessionJsonContractSerializer.Deserialize(File.ReadAllText(sessionPath));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
            {
                return;
            }

            if (sessionContract == null
                || !MatchesCurrentProcessGuiSession(
                    sessionContract,
                    projectFingerprint,
                    expectedEndpoint,
                    sessionOptions,
                    currentProcessId,
                    currentProcessStartedAtUtc,
                    currentEditorInstanceId,
                    sessionReplacementScope))
            {
                return;
            }

            FileUtilities.DeleteIfExists(sessionPath);
            DeleteUnixEndpointResidue(sessionContract, expectedEndpoint);
        }

        private static async Task WriteSessionJsonCreateNewCoreAsync (
            string sessionPath,
            string json,
            CancellationToken cancellationToken)
        {
            var temporaryPath = sessionPath + $".tmp.{Guid.NewGuid():N}";

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    var buffer = Encoding.UTF8.GetBytes(json);
                    await stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, sessionPath);
            }
            finally
            {
                FileUtilities.DeleteIfExists(temporaryPath);
            }
        }

        private static bool MatchesCurrentProcessGuiSession (
            DaemonSessionJsonContract sessionContract,
            string projectFingerprint,
            IpcEndpoint expectedEndpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            int currentProcessId,
            DateTimeOffset currentProcessStartedAtUtc,
            string currentEditorInstanceId,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            if (sessionContract.SchemaVersion != DaemonSessionStorageContract.CurrentSchemaVersion
                || !string.Equals(sessionContract.ProjectFingerprint, projectFingerprint, StringComparison.Ordinal)
                || !ContractLiteralCodec.Matches(sessionContract.EditorMode, DaemonEditorMode.Gui)
                || sessionContract.ProcessId != currentProcessId
                || !MatchesCurrentProcessIdentity(sessionContract, currentProcessStartedAtUtc, currentEditorInstanceId))
            {
                return false;
            }

            if (sessionReplacementScope == UnityGuiSessionReplacementScope.AnyCurrentProcessSession)
            {
                return true;
            }

            if (sessionReplacementScope != UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionReplacementScope), sessionReplacementScope, null);
            }

            return string.Equals(sessionContract.OwnerKind, sessionOptions.OwnerKind, StringComparison.Ordinal)
                && sessionContract.CanShutdownProcess == sessionOptions.CanShutdownProcess
                && ContractLiteralCodec.Matches(sessionContract.EndpointTransportKind, expectedEndpoint.TransportKind)
                && string.Equals(sessionContract.EndpointAddress, expectedEndpoint.Address, StringComparison.Ordinal)
                && sessionContract.OwnerProcessId == sessionOptions.OwnerProcessId;
        }

        private static bool MatchesCurrentProcessIdentity (
            DaemonSessionJsonContract sessionContract,
            DateTimeOffset currentProcessStartedAtUtc,
            string currentEditorInstanceId)
        {
            if (!string.IsNullOrWhiteSpace(sessionContract.EditorInstanceId)
                && !string.IsNullOrWhiteSpace(currentEditorInstanceId))
            {
                return string.Equals(sessionContract.EditorInstanceId, currentEditorInstanceId, StringComparison.Ordinal);
            }

            return sessionContract.ProcessStartedAtUtc == currentProcessStartedAtUtc;
        }

        private static void DeleteUnixEndpointResidue (
            DaemonSessionJsonContract sessionContract,
            IpcEndpoint expectedEndpoint)
        {
            if (expectedEndpoint.TransportKind != IpcTransportKind.UnixDomainSocket
                || !ContractLiteralCodec.Matches(sessionContract.EndpointTransportKind, expectedEndpoint.TransportKind)
                || !string.Equals(sessionContract.EndpointAddress, expectedEndpoint.Address, StringComparison.Ordinal))
            {
                return;
            }

            FileUtilities.DeleteIfExists(expectedEndpoint.Address);
            UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                expectedEndpoint.Address,
                UcliIpcEndpointNames.DaemonAddressPrefix);
        }

        private static bool MatchesRegistration (
            UnityGuiSessionRegistration registration,
            DaemonSessionJsonContract sessionContract)
        {
            using var currentProcess = Process.GetCurrentProcess();
            return sessionContract.SchemaVersion == DaemonSessionStorageContract.CurrentSchemaVersion
                && sessionContract.IssuedAtUtc == registration.IssuedAtUtc
                && sessionContract.CanShutdownProcess == registration.CanShutdownProcess
                && sessionContract.ProcessId == currentProcess.Id
                && ContractLiteralCodec.Matches(sessionContract.EditorMode, DaemonEditorMode.Gui)
                && ContractLiteralCodec.Matches(sessionContract.EndpointTransportKind, registration.Endpoint.TransportKind)
                && string.Equals(sessionContract.EndpointAddress, registration.Endpoint.Address, StringComparison.Ordinal);
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
