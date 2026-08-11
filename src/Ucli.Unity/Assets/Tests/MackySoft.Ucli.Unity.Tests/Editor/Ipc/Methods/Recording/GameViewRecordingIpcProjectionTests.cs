using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Unity.Recording;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using ContractRecordingState = MackySoft.Ucli.Contracts.Recording.GameViewRecordingState;
using ContractStopReason = MackySoft.Ucli.Contracts.Recording.GameViewRecordingStopReason;
using RuntimeRecordingState = MackySoft.Ucli.Unity.Recording.GameViewRecordingState;
using RuntimeStopReason = MackySoft.Ucli.Unity.Recording.GameViewRecordingStopReason;

namespace MackySoft.Ucli.Unity.Ipc
{
    [TestFixture]
    internal sealed class GameViewRecordingIpcProjectionTests
    {
        private static readonly Sha256Digest RequestDigest =
            Sha256Digest.Parse(new string('c', 64));

        [Test]
        public void CaptureCurrentBinding_MatchesOnlyCompleteCurrentHostIdentity ()
        {
            var generation = new UnityEditorGenerationSnapshot(1, 2, 3, 4);
            var projection = CreateProjection(generation, out var host);
            var binding = projection.CaptureCurrentBinding();

            Assert.That(projection.IsCurrentBinding(binding), Is.True);
            Assert.That(binding.Process, Is.EqualTo(host.Process));
            Assert.That(binding.Runtime.RuntimeId, Is.EqualTo(host.EditorInstanceId));
            Assert.That(binding.Generation, Is.EqualTo(generation));
            Assert.That(projection.IsCurrentBinding(new IpcGameViewRecordingStartBinding(
                new ProcessIdentity(host.Process.ProcessId, host.Process.Generation + 1),
                binding.Runtime,
                binding.Generation)), Is.False);
            Assert.That(projection.IsCurrentBinding(new IpcGameViewRecordingStartBinding(
                binding.Process,
                new GameViewRecordingRuntimeIdentity(
                    Guid.NewGuid(),
                    binding.Runtime.OperatingSystem,
                    binding.Runtime.EncoderName,
                    binding.Runtime.EncoderVersion),
                binding.Generation)), Is.False);
            Assert.That(projection.IsCurrentBinding(new IpcGameViewRecordingStartBinding(
                binding.Process,
                binding.Runtime,
                new UnityEditorGenerationSnapshot(
                    generation.CompileGeneration,
                    generation.DomainReloadGeneration + 1,
                    generation.AssetRefreshGeneration,
                    generation.PlayModeGeneration))), Is.False);
        }

        [Test]
        public void Project_UsesBindingGenerationAndRejectsAnotherRuntime ()
        {
            var projection = CreateProjection(
                new UnityEditorGenerationSnapshot(5, 6, 7, 8),
                out _);
            var binding = projection.CaptureCurrentBinding();
            var accepted = CreateRuntimeSnapshot(binding);

            var projected = projection.Project(accepted, binding);

            Assert.That(projected.StartGeneration, Is.EqualTo(binding.Generation));
            Assert.That(projected.Runtime, Is.EqualTo(binding.Runtime));
            Assert.Throws<InvalidOperationException>(() => projection.Project(
                CreateRuntimeSnapshot(new IpcGameViewRecordingStartBinding(
                    binding.Process,
                    new GameViewRecordingRuntimeIdentity(
                        Guid.NewGuid(),
                        binding.Runtime.OperatingSystem,
                        binding.Runtime.EncoderName,
                        binding.Runtime.EncoderVersion),
                    binding.Generation)),
                binding));
        }

        [Test]
        public void ProjectMissingForStatus_BeforeDeadlineOnExactBinding_ReturnsPreparing ()
        {
            var projection = CreateProjection(
                new UnityEditorGenerationSnapshot(1, 1, 0, 1),
                out _);
            var binding = projection.CaptureCurrentBinding();

            var snapshot = projection.ProjectMissingForStatus(
                 Guid.NewGuid(),
                 RequestDigest,
                 effectiveMaxDurationSeconds: 120,
                 binding,
                 DateTimeOffset.UtcNow.AddMinutes(1),
                 known: null,
                 adapterRegistered: true);
            Assert.That(snapshot, Is.TypeOf<IpcGameViewRecordingActiveSnapshot>());
            var result = (IpcGameViewRecordingActiveSnapshot)snapshot;

            Assert.That(result.State, Is.EqualTo(ContractRecordingState.Preparing));
            Assert.That(result.StartedAtUtc, Is.Null);
            Assert.That(result.StartGeneration, Is.EqualTo(binding.Generation));
            Assert.That(result.Runtime, Is.EqualTo(binding.Runtime));
        }

