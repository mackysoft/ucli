using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

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
        private static readonly TimeSpan SessionLockAcquireTimeout = TimeSpan.FromSeconds(1);

        /// <summary> Prepares one GUI daemon session generation without publishing <c>session.json</c>. </summary>
        /// <param name="storageRoot"> The shared storage root path. </param>
        /// <param name="projectFingerprint"> The project fingerprint served by this GUI Editor. </param>
        /// <param name="endpoint"> The resolved daemon IPC endpoint. </param>
        /// <param name="sessionOptions"> The normalized session ownership options. </param>
        /// <param name="editorInstanceId"> The non-empty Editor process identity captured for this host generation. </param>
        /// <param name="sessionReplacementScope"> The scope of existing current-process GUI sessions that may be replaced. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by bootstrap lifecycle. </param>
        /// <returns> The prepared generation that retains exclusive publication ownership until disposed. </returns>
        public static async Task<PreparedSession> PrepareAsync (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            IpcEndpoint endpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            Guid editorInstanceId,
            UnityGuiSessionReplacementScope sessionReplacementScope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateArguments(
                storageRoot,
                projectFingerprint,
                endpoint,
                sessionOptions,
                editorInstanceId,
                sessionReplacementScope);

            var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, projectFingerprint);
            var sessionLockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(storageRoot, projectFingerprint);
            var sessionDirectoryPath = Path.GetDirectoryName(sessionPath)
                ?? throw new InvalidOperationException($"GUI session directory path could not be resolved: {sessionPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(sessionDirectoryPath);

            var sessionLock = await FileExclusiveLock.AcquireAsync(
                sessionLockPath,
                SessionLockAcquireTimeout,
                cancellationToken);
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                var currentProcessId = currentProcess.Id;
                var currentProcessStartedAtUtc = currentProcess.StartTime.ToUniversalTime();
                var replaceableSession = ReadExistingSessionForReplacement(
                    sessionPath,
                    projectFingerprint,
                    endpoint,
                    sessionOptions,
                    currentProcessId,
                    editorInstanceId,
                    sessionReplacementScope);
                if (replaceableSession != null)
                {
                    DeleteUnixEndpointResidue(replaceableSession, endpoint);
                }

                var issuedAtUtc = DateTimeOffset.UtcNow;
                var sessionGenerationId = Guid.NewGuid();
                var sessionToken = IpcSessionToken.CreateRandom();
                var sessionContract = new DaemonSessionJsonContract(
                    SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                    SessionGenerationId: sessionGenerationId,
                    SessionToken: sessionToken.GetEncodedValue(),
                    ProjectFingerprint: projectFingerprint,
                    IssuedAtUtc: issuedAtUtc,
                    EditorMode: DaemonEditorMode.Gui,
                    OwnerKind: sessionOptions.OwnerKind,
                    CanShutdownProcess: sessionOptions.CanShutdownProcess,
                    EndpointTransportKind: endpoint.TransportKind,
                    EndpointAddress: endpoint.Address,
                    ProcessId: currentProcessId,
                    ProcessStartedAtUtc: currentProcessStartedAtUtc,
                    OwnerProcessId: sessionOptions.OwnerProcessId,
                    EditorInstanceId: editorInstanceId);
                var registration = new UnityGuiSessionRegistration(
                    sessionPath,
                    sessionLockPath,
                    sessionGenerationId,
                    sessionToken,
                    projectFingerprint,
                    issuedAtUtc,
                    endpoint,
                    sessionOptions.CanShutdownProcess);
                var json = DaemonSessionJsonContractSerializer.Serialize(sessionContract) + Environment.NewLine;
                return new PreparedSession(registration, json, sessionLock);
            }
            catch
            {
                sessionLock.Dispose();
                throw;
            }
        }

        /// <summary> Publishes one prepared GUI session after its endpoint has begun listening. </summary>
        /// <param name="preparedSession"> The prepared generation that owns the session publication lock. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by bootstrap lifecycle. </param>
        /// <returns> The persisted session registration. </returns>
        public static async Task<UnityGuiSessionRegistration> PublishAsync (
            PreparedSession preparedSession,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (preparedSession == null)
            {
                throw new ArgumentNullException(nameof(preparedSession));
            }

            preparedSession.ThrowIfCannotPublish();
            try
            {
                await FileUtilities.WriteAllTextAtomicallyAsync(
                        preparedSession.SessionPath,
                        preparedSession.SessionJson,
                        cancellationToken)
                    .ConfigureAwait(false);
                FileSystemAccessBoundary.EnsureSecureFile(preparedSession.SessionPath);
                preparedSession.MarkPublished();
                return preparedSession.Registration;
            }
            catch
            {
                DeleteOwnedSessionWithoutLock(preparedSession.Registration, deleteEndpoint: false);
                throw;
            }
        }

        /// <summary> Deletes one GUI daemon session registration and endpoint residue. </summary>
        /// <param name="registration"> The session registration to delete. </param>
        public static void Delete (UnityGuiSessionRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException(nameof(registration));
            }

            using var sessionLock = FileExclusiveLock.Acquire(
                registration.SessionLockPath,
                SessionLockAcquireTimeout,
                CancellationToken.None);
            DeleteOwnedSessionWithoutLock(registration, deleteEndpoint: true);
        }

        /// <summary> Deletes a prepared generation while its caller still owns the publication lease. </summary>
        /// <param name="preparedSession"> The prepared generation whose publication task has terminated. </param>
        internal static void DeleteOwnedSessionBeforeLeaseRelease (PreparedSession preparedSession)
        {
            if (preparedSession == null)
            {
                throw new ArgumentNullException(nameof(preparedSession));
            }

            preparedSession.ThrowIfDisposed();
            DeleteOwnedSessionWithoutLock(preparedSession.Registration, deleteEndpoint: true);
        }

        private static void ValidateArguments (
            string storageRoot,
            ProjectFingerprint projectFingerprint,
            IpcEndpoint endpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            Guid editorInstanceId,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            ValidateSessionReplacementScope(sessionReplacementScope);
            if (string.IsNullOrWhiteSpace(storageRoot))
            {
                throw new ArgumentException("storageRoot must not be empty.", nameof(storageRoot));
            }

            if (projectFingerprint == null)
            {
                throw new ArgumentNullException(nameof(projectFingerprint));
            }

            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (sessionOptions == null)
            {
                throw new ArgumentNullException(nameof(sessionOptions));
            }

            if (editorInstanceId == Guid.Empty)
            {
                throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
            }
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

        private static DaemonSessionJsonContract ReadExistingSessionForReplacement (
            string sessionPath,
            ProjectFingerprint projectFingerprint,
            IpcEndpoint expectedEndpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            int currentProcessId,
            Guid currentEditorInstanceId,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            var json = FileUtilities.ReadAllTextOrNull(sessionPath);
            if (json == null)
            {
                return null;
            }

            DaemonSessionJsonContract sessionContract;
            try
            {
                sessionContract = DaemonSessionJsonContractSerializer.Deserialize(json);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or JsonException)
            {
                throw new InvalidOperationException($"GUI session already exists and cannot be replaced: {sessionPath}", exception);
            }

            if (sessionContract == null
                || !MatchesCurrentProcessGuiSession(
                    sessionContract,
                    projectFingerprint,
                    expectedEndpoint,
                    sessionOptions,
                    currentProcessId,
                    currentEditorInstanceId,
                    sessionReplacementScope))
            {
                throw new InvalidOperationException($"GUI session already exists and cannot be replaced: {sessionPath}");
            }

            return sessionContract;
        }

        private static bool MatchesCurrentProcessGuiSession (
            DaemonSessionJsonContract sessionContract,
            ProjectFingerprint projectFingerprint,
            IpcEndpoint expectedEndpoint,
            UnityGuiBootstrapSessionOptions sessionOptions,
            int currentProcessId,
            Guid currentEditorInstanceId,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            if (sessionContract.SchemaVersion != DaemonSessionStorageContract.CurrentSchemaVersion
                || sessionContract.SessionGenerationId == Guid.Empty
                || sessionContract.ProjectFingerprint != projectFingerprint
                || sessionContract.EditorMode != DaemonEditorMode.Gui
                || sessionContract.ProcessId != currentProcessId
                || sessionContract.EditorInstanceId != currentEditorInstanceId)
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

            return sessionContract.OwnerKind == sessionOptions.OwnerKind
                && sessionContract.CanShutdownProcess == sessionOptions.CanShutdownProcess
                && sessionContract.EndpointTransportKind == expectedEndpoint.TransportKind
                && string.Equals(sessionContract.EndpointAddress, expectedEndpoint.Address, StringComparison.Ordinal)
                && sessionContract.OwnerProcessId == sessionOptions.OwnerProcessId;
        }

        private static void DeleteUnixEndpointResidue (
            DaemonSessionJsonContract sessionContract,
            IpcEndpoint expectedEndpoint)
        {
            if (expectedEndpoint.TransportKind != IpcTransportKind.UnixDomainSocket
                || sessionContract.EndpointTransportKind != expectedEndpoint.TransportKind
                || !string.Equals(sessionContract.EndpointAddress, expectedEndpoint.Address, StringComparison.Ordinal))
            {
                return;
            }

            FileUtilities.DeleteIfExists(expectedEndpoint.Address);
        }

        private static bool MatchesRegistration (
            UnityGuiSessionRegistration registration,
            DaemonSessionJsonContract sessionContract)
        {
            return sessionContract.SchemaVersion == DaemonSessionStorageContract.CurrentSchemaVersion
                && sessionContract.SessionGenerationId == registration.SessionGenerationId;
        }

        private static void DeleteOwnedSessionWithoutLock (
            UnityGuiSessionRegistration registration,
            bool deleteEndpoint)
        {
            var json = FileUtilities.ReadAllTextOrNull(registration.SessionPath);
            if (json == null)
            {
                return;
            }

            DaemonSessionJsonContract sessionContract;
            try
            {
                sessionContract = DaemonSessionJsonContractSerializer.Deserialize(json);
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException)
            {
                return;
            }

            if (sessionContract == null || !MatchesRegistration(registration, sessionContract))
            {
                return;
            }

            FileUtilities.DeleteIfExists(registration.SessionPath);
            if (!deleteEndpoint || registration.Endpoint.TransportKind != IpcTransportKind.UnixDomainSocket)
            {
                return;
            }

            FileUtilities.DeleteIfExists(registration.Endpoint.Address);
        }

        /// <summary> Represents one unpublished GUI session generation and its exclusive publication lease. </summary>
        internal sealed class PreparedSession : IDisposable
        {
            private readonly FileExclusiveLock sessionLock;

            private bool disposed;

            private bool published;

            public PreparedSession (
                UnityGuiSessionRegistration registration,
                string sessionJson,
                FileExclusiveLock sessionLock)
            {
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                SessionJson = sessionJson ?? throw new ArgumentNullException(nameof(sessionJson));
                this.sessionLock = sessionLock ?? throw new ArgumentNullException(nameof(sessionLock));
            }

            public string SessionPath => Registration.SessionPath;

            internal UnityGuiSessionRegistration Registration { get; }

            internal string SessionJson { get; }

            public void Dispose ()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                sessionLock.Dispose();
            }

            internal void ThrowIfCannotPublish ()
            {
                ThrowIfDisposed();

                if (published)
                {
                    throw new InvalidOperationException("Prepared GUI session has already been published.");
                }
            }

            internal void MarkPublished ()
            {
                ThrowIfCannotPublish();
                published = true;
            }

            internal void ThrowIfDisposed ()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(PreparedSession));
                }
            }
        }
    }
}
