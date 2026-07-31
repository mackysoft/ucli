using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityGuiHostGenerationIdentityTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("gui-host-generation-fingerprint");

        private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid OtherEditorInstanceId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid SidecarGenerationId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        [TearDown]
        public void TearDown ()
        {
            UnityEditorSessionStateStore.SetEditorInstanceIdForTests(null);
        }

        [Test]
        [Category("Size.Small")]
        public async Task PersistedSessionAndLifecycle_UseCapturedEditorInstanceId ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                $"ucli-gui-host-generation-identity-tests-{Guid.NewGuid():N}");
            var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
            UnityGuiSessionRegistration registration = null;

            try
            {
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests(EditorInstanceId.ToString("N"));
                var capturedEditorInstanceId = UnityEditorSessionStateStore.GetOrCreateEditorInstanceId();
                UnityEditorSessionStateStore.SetEditorInstanceIdForTests(OtherEditorInstanceId.ToString("N"));

                using (var preparedSession = await UnityGuiSessionPersistence.PrepareAsync(
                           guardedStorageRoot,
                           ProjectFingerprint,
                           UnityIpcEndpointBinding.Create(
                               new IpcEndpoint(
                                   IpcTransportKind.NamedPipe,
                                   "ucli-gui-host-generation-identity")),
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
                    guardedStorageRoot,
                    ProjectFingerprint,
                    capturedEditorInstanceId,
                    SidecarGenerationId,
                    "1.2.3-tests");
                await lifecyclePersistence.WriteAsync(
                    new UnityEditorRuntimeObservation(
                        state: new UnityEditorStateSnapshot(
                            editorMode: UnityEditorMode.Gui,
                            lifecycleState: UnityEditorLifecycleState.Ready,
                            compileState: UnityEditorCompileState.Ready,
                            generations: new UnityEditorGenerationSnapshot(1, 2, 0, 0),
                            playMode: new UnityEditorPlayModeSnapshot(
                                UnityEditorPlayModeState.Stopped,
                                UnityEditorPlayModeTransition.None,
                                IsPlaying: false,
                                IsPlayingOrWillChangePlaymode: false)),
                        observedAtUtc: new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero)),
                    null,
                    CancellationToken.None);

                var sessionContract = DaemonSessionJsonContractSerializer.Deserialize(
                    File.ReadAllText(UcliStoragePathResolver.ResolveSessionPath(
                        guardedStorageRoot,
                        ProjectFingerprint).Value));
                var lifecycleContract = DaemonLifecycleJsonContractSerializer.Deserialize(
                    File.ReadAllText(UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                        guardedStorageRoot,
                        ProjectFingerprint).Value));
                Assert.That(sessionContract, Is.Not.Null);
                Assert.That(lifecycleContract, Is.Not.Null);
                Assert.That(sessionContract.EditorInstanceId, Is.EqualTo(EditorInstanceId));
                Assert.That(lifecycleContract.EditorInstanceId, Is.EqualTo(EditorInstanceId));
                Assert.That(lifecycleContract.SidecarGenerationId, Is.EqualTo(SidecarGenerationId));
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
