using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
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
        [TearDown]
        public void TearDown ()
        {
            UnityEditorProcessIdentity.SetEditorInstanceIdForTests(null);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Write_WhenSessionIsUserOwned_PersistsGuiSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-user-owned");
                await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SchemaVersion, Is.EqualTo(DaemonSessionStorageContract.CurrentSchemaVersion));
                Assert.That(contract.EditorMode, Is.EqualTo(DaemonEditorModeValues.Gui));
                Assert.That(contract.OwnerKind, Is.EqualTo(DaemonSessionOwnerKindValues.User));
                Assert.That(contract.CanShutdownProcess, Is.False);
                Assert.That(contract.ProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.ProcessStartedAtUtc, Is.Not.Null);
                Assert.That(
                    Math.Abs((contract.ProcessStartedAtUtc!.Value - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds),
                    Is.LessThanOrEqualTo(2));
                Assert.That(contract.EditorInstanceId, Is.EqualTo("editor-instance-user-owned"));
                Assert.That(contract.OwnerProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.EndpointTransportKind, Is.EqualTo(IpcTransportKindValues.NamedPipe));
                Assert.That(contract.EndpointAddress, Is.EqualTo("ucli-gui-session-tests"));
                Assert.That(contract.SessionToken, Is.Not.Null.And.Not.Empty);
                Assert.That(contract.ProjectFingerprint, Is.EqualTo("fingerprint"));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Write_WhenSessionIsCliOwned_PersistsCliOwnershipValues () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-cli-owned");
                await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(new IpcGuiBootstrapArguments(
                        OwnerProcessId: 123,
                        CanShutdownProcess: true)));

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.EditorMode, Is.EqualTo(DaemonEditorModeValues.Gui));
                Assert.That(contract.OwnerKind, Is.EqualTo(DaemonSessionOwnerKindValues.Cli));
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
        public IEnumerator Delete_WhenSessionExists_InvalidatesFileBackedToken () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-token-validation");
                var registration = await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));
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
        public IEnumerator Write_WhenCurrentProcessGuiSessionAlreadyExists_ReplacesSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-replace-current");
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
                        EditorMode: DaemonEditorModeValues.Gui,
                        OwnerKind: DaemonSessionOwnerKindValues.User,
                        CanShutdownProcess: false,
                        EndpointTransportKind: IpcTransportKindValues.NamedPipe,
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime().AddSeconds(30),
                        OwnerProcessId: currentProcess.Id)
                    {
                        EditorInstanceId = "editor-instance-replace-current",
                    });

                await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.Not.EqualTo("existing-current-process-token"));
                Assert.That(contract.EditorMode, Is.EqualTo(DaemonEditorModeValues.Gui));
                Assert.That(contract.OwnerKind, Is.EqualTo(DaemonSessionOwnerKindValues.User));
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
        public IEnumerator Write_WhenCurrentProcessGuiSessionWithoutEditorInstanceIdAlreadyExists_ReplacesSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-replace-current-missing-id");
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
                        EditorMode: DaemonEditorModeValues.Gui,
                        OwnerKind: DaemonSessionOwnerKindValues.User,
                        CanShutdownProcess: false,
                        EndpointTransportKind: IpcTransportKindValues.NamedPipe,
                        EndpointAddress: "ucli-gui-session-tests",
                        ProcessId: currentProcess.Id,
                        ProcessStartedAtUtc: currentProcess.StartTime.ToUniversalTime(),
                        OwnerProcessId: currentProcess.Id));

                await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));

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
        public IEnumerator Write_WhenCurrentProcessGuiSessionHasDifferentEditorInstanceId_DoesNotReplaceSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-current");
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
                        EditorMode: DaemonEditorModeValues.Gui,
                        OwnerKind: DaemonSessionOwnerKindValues.User,
                        CanShutdownProcess: false,
                        EndpointTransportKind: IpcTransportKindValues.NamedPipe,
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
                    await WriteSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null));
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
        public IEnumerator Write_WhenCurrentProcessGuiSessionUsesUnexpectedUnixEndpoint_DoesNotDeleteEndpointResidue () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-unexpected-endpoint");
                var sessionPath = UcliStoragePathResolver.ResolveSessionPath(storageRoot, "fingerprint");
                var sessionDirectoryPath = Path.GetDirectoryName(sessionPath);
                Assert.That(sessionDirectoryPath, Is.Not.Null);
                Directory.CreateDirectory(sessionDirectoryPath!);
                using var currentProcess = Process.GetCurrentProcess();
                var unexpectedEndpointPath = Path.Combine(storageRoot, "unexpected.sock");
                var expectedEndpointPath = Path.Combine(storageRoot, "expected.sock");
                File.WriteAllText(unexpectedEndpointPath, "socket residue placeholder");
                WriteSessionContract(
                    sessionPath,
                    new DaemonSessionJsonContract(
                        SchemaVersion: DaemonSessionStorageContract.CurrentSchemaVersion,
                        SessionToken: "existing-current-process-token",
                        ProjectFingerprint: "fingerprint",
                        IssuedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                        EditorMode: DaemonEditorModeValues.Gui,
                        OwnerKind: DaemonSessionOwnerKindValues.User,
                        CanShutdownProcess: false,
                        EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
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
                    await WriteSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null),
                        new IpcEndpoint(IpcTransportKind.UnixDomainSocket, expectedEndpointPath));
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
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Write_WhenSessionAlreadyExists_DoesNotOverwriteSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-already-exists");
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
                        EditorMode: DaemonEditorModeValues.Batchmode,
                        OwnerKind: DaemonSessionOwnerKindValues.Cli,
                        CanShutdownProcess: true,
                        EndpointTransportKind: IpcTransportKindValues.NamedPipe,
                        EndpointAddress: "ucli-existing-session",
                        ProcessId: 123,
                        ProcessStartedAtUtc: DateTimeOffset.UtcNow,
                        OwnerProcessId: 123));

                InvalidOperationException exception = null;
                try
                {
                    await WriteSessionAsync(
                        storageRoot,
                        UnityGuiBootstrapSessionOptions.Create(null));
                }
                catch (InvalidOperationException caughtException)
                {
                    exception = caughtException;
                }

                Assert.That(exception, Is.Not.Null);
                Assert.That(exception.Message, Does.Contain("GUI session already exists"));
                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.SessionToken, Is.EqualTo("existing-session-token"));
                Assert.That(contract.EditorMode, Is.EqualTo(DaemonEditorModeValues.Batchmode));
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
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-delete-replaced");
                var endpointResiduePath = Path.Combine(storageRoot, "ipc.sock");
                var registration = await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null),
                    new IpcEndpoint(IpcTransportKind.UnixDomainSocket, endpointResiduePath));
                File.WriteAllText(endpointResiduePath, "socket residue placeholder");
                var originalContract = ReadSessionContract(storageRoot);
                var replacementContract = originalContract with
                {
                    SessionToken = "replacement-session-token",
                    IssuedAtUtc = originalContract.IssuedAtUtc.AddSeconds(1),
                    EditorMode = DaemonEditorModeValues.Batchmode,
                    OwnerKind = DaemonSessionOwnerKindValues.Cli,
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
                Assert.That(contract.EditorMode, Is.EqualTo(DaemonEditorModeValues.Batchmode));
                Assert.That(contract.ProcessId, Is.EqualTo(456));
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Write_AfterDelete_ReRegistersWithNewToken () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                UnityEditorProcessIdentity.SetEditorInstanceIdForTests("editor-instance-reregister");
                var firstRegistration = await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));
                var firstSessionToken = ReadSessionContract(storageRoot).SessionToken;
                UnityGuiSessionPersistence.Delete(firstRegistration);

                var secondRegistration = await WriteSessionAsync(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));
                var secondSessionToken = ReadSessionContract(storageRoot).SessionToken;

                Assert.That(secondSessionToken, Is.Not.EqualTo(firstSessionToken));
                Assert.That(File.Exists(secondRegistration.SessionPath), Is.True);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        private static UniTask<UnityGuiSessionRegistration> WriteSessionAsync (
            string storageRoot,
            UnityGuiBootstrapSessionOptions sessionOptions,
            IpcEndpoint endpoint = null)
        {
            return UnityGuiSessionPersistence.WriteAsync(
                    storageRoot,
                    "fingerprint",
                    endpoint ?? new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-session-tests"),
                    sessionOptions,
                    CancellationToken.None)
                .AsUniTask();
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

        private static void DeleteDirectory (string storageRoot)
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }
}
