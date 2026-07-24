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

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityLifecycleSidecarPersistenceTests
    {
        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("lifecycle-sidecar-fingerprint");

        private static readonly ProjectFingerprint OwnershipProjectFingerprint =
            ProjectFingerprintTestFactory.Create("lifecycle-sidecar-ownership-fingerprint");

        private static readonly Guid EditorInstanceId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid PredecessorSidecarGenerationId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid SuccessorSidecarGenerationId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid SessionGenerationId =
            Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenEditorInstanceIdIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new UnityLifecycleSidecarPersistence(
                AbsolutePath.Parse(Path.GetTempPath()),
                ProjectFingerprint,
                Guid.Empty,
                PredecessorSidecarGenerationId,
                "1.2.3-tests"));

            Assert.That(exception.ParamName, Is.EqualTo("editorInstanceId"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenSidecarGenerationIdIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() => new UnityLifecycleSidecarPersistence(
                AbsolutePath.Parse(Path.GetTempPath()),
                ProjectFingerprint,
                EditorInstanceId,
                Guid.Empty,
                "1.2.3-tests"));

            Assert.That(exception.ParamName, Is.EqualTo("sidecarGenerationId"));
        }

        [Test]
        [Category("Size.Small")]
        public async Task WriteAsync_WithCapturedSnapshot_PersistsLifecycleContract ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                $"ucli-lifecycle-sidecar-persistence-tests-{Guid.NewGuid():N}");
            var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
            var observedAtUtc = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
            var persistence = new UnityLifecycleSidecarPersistence(
                guardedStorageRoot,
                ProjectFingerprint,
                EditorInstanceId,
                PredecessorSidecarGenerationId,
                "1.2.3-tests");
            var snapshot = CreateObservation(
                IpcEditorLifecycleState.Recovering,
                observedAtUtc);
            var recoveryLease = new DaemonLifecycleRecoveryLease(
                SessionGenerationId,
                observedAtUtc + DaemonLifecycleObservationTimings.DomainReloadRecoveryLeaseDuration);

            try
            {
                await persistence.WriteAsync(snapshot, recoveryLease, CancellationToken.None);

                var sidecarPath = UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                    guardedStorageRoot,
                    ProjectFingerprint);
                var contract = DaemonLifecycleJsonContractSerializer.Deserialize(
                    File.ReadAllText(sidecarPath.Value));

                Assert.That(contract, Is.Not.Null);
                Assert.That(contract.State.LifecycleState, Is.EqualTo(IpcEditorLifecycleState.Recovering));
                Assert.That(
                    IpcEditorLifecycleSemantics.ResolveBlockingReason(contract.State.LifecycleState),
                    Is.EqualTo(IpcEditorBlockingReason.Recovery));
                Assert.That(
                    IpcEditorLifecycleSemantics.CanAcceptExecutionRequests(contract.State.LifecycleState),
                    Is.False);
                Assert.That(contract.ObservedAtUtc, Is.EqualTo(observedAtUtc));
                Assert.That(contract.SidecarGenerationId, Is.EqualTo(PredecessorSidecarGenerationId));
                Assert.That(contract.ServerVersion, Is.EqualTo("1.2.3-tests"));
                Assert.That(contract.EditorInstanceId, Is.EqualTo(EditorInstanceId));
                Assert.That(contract.RecoveryLease, Is.EqualTo(recoveryLease));

                await persistence.DeleteIfOwnedAsync(CancellationToken.None);

                Assert.That(File.Exists(sidecarPath.Value), Is.False);
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task DeleteIfOwnedAsync_WhenSuccessorWritesSameValuesWithDifferentGenerationId_DoesNotDeleteSuccessorSidecar ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                $"ucli-lifecycle-sidecar-ownership-tests-{Guid.NewGuid():N}");
            var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
            var predecessor = new UnityLifecycleSidecarPersistence(
                guardedStorageRoot,
                OwnershipProjectFingerprint,
                EditorInstanceId,
                PredecessorSidecarGenerationId,
                "same-version");
            var successor = new UnityLifecycleSidecarPersistence(
                guardedStorageRoot,
                OwnershipProjectFingerprint,
                EditorInstanceId,
                SuccessorSidecarGenerationId,
                "same-version");
            var sidecarPath = UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                guardedStorageRoot,
                OwnershipProjectFingerprint);
            var observation = CreateObservation(
                IpcEditorLifecycleState.Ready,
                CreateObservedAtUtc(0));

            try
            {
                await predecessor.WriteAsync(observation, null, CancellationToken.None);
                await successor.WriteAsync(observation, null, CancellationToken.None);

                await predecessor.DeleteIfOwnedAsync(CancellationToken.None);

                Assert.That(File.Exists(sidecarPath.Value), Is.True);
                var contract = DaemonLifecycleJsonContractSerializer.Deserialize(
                    File.ReadAllText(sidecarPath.Value));
                Assert.That(contract, Is.Not.Null);
                Assert.That(contract.SidecarGenerationId, Is.EqualTo(SuccessorSidecarGenerationId));

                await successor.DeleteIfOwnedAsync(CancellationToken.None);

                Assert.That(File.Exists(sidecarPath.Value), Is.False);
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }
            }
        }

        private static UnityEditorObservation CreateObservation (
            IpcEditorLifecycleState lifecycleState,
            DateTimeOffset observedAtUtc)
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Gui,
                    lifecycleState: lifecycleState,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(1, 2, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        IpcPlayModeState.Stopped,
                        IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: observedAtUtc);
        }

        private static DateTimeOffset CreateObservedAtUtc (int offsetSeconds)
        {
            return new DateTimeOffset(2026, 7, 11, 0, 0, offsetSeconds, TimeSpan.Zero);
        }
    }
}