        [Test]
        public void ProjectMissingForStatus_AfterDeadline_ReturnsIndeterminateWithoutInventingAcceptance ()
        {
            var projection = CreateProjection(
                new UnityEditorGenerationSnapshot(1, 1, 0, 1),
                out _);
            var binding = projection.CaptureCurrentBinding();

            var snapshot = projection.ProjectMissingForStatus(
                 Guid.NewGuid(),
                 RequestDigest,
                 effectiveMaxDurationSeconds: 120,
                 binding,
                 DateTimeOffset.UtcNow.AddSeconds(-1),
                 known: null,
                 adapterRegistered: true);
            Assert.That(snapshot, Is.TypeOf<IpcGameViewRecordingIndeterminateSnapshot>());
            var result = (IpcGameViewRecordingIndeterminateSnapshot)snapshot;

            Assert.That(result.State, Is.EqualTo(ContractRecordingState.Indeterminate));
            Assert.That(result.StopReason, Is.EqualTo(ContractStopReason.InternalFailure));
            Assert.That(result.Failure.Code, Is.EqualTo(
                GameViewRecordingErrorCodes.DispatchDeadlineExceeded));
            Assert.That(result.StartedAtUtc, Is.Null);
        }

        [Test]
        public void ProjectMissingForStatus_WhenGenerationChanged_PreservesKnownFactsInTerminalSnapshot ()
        {
            var startedGeneration = new UnityEditorGenerationSnapshot(1, 1, 0, 4);
            var currentGeneration = new UnityEditorGenerationSnapshot(1, 1, 0, 5);
            var projection = CreateProjection(currentGeneration, out var host);
            var binding = new IpcGameViewRecordingStartBinding(
                host.Process,
                GameViewRecordingRuntimeIdentityFactory.Create(host.EditorInstanceId),
                startedGeneration);
            var recordingId = Guid.NewGuid();
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-10);
            var known = CreateKnown(
                recordingId,
                binding,
                startedAtUtc,
                encodedFrameCount: 7);

            var snapshot = projection.ProjectMissingForStatus(
                 recordingId,
                 RequestDigest,
                 effectiveMaxDurationSeconds: 120,
                 binding,
                 DateTimeOffset.UtcNow.AddMinutes(1),
                 known,
                 adapterRegistered: true);
            Assert.That(snapshot, Is.TypeOf<IpcGameViewRecordingIndeterminateSnapshot>());
            var result = (IpcGameViewRecordingIndeterminateSnapshot)snapshot;

            Assert.That(result.State, Is.EqualTo(ContractRecordingState.Indeterminate));
            Assert.That(result.StopReason, Is.EqualTo(ContractStopReason.PlayModeExited));
            Assert.That(result.StartedAtUtc, Is.EqualTo(startedAtUtc));
            Assert.That(result.EncodedFrameCount, Is.EqualTo(7));
            Assert.That(result.StartGeneration, Is.EqualTo(startedGeneration));
            Assert.That(result.ObservedGeneration, Is.EqualTo(currentGeneration));
        }

        private static GameViewRecordingIpcProjection CreateProjection (
            UnityEditorGenerationSnapshot generation,
            out UnityLifecycleExecutionHostContext host)
        {
            host = new UnityLifecycleExecutionHostContext(
                new ProcessIdentity(123, 456),
                Guid.NewGuid(),
                Guid.NewGuid(),
                recoveryLease: null);
            return new GameViewRecordingIpcProjection(
                host,
                new FixedAvailabilitySource(generation));
        }

        private static GameViewRecordingSnapshot CreateRuntimeSnapshot (
            IpcGameViewRecordingStartBinding binding)
        {
            var now = DateTimeOffset.UtcNow;
            return new GameViewRecordingSnapshot(
                Guid.NewGuid(),
                RequestDigest,
                EffectiveMaxDurationSeconds: 120,
                RuntimeRecordingState.Recording,
                RuntimeStopReason.None,
                GameViewRecordingFailure.None,
                binding.Runtime,
                Cleanup: null,
                Target: null,
                Timing: null,
                StartedAtUtc: now,
                StopRequestedAtUtc: null,
                CompletedAtUtc: null,
                UpdatedAtUtc: now,
                Message: "active",
                StartBinding: binding);
        }

        private static IpcGameViewRecordingSnapshot CreateKnown (
            Guid recordingId,
            IpcGameViewRecordingStartBinding binding,
            DateTimeOffset startedAtUtc,
            int encodedFrameCount)
        {
            return new IpcGameViewRecordingActiveSnapshot(
                recordingId,
                RequestDigest,
                ContractRecordingState.Recording,
                binding.Runtime,
                target: null,
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount,
                startedAtUtc,
                updatedAtUtc: startedAtUtc,
                binding.Generation,
                binding.Generation);
        }

        private sealed class FixedAvailabilitySource : IUnityEditorAvailabilityObservationSource
        {
            private readonly UnityEditorRuntimeObservation observation;

            public FixedAvailabilitySource (UnityEditorGenerationSnapshot generation)
            {
                observation = new UnityEditorRuntimeObservation(
                    new UnityEditorStateSnapshot(
                        UnityEditorMode.Gui,
                        UnityEditorLifecycleState.Ready,
                        UnityEditorCompileState.Ready,
                        generation,
                        new UnityEditorPlayModeSnapshot(
                            UnityEditorPlayModeState.Playing,
                            UnityEditorPlayModeTransition.None,
                            IsPlaying: true,
                            IsPlayingOrWillChangePlaymode: true)),
                    DateTimeOffset.UtcNow);
            }

            public UnityEditorRuntimeObservation CaptureAvailabilityObservation ()
            {
                return observation;
            }
        }
    }
}
