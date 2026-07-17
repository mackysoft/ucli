using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.ScreenshotCapture.Staging;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotCaptureServiceTests
    {
        private static readonly Guid CaptureId = Guid.Parse("ab66cdfa-d4bd-49bd-b727-a1201d4426f4");

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WithoutGuiSession_ReturnsRequiresGuiWithoutCapture ()
        {
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(CreateSnapshot(DaemonEditorMode.Batchmode, domainReloadGeneration: 1)),
                backend,
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotRequiresGuiSession));
            Assert.That(backend.CallCount, Is.Zero);
            Assert.That(writer.WriteCallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WithStableGuiFence_WritesRawImageAndReturnsObservedMetadata ()
        {
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: 7);
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                backend,
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(writer.WriteCallCount, Is.EqualTo(1));
            Assert.That(writer.LastCaptureId, Is.EqualTo(CaptureId));
            Assert.That(writer.LastBytes.ToArray(), Is.EqualTo(CreateFrameBytes()));
            Assert.That(
                result.Response.Capture.Target,
                Is.EqualTo(IpcScreenshotTarget.Game));
            Assert.That(
                result.Response.Capture.SizeMode,
                Is.EqualTo(IpcScreenshotSizeMode.CurrentSurface));
            Assert.That(result.Response.Capture.Width, Is.EqualTo(2));
            Assert.That(result.Response.Capture.Height, Is.EqualTo(1));
            Assert.That(result.Response.CaptureId, Is.EqualTo(CaptureId));
            Assert.That(
                result.Response.Capture.State.LifecycleState,
                Is.EqualTo(IpcEditorLifecycleState.Ready));
            Assert.That(
                result.Response.Capture.State.CompileState,
                Is.EqualTo(IpcCompileState.Ready));
            Assert.That(
                result.Response.Capture.ColorSpace,
                Is.EqualTo(IpcScreenshotColorSpace.Linear));
            Assert.That(
                result.Response.Capture.State.Generations,
                Is.EqualTo(new IpcUnityGenerationSnapshot(3, 7, 5, 2)));
            Assert.That(
                result.Response.Staging.PixelFormat,
                Is.EqualTo(IpcScreenshotPixelFormat.Rgba8Srgb));
            Assert.That(
                result.Response.Staging.RowOrder,
                Is.EqualTo(IpcScreenshotRowOrder.TopDown));
            Assert.That(result.Response.Staging.Width, Is.EqualTo(2));
            Assert.That(result.Response.Staging.Height, Is.EqualTo(1));
            Assert.That(result.Response.Staging.RowStrideBytes, Is.EqualTo(8));
            Assert.That(result.Response.Staging.SizeBytes, Is.EqualTo(8));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenRequestOwnsMutationLane_DoesNotTreatOwnLaneAsLifecycleChange ()
        {
            var telemetryState = new UnityEditorLifecycleTelemetryState(
                compileGeneration: 3,
                domainReloadGeneration: 7,
                assetRefreshGeneration: 5,
                playModeGeneration: 2,
                isDomainReloading: false,
                isShuttingDown: false,
                isStartupPending: false);
            var readinessGate = new UnityEditorReadinessGate(
                DaemonEditorMode.Gui,
                new UnityEditorLifecycleMonitor(
                    telemetryState,
                    static () => false,
                    static () => false,
                    static () => false,
                    static () => false),
                static () => false,
                new BusyMutationExecutionState(),
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                static _ => { },
                subscribeToEditorEvents: false);
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                readinessGate,
                new StubCaptureBackend(CreateBackendSuccess()),
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(writer.WriteCallCount, Is.EqualTo(1));
            Assert.That(
                readinessGate.CaptureAvailabilityObservation().State.LifecycleState,
                Is.EqualTo(IpcEditorLifecycleState.Busy));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenLifecycleFenceChanges_DoesNotPublishStagingImage ()
        {
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(
                    CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: 7),
                    CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: 8)),
                backend,
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(writer.WriteCallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenOnlyObservationMetadataChanges_PublishesStagingImage ()
        {
            var captured = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: 7);
            var before = new UnityEditorObservation(
                captured.State,
                new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero));
            var after = new UnityEditorObservation(
                before.State,
                new DateTimeOffset(2026, 7, 13, 0, 0, 1, TimeSpan.Zero),
                new IpcPrimaryDiagnostic(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                    Code: "CS0000",
                    File: null,
                    Line: null,
                    Column: null,
                    Message: "diagnostic changed"));
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(before, after),
                new StubCaptureBackend(CreateBackendSuccess()),
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(writer.WriteCallCount, Is.EqualTo(1));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenAssetRefreshGenerationChanges_DoesNotPublishStagingImage ()
        {
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(
                    CreateSnapshot(
                        DaemonEditorMode.Gui,
                        domainReloadGeneration: 7,
                        assetRefreshGeneration: 11),
                    CreateSnapshot(
                        DaemonEditorMode.Gui,
                        domainReloadGeneration: 7,
                        assetRefreshGeneration: 12)),
                backend,
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(writer.WriteCallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenCancelledAfterPublish_DeletesPublishedStagingImage ()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: 7);
            var writer = new StubStagingImageWriter(() => cancellationTokenSource.Cancel());
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                new StubCaptureBackend(CreateBackendSuccess()),
                writer);

            Assert.Catch<OperationCanceledException>(() =>
                service.CaptureAsync(CreateRequest(), cancellationTokenSource.Token)
                    .GetAwaiter()
                    .GetResult());
            Assert.That(writer.DeletedCaptureIds, Is.EqualTo(new[] { CaptureId }));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenWriterReportsWrongSize_DeletesPublishedStagingImage ()
        {
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: 7);
            var writer = new StubStagingImageWriter(reportedSizeBytes: 7);
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                new StubCaptureBackend(CreateBackendSuccess()),
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(writer.DeletedCaptureIds, Is.EqualTo(new[] { CaptureId }));
        }

        [TestCase(IpcScreenshotTarget.Game)]
        [TestCase(IpcScreenshotTarget.Scene)]
        [Category("Size.Small")]
        public void CaptureAsync_DuringStablePlayMode_CapturesPixels (
            IpcScreenshotTarget target)
        {
            var snapshot = CreateSnapshot(
                DaemonEditorMode.Gui,
                domainReloadGeneration: 7,
                isPlaying: true);
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                backend,
                new StubStagingImageWriter());

            var result = service.CaptureAsync(CreateRequest(target), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(backend.CallCount, Is.EqualTo(1));
            Assert.That(result.Response.Capture.Target, Is.EqualTo(target));
            Assert.That(
                result.Response.Capture.State.LifecycleState,
                Is.EqualTo(IpcEditorLifecycleState.PlayMode));
            Assert.That(
                result.Response.Capture.State.PlayMode.State,
                Is.EqualTo(IpcPlayModeState.Playing));
        }

        [TestCase(IpcScreenshotTarget.Game)]
        [TestCase(IpcScreenshotTarget.Scene)]
        [Category("Size.Small")]
        public void CaptureAsync_DuringPlayModeTransition_ReturnsCaptureUnsupported (
            IpcScreenshotTarget target)
        {
            var snapshot = new UnityEditorObservation(
                new UnityEditorStateSnapshot(
                    DaemonEditorMode.Gui,
                    IpcEditorLifecycleState.PlayMode,
                    IpcCompileState.Ready,
                    new IpcUnityGenerationSnapshot(3, 7, 5, 2),
                    new IpcPlayModeSnapshot(
                        IpcPlayModeState.Playing,
                        IpcPlayModeTransition.Exiting,
                        IsPlaying: true,
                        IsPlayingOrWillChangePlaymode: true)),
                DateTimeOffset.UnixEpoch);
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                backend,
                new StubStagingImageWriter());

            var result = service.CaptureAsync(CreateRequest(target), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(backend.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenGeneralReadinessAcceptsIncoherentState_ReturnsCaptureUnsupported ()
        {
            var snapshot = new UnityEditorObservation(
                new UnityEditorStateSnapshot(
                    DaemonEditorMode.Gui,
                    IpcEditorLifecycleState.Ready,
                    IpcCompileState.Ready,
                    new IpcUnityGenerationSnapshot(3, 7, 5, 2),
                    new IpcPlayModeSnapshot(
                        IpcPlayModeState.Playing,
                        IpcPlayModeTransition.None,
                        IsPlaying: true,
                        IsPlayingOrWillChangePlaymode: true)),
                DateTimeOffset.UnixEpoch);
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                backend,
                new StubStagingImageWriter());

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(backend.CallCount, Is.Zero);
        }

        private static IpcScreenshotCaptureRequest CreateRequest (
            IpcScreenshotTarget target = IpcScreenshotTarget.Game)
        {
            return new IpcScreenshotCaptureRequest(
                CaptureId,
                target,
                RequestedWidth: null,
                RequestedHeight: null);
        }

        private static UnityScreenshotBackendResult CreateBackendSuccess ()
        {
            return UnityScreenshotBackendResult.Success(
                new UnityScreenshotFrame(
                    width: 2,
                    height: 1,
                    IpcScreenshotColorSpace.Linear,
                    CreateFrameBytes()));
        }

        private static byte[] CreateFrameBytes ()
        {
            return new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };
        }

        private static UnityEditorObservation CreateSnapshot (
            DaemonEditorMode editorMode,
            long domainReloadGeneration,
            bool isPlaying = false,
            long assetRefreshGeneration = 5)
        {
            return new UnityEditorObservation(
                state: new UnityEditorStateSnapshot(
                    editorMode: editorMode,
                    lifecycleState: isPlaying
                        ? IpcEditorLifecycleState.PlayMode
                        : IpcEditorLifecycleState.Ready,
                    compileState: IpcCompileState.Ready,
                    generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: 3,
                        DomainReloadGeneration: domainReloadGeneration,
                        AssetRefreshGeneration: assetRefreshGeneration,
                        PlayModeGeneration: 2),
                    playMode: new IpcPlayModeSnapshot(
                        State: isPlaying
                            ? IpcPlayModeState.Playing
                            : IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: isPlaying,
                        IsPlayingOrWillChangePlaymode: isPlaying)),
                observedAtUtc: DateTimeOffset.UnixEpoch);
        }

        private sealed class StubCaptureBackend : IUnityScreenshotCaptureBackend
        {
            private readonly UnityScreenshotBackendResult result;

            public StubCaptureBackend (UnityScreenshotBackendResult result)
            {
                this.result = result;
            }

            public int CallCount { get; private set; }

            public Task<UnityScreenshotBackendResult> CaptureAsync (
                IpcScreenshotCaptureRequest request,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return Task.FromResult(result);
            }
        }

        private sealed class StubStagingImageWriter : IScreenshotStagingImageWriter
        {
            private readonly Action afterWrite;

            private readonly long? reportedSizeBytes;

            public StubStagingImageWriter (
                Action afterWrite = null,
                long? reportedSizeBytes = null)
            {
                this.afterWrite = afterWrite;
                this.reportedSizeBytes = reportedSizeBytes;
            }

            public int WriteCallCount { get; private set; }

            public Guid LastCaptureId { get; private set; }

            public ReadOnlyMemory<byte> LastBytes { get; private set; }

            public List<Guid> DeletedCaptureIds { get; } = new List<Guid>();

            public Task<long> WriteAtomicAsync (
                Guid captureId,
                ReadOnlyMemory<byte> bytes,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteCallCount++;
                LastCaptureId = captureId;
                LastBytes = bytes;
                afterWrite?.Invoke();
                return Task.FromResult(reportedSizeBytes ?? bytes.Length);
            }

            public void DeleteIfExists (Guid captureId)
            {
                DeletedCaptureIds.Add(captureId);
            }
        }

        private sealed class SequenceReadinessGate : IUnityEditorReadinessGate
        {
            private readonly UnityEditorObservation[] snapshots;

            private int snapshotIndex;

            public SequenceReadinessGate (params UnityEditorObservation[] snapshots)
            {
                this.snapshots = snapshots;
            }

            public UnityEditorObservation CaptureObservation ()
            {
                var index = Math.Min(snapshotIndex, snapshots.Length - 1);
                snapshotIndex++;
                return snapshots[index];
            }

            public Task<UnityEditorExecutionReadinessResult> EnsureExecutionReadyAsync (
                bool failFast,
                CancellationToken cancellationToken = default,
                bool allowPlayMode = false)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = CaptureObservation();
                return Task.FromResult(snapshot.CanAcceptExecutionRequests
                    ? UnityEditorExecutionReadinessResult.Ready(snapshot)
                    : UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
            }
        }

        private sealed class BusyMutationExecutionState : IUnityMutationExecutionState
        {
            public bool IsBusy => true;

            public bool HasUnfinishedWork => true;
        }
    }
}
