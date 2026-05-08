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
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiSessionPersistenceTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Write_WhenSessionIsUserOwned_PersistsGuiSessionJson () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = CreateStorageRoot();
            try
            {
                var registration = await WriteSession(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));

                var contract = ReadSessionContract(storageRoot);
                Assert.That(contract.EditorMode, Is.EqualTo(DaemonEditorModeValues.Gui));
                Assert.That(contract.OwnerKind, Is.EqualTo(DaemonSessionOwnerKindValues.User));
                Assert.That(contract.CanShutdownProcess, Is.False);
                Assert.That(contract.ProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.OwnerProcessId, Is.EqualTo(Process.GetCurrentProcess().Id));
                Assert.That(contract.EndpointTransportKind, Is.EqualTo(IpcTransportKindValues.NamedPipe));
                Assert.That(contract.EndpointAddress, Is.EqualTo("ucli-gui-session-tests"));
                Assert.That(contract.SessionToken, Is.EqualTo(registration.SessionToken));
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
                await WriteSession(
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
                var registration = await WriteSession(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));
                var validator = new FileBackedSessionTokenValidator(registration.SessionPath);

                Assert.That(
                    await validator.Validate(registration.SessionToken, CancellationToken.None),
                    Is.True);

                UnityGuiSessionPersistence.Delete(registration);

                Assert.That(File.Exists(registration.SessionPath), Is.False);
                Assert.That(
                    await validator.Validate(registration.SessionToken, CancellationToken.None),
                    Is.False);
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
                var firstRegistration = await WriteSession(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));
                UnityGuiSessionPersistence.Delete(firstRegistration);

                var secondRegistration = await WriteSession(
                    storageRoot,
                    UnityGuiBootstrapSessionOptions.Create(null));

                Assert.That(secondRegistration.SessionToken, Is.Not.EqualTo(firstRegistration.SessionToken));
                Assert.That(File.Exists(secondRegistration.SessionPath), Is.True);
            }
            finally
            {
                DeleteDirectory(storageRoot);
            }
        });

        private static UniTask<UnityGuiSessionRegistration> WriteSession (
            string storageRoot,
        UnityGuiBootstrapSessionOptions sessionOptions)
        {
            return UnityGuiSessionPersistence.Write(
                    storageRoot,
                    "fingerprint",
                    new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-session-tests"),
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
