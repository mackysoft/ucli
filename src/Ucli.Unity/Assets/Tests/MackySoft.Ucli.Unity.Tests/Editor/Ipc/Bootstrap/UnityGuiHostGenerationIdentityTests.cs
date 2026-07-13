using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiHostGenerationIdentityTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("gui-host-generation-fingerprint");

        private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid OtherEditorInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Sha256Digest RequestPayloadHash = Sha256Digest.Parse(
            "cda34040abc54e9b351b66c6ecbc9708cf2c70996b0805553b3854bdce80d94b");

        [TearDown]
        public void TearDown ()
        {
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(null);
        }

        [Test]
        [Category("Size.Small")]
        public async Task PersistedArtifacts_UseCapturedEditorInstanceId ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                $"ucli-gui-host-generation-identity-tests-{Guid.NewGuid():N}");
            UnityGuiSessionRegistration registration = null;

            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests(EditorInstanceId.ToString("N"));
                var capturedEditorInstanceId = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests(OtherEditorInstanceId.ToString("N"));

                using (var preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                           storageRoot,
                           ProjectFingerprint,
                           new IpcEndpoint(IpcTransportKind.NamedPipe, "ucli-gui-host-generation-identity"),
                           UnityGuiBootstrapSessionOptions.Create(null),
                           capturedEditorInstanceId,
                           UnityGuiSessionReplacementScope.EquivalentCurrentProcessSession,
                           CancellationToken.None))
                {
                    registration = await UnityGuiSessionPersistence.PublishAsync(
                        preparedSession,
                        CancellationToken.None);
                }

                var lifecyclePersistence = new UnityLifecycleSidecarPersistence(
                    storageRoot,
                    ProjectFingerprint,
                    capturedEditorInstanceId,
                    "1.2.3-tests");
                await lifecyclePersistence.WriteAsync(
                    new UnityEditorLifecycleSnapshot(
                        DaemonEditorMode.Gui,
                        IpcEditorLifecycleStateCodec.Ready,
                        null,
                        IpcCompileStateCodec.Ready,
                        "compile-generation",
                        "domain-reload-generation",
                        CanAcceptExecutionRequests: true,
                        ObservedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero)),
                    CancellationToken.None);

                var operationStore = FileRecoverableIpcOperationStore.Create(
                    new IpcProjectIdentity(storageRoot, ProjectFingerprint, "6000.1.4f1"),
                    capturedEditorInstanceId);
                var requestId = Guid.Parse("7b6d4c17-1b8e-4f28-a2e6-123456789abc");
                var operationWriteResult = await operationStore.WritePendingAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
                    IpcPayloadCodec.SerializeToElement(new { before = "snapshot" }),
                    CancellationToken.None);
                var operationReadResult = await operationStore.ReadAsync(
                    UnityIpcMethod.PlayEnter,
                    requestId,
                    RequestPayloadHash,
                    CancellationToken.None);

                var sessionContract = DaemonSessionJsonContractSerializer.Deserialize(
                    File.ReadAllText(UcliStoragePathResolver.ResolveSessionPath(
                        storageRoot,
                        ProjectFingerprint)));
                var lifecycleContract = DaemonLifecycleJsonContractSerializer.Deserialize(
                    File.ReadAllText(UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                        storageRoot,
                        ProjectFingerprint)));
                var expectedPersistedId = EditorInstanceId.ToString("N");

                Assert.That(operationWriteResult.IsSuccess, Is.True, operationWriteResult.ErrorMessage);
                Assert.That(operationReadResult.IsSuccess, Is.True, operationReadResult.ErrorMessage);
                Assert.That(sessionContract, Is.Not.Null);
                Assert.That(lifecycleContract, Is.Not.Null);
                Assert.That(operationReadResult.Record, Is.Not.Null);
                Assert.That(sessionContract.EditorInstanceId, Is.EqualTo(expectedPersistedId));
                Assert.That(lifecycleContract.EditorInstanceId, Is.EqualTo(expectedPersistedId));
                Assert.That(operationReadResult.Record.HostEditorInstanceId, Is.EqualTo(expectedPersistedId));
            }
            finally
            {
                if (registration != null)
                {
                    UnityGuiSessionPersistence.Delete(registration);
                }

                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }
            }
        }
    }
}
