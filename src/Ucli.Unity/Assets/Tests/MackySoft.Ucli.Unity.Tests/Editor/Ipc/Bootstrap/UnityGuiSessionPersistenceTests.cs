using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiSessionPersistenceTests
    {
        private const string FirstCanonicalSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        private const string SecondCanonicalSessionToken = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAE";

        [TearDown]
        public void TearDown ()
        {
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(null);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator GuiSupervisorDelete_WhenExpectedTokenDoesNotOwnCurrentManifest_PreservesSuccessorManifest () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            const string ProjectFingerprint = "fingerprint";
            var successorToken = ParseSessionToken(FirstCanonicalSessionToken);
            var retiredToken = ParseSessionToken(SecondCanonicalSessionToken);
            try
            {
                using (var publicationLease = await UnityGuiSupervisorPersistence.AcquirePublicationLeaseAsync(
                           storageRoot,
                           ProjectFingerprint,
                           CancellationToken.None))
                {
                    await publicationLease.PublishAsync(
                        CreateDefaultEndpoint(),
                        successorToken,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None);
                }

                var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
                    storageRoot,
                    ProjectFingerprint);

                UnityGuiSupervisorPersistence.Delete(
                    storageRoot,
                    ProjectFingerprint,
                    expectedSessionToken: retiredToken);

                Assert.That(File.Exists(manifestPath), Is.True);

                UnityGuiSupervisorPersistence.Delete(
                    storageRoot,
                    ProjectFingerprint,
                    expectedSessionToken: successorToken);
                Assert.That(File.Exists(manifestPath), Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenSessionIsUserOwned_PersistsGuiSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-user-owned");
                var registration = await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SchemaVersion, Is.EqualTo(DaemonSessionStorageContract.CurrentSchemaVersion));
                Assert.That(contract.EditorMode, Is.EqualTo("gui"));
                Assert.That(contract.OwnerKind, Is.EqualTo("user"));
                Assert.That(contract.CanShutdownProcess, Is.False);
                Assert.That(contract.ProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.ProcessStartedAtUtc, Is.Not.Null);
                Assert.That(
                    Math.Abs((contract.ProcessStartedAtUtc!.Value - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds),
                    Is.LessThanOrEqualTo(2));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-user-owned"));
                Assert.That(contract.OwnerProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.EndpointTransportKind, Is.EqualTo("namedPipe"));
                Assert.That(contract.EndpointAddress, Is.EqualTo("ucli-gui-session-tests"));
                Assert.That(contract.SessionToken, Is.Not.Null.And.Not.Empty);
                Assert.That(IpcSessionToken.IsValidEncodedValue(contract.SessionToken), Is.True);
                Assert.That(registration.SessionToken.Matches(contract.SessionToken), Is.True);
                Assert.That(contract.ProjectFingerprint, Is.EqualTo("fingerprint"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenSessionIsCliOwned_PersistsCliOwnershipValues () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-cli-owned");
                await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(new IpcGuiBootstrapArguments(
                        OwnerProcessId: 123,
                        CanShutdownProcess: true)),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.EditorMode, Is.EqualTo("gui"));
                Assert.That(contract.OwnerKind, Is.EqualTo("cli"));
                Assert.That(contract.CanShutdownProcess, Is.True);
                Assert.That(contract.OwnerProcessId, Is.EqualTo(123));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenReplacingCurrentProcessGuiSessionWithDifferentOwnership_ReplacesSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-replace-stale");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-cli-owned-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "cli",
                        CanShutdownProcess: true,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: 123)
                    {
                        EditorInstanceId = "editor-instance-replace-stale",
                    });

                await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.AnyCurrentProcessSession);

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.Not.EqualTo("existing-cli-owned-token"));
                Assert.That(contract.OwnerKind, Is.EqualTo("user"));
                Assert.That(contract.CanShutdownProcess, Is.False);
                Assert.That(contract.OwnerProcessId, Is.EqualTo(currentProcess.Id));
                Assert.That(contract.ProcessId, Is.EqualTo(currentProcess.Id));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-replace-stale"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenCurrentProcessGuiSessionHasDifferentOwnershipWithoutReplace_DoesNotReplaceSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-ownership-mismatch");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-cli-owned-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "cli",
                        CanShutdownProcess: true,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: 123)
                    {
                        EditorInstanceId = "editor-instance-ownership-mismatch",
                    });

                InvalidOperationException exception = null;
                try
                {
                    await PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        CreateDefaultEndpoint(),
                        UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("GUI session already exists"));
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-cli-owned-token"));
                Assert.That(contract.OwnerKind, Is.EqualTo("cli"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenReplacingCurrentProcessGuiSessionHasDifferentEditorInstanceId_DoesNotReplaceSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-current");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-other-editor-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "cli",
                        CanShutdownProcess: true,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: 123)
                    {
                        EditorInstanceId = "editor-instance-other",
                    });

                InvalidOperationException exception = null;
                try
                {
                    await PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        CreateDefaultEndpoint(),
                        UnityGuiSessionReplacementScope.AnyCurrentProcessSession);
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("GUI session already exists"));
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-other-editor-token"));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-other"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_WhenSessionExists_InvalidatesFileBackedToken () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-token-validation");
                var registration = await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                var sessionToken = ReadSessionContract(storageRoot).SessionToken;
                var validator = new FileBackedSessionTokenValidator(registration.SessionPath);

                Assert.That(
                    await validator.ValidateAsync(sessionToken, CancellationToken.None),
                    Is.True);
                Assert.That(
                    await validator.ValidateAsync("wrong-session-token", CancellationToken.None),
                    Is.False);

                UnityGuiSessionPersistence.Delete(registration);

                Assert.That(File.Exists(registration.SessionPath), Is.False);
                Assert.That(
                    await validator.ValidateAsync(sessionToken, CancellationToken.None),
                    Is.False);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenCurrentProcessGuiSessionAlreadyExists_ReplacesSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-replace-current");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-current-process-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "user",
                        CanShutdownProcess: false,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime().AddSeconds(30),
                        OwnerProcessId: currentProcess.Id)
                    {
                        EditorInstanceId = "editor-instance-replace-current",
                    });

                await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.Not.EqualTo("existing-current-process-token"));
                Assert.That(contract.EditorMode, Is.EqualTo("gui"));
                Assert.That(contract.OwnerKind, Is.EqualTo("user"));
                Assert.That(contract.ProcessId, Is.EqualTo(currentProcess.Id));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-replace-current"));
                Assert.That(contract.EndpointAddress, Is.EqualTo("ucli-gui-session-tests"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenReplaceableSessionIsOpenForReadOnWindows_RetriesAtomicReplacement () => UniTask.ToCoroutine(async () =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-replace-open-session");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-open-session-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "user",
                        CanShutdownProcess: false,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: currentProcess.Id)
                    {
                        EditorInstanceId = "editor-instance-replace-open-session",
                    });

                System.Threading.Tasks.Task<UnityGuiSessionRegistration> replacementTask;
                using (new FileStream(sessionPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    replacementTask = PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        CreateDefaultEndpoint(),
                        UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession)
                        .AsTask();
                    Assert.That(replacementTask.IsCompleted, Is.False);
                }

                await replacementTask;

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.Not.EqualTo("existing-open-session-token"));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-replace-open-session"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenCurrentProcessGuiSessionWithoutEditorInstanceIdAlreadyExists_ReplacesSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-replace-current-missing-id");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-current-process-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "user",
                        CanShutdownProcess: false,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: currentProcess.Id));

                await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.Not.EqualTo("existing-current-process-token"));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-replace-current-missing-id"));
                Assert.That(contract.ProcessId, Is.EqualTo(currentProcess.Id));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenCurrentProcessGuiSessionWithoutEditorInstanceIdHasDifferentStartTime_DoesNotReplaceSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-legacy-start-time-mismatch");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-legacy-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "user",
                        CanShutdownProcess: false,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime().AddSeconds(30),
                        OwnerProcessId: currentProcess.Id));

                InvalidOperationException exception = null;
                try
                {
                    await PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        CreateDefaultEndpoint(),
                        UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("GUI session already exists"));
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-legacy-token"));
                Assert.That(contract.EditorInstanceId, Is.Null);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenCurrentProcessGuiSessionHasDifferentEditorInstanceId_DoesNotReplaceSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-current");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-different-editor-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "user",
                        CanShutdownProcess: false,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: currentProcess.Id)
                    {
                        EditorInstanceId = "editor-instance-other",
                    });

                InvalidOperationException exception = null;
                try
                {
                    await PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        CreateDefaultEndpoint(),
                        UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("GUI session already exists"));
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-different-editor-token"));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-other"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenCurrentProcessGuiSessionUsesUnexpectedUnixEndpoint_DoesNotDeleteEndpointResidue () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var unexpectedEndpointPath = CreateShortUnixSocketPath();
            var expectedEndpointPath = CreateShortUnixSocketPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-unexpected-endpoint");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                File.WriteAllText(unexpectedEndpointPath, "socket residue placeholder");
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-current-process-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: "gui",
                        OwnerKind: "user",
                        CanShutdownProcess: false,
                        EndpointTransportKind: "unixDomainSocket",
                        EndpointAddress: unexpectedEndpointPath,
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: currentProcess.Id)
                    {
                        EditorInstanceId = "editor-instance-unexpected-endpoint",
                    });

                InvalidOperationException exception = null;
                try
                {
                    await PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        new IpcEndpoint(IpcTransportKind.UnixDomainSocket, expectedEndpointPath),
                        UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(File.Exists(unexpectedEndpointPath), Is.True);
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-current-process-token"));
                Assert.That(contract.EndpointAddress, Is.EqualTo(unexpectedEndpointPath));
            }
            finally
            {
                DeleteFile(unexpectedEndpointPath);
                DeleteFile(expectedEndpointPath);
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_WhenSessionAlreadyExists_DoesNotOverwriteSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-already-exists");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-session-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow,
                        EditorMode: "batchmode",
                        OwnerKind: "cli",
                        CanShutdownProcess: true,
                        EndpointTransportKind: "namedPipe",
                        EndpointAddress: "ucli-existing-session",
                        ProcessId: 123,
                        ProcessStartedAtUtc: DateTimeOffset.UtcNow,
                        OwnerProcessId: 123));

                InvalidOperationException exception = null;
                try
                {
                    await PrepareAndPublishSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        CreateDefaultEndpoint(),
                        UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("GUI session already exists"));
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-session-token"));
                Assert.That(contract.EditorMode, Is.EqualTo("batchmode"));
                Assert.That(contract.EndpointAddress, Is.EqualTo("ucli-existing-session"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Delete_WhenSessionWasReplaced_LeavesCurrentSessionAndEndpointResidue () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            var endpointResiduePath = CreateShortUnixSocketPath();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-delete-replaced");
                var registration = await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    new IpcEndpoint(IpcTransportKind.UnixDomainSocket, endpointResiduePath),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                File.WriteAllText(endpointResiduePath, "socket residue placeholder");
                var originalContract = ReadSessionContract(storageRoot);
                var replacementContract = originalContract with
                {
                    SessionToken = "replacement-session-token",
                    IssuedAtUtc = originalContract.IssuedAtUtc.AddSeconds(1),
                    EditorMode = "batchmode",
                    OwnerKind = "cli",
                    CanShutdownProcess = true,
                    ProcessId = 456,
                    OwnerProcessId = 456,
                };
                WriteSessionContract(registration.SessionPath, replacementContract);

                UnityGuiSessionPersistence.Delete(registration);

                Assert.That(File.Exists(registration.SessionPath), Is.True);
                Assert.That(File.Exists(endpointResiduePath), Is.True);
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("replacement-session-token"));
                Assert.That(contract.EditorMode, Is.EqualTo("batchmode"));
                Assert.That(contract.ProcessId, Is.EqualTo(456));
            }
            finally
            {
                DeleteFile(endpointResiduePath);
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PrepareAndPublish_AfterDelete_ReRegistersWithNewToken () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests("editor-instance-reregister");
                var firstRegistration = await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                var firstSessionToken = ReadSessionContract(storageRoot).SessionToken;
                UnityGuiSessionPersistence.Delete(firstRegistration);

                var secondRegistration = await PrepareAndPublishSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    CreateDefaultEndpoint(),
                    UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession);
                var secondSessionToken = ReadSessionContract(storageRoot).SessionToken;

                Assert.That(secondSessionToken, Is.Not.EqualTo(firstSessionToken));
                Assert.That(File.Exists(secondRegistration.SessionPath), Is.True);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        private static async UniTask<UnityGuiSessionRegistration> PrepareAndPublishSessionAsync (
            string storageRoot,
            UnityGuiBootstrapSessionOptions sessionOptions,
            IpcEndpoint endpoint,
            UnityGuiSessionReplacementScope sessionReplacementScope)
        {
            using var preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                storageRoot,
                "fingerprint",
                endpoint,
                sessionOptions,
                sessionReplacementScope: sessionReplacementScope,
                cancellationToken: CancellationToken.None);
            return await UnityGuiSessionPersistence.PublishAsync(
                preparedSession,
                CancellationToken.None);
        }

        private static IpcEndpoint CreateDefaultEndpoint ()
        {
            return new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-session-tests");
        }

        private static DaemonSessionJsonContract ReadSessionContract (string storageRoot)
        {
            var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
            var json = File.ReadAllText(sessionPath);
            var contract = DaemonSessionJsonContractSerializer.Deserialize(json);
            Assert.That(contract, Is.Not.Null);
            return contract!;
        }

        private static void WriteSessionContract (
            string sessionPath,
            DaemonSessionJsonContract contract)
        {
            File.WriteAllText(
                sessionPath,
                DaemonSessionJsonContractSerializer.Serialize(contract) + Environment.NewLine);
        }

        private static string CreateStorageRoot ()
        {
            return Path.Combine(Path.GetTempPath(), $"ucli-gui-session-tests-{Guid.NewGuid():N}");
        }

        private static string CreateShortUnixSocketPath ()
        {
            return $"/tmp/ucli-{Guid.NewGuid():N}.sock";
        }

        private static IpcSessionToken ParseSessionToken (string value)
        {
            Assert.That(IpcSessionToken.TryParse(value, out var sessionToken), Is.True);
            return sessionToken!;
        }

        private static void DeleteDirectory (string storageRoot)
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }

        private static void DeleteFile (string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
