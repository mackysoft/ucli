using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        [Test]
        [Category("Size.Small")]
        public async Task WriteAsync_WithCapturedSnapshot_PersistsLifecycleContract ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                $"ucli-lifecycle-sidecar-persistence-tests-{Guid.NewGuid():N}");
            const string ProjectFingerprint = "lifecycle-sidecar-fingerprint";
            var observedAtUtc = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
            var persistence = new UnityLifecycleSidecarPersistence(
                storageRoot,
                ProjectFingerprint,
                "1.2.3-tests");
            var snapshot = new UnityEditorLifecycleSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleStateCodec.Recovering,
                IpcEditorBlockingReasonCodec.Recovery,
                IpcCompileStateCodec.Ready,
                "compile-generation",
                "reload-generation",
                CanAcceptExecutionRequests: false,
                ObservedAtUtc: observedAtUtc);

            try
            {
                await persistence.WriteAsync(snapshot, CancellationToken.None);

                var sidecarPath = UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                    storageRoot,
                    ProjectFingerprint);
                var contract = DaemonLifecycleJsonContractSerializer.Deserialize(
                    File.ReadAllText(sidecarPath));

                Assert.That(contract, Is.Not.Null);
                Assert.That(contract.LifecycleState, Is.EqualTo(IpcEditorLifecycleStateCodec.Recovering));
                Assert.That(contract.BlockingReason, Is.EqualTo(IpcEditorBlockingReasonCodec.Recovery));
                Assert.That(contract.CanAcceptExecutionRequests, Is.False);
                Assert.That(contract.ObservedAtUtc, Is.EqualTo(observedAtUtc));
                Assert.That(contract.ServerVersion, Is.EqualTo("1.2.3-tests"));

                await persistence.DeleteIfOwnedAsync(CancellationToken.None);

                Assert.That(File.Exists(sidecarPath), Is.False);
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
        public async Task DeleteIfOwnedAsync_AfterSuccessorWrite_DoesNotDeleteSuccessorSidecar ()
        {
            var storageRoot = Path.Combine(
                Path.GetTempPath(),
                $"ucli-lifecycle-sidecar-ownership-tests-{Guid.NewGuid():N}");
            const string ProjectFingerprint = "lifecycle-sidecar-ownership-fingerprint";
            var predecessor = new UnityLifecycleSidecarPersistence(
                storageRoot,
                ProjectFingerprint,
                "predecessor");
            var successor = new UnityLifecycleSidecarPersistence(
                storageRoot,
                ProjectFingerprint,
                "successor");
            var sidecarPath = UcliStoragePathResolver.ResolveDaemonLifecyclePath(
                storageRoot,
                ProjectFingerprint);

            try
            {
                await predecessor.WriteAsync(
                    CreateSnapshot("predecessor", 0),
                    CancellationToken.None);
                await successor.WriteAsync(
                    CreateSnapshot("successor", 1),
                    CancellationToken.None);

                await predecessor.DeleteIfOwnedAsync(CancellationToken.None);

                var contract = DaemonLifecycleJsonContractSerializer.Deserialize(
                    File.ReadAllText(sidecarPath));
                Assert.That(contract, Is.Not.Null);
                Assert.That(contract.ServerVersion, Is.EqualTo("successor"));

                await successor.DeleteIfOwnedAsync(CancellationToken.None);

                Assert.That(File.Exists(sidecarPath), Is.False);
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }
            }
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (
            string lifecycleState,
            int observedAtOffsetSeconds)
        {
            return new UnityEditorLifecycleSnapshot(
                DaemonEditorMode.Gui,
                lifecycleState,
                null,
                IpcCompileStateCodec.Ready,
                "compile-generation",
                "reload-generation",
                CanAcceptExecutionRequests: true,
                ObservedAtUtc: new DateTimeOffset(
                    2026,
                    7,
                    11,
                    0,
                    0,
                    observedAtOffsetSeconds,
                    TimeSpan.Zero));
        }
    }
}
