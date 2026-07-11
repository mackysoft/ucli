using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using MackySoft.Ucli.Unity.ScreenshotCapture.Staging;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityScreenshotCaptureServiceTests
    {
        private const string StagingPath = "/tmp/ucli-screenshot-test/capture.rgba";

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WithoutGuiSession_ReturnsRequiresGuiWithoutCapture ()
        {
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(CreateSnapshot(DaemonEditorMode.Batchmode, domainReloadGeneration: "1")),
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
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: "7");
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
            Assert.That(writer.LastPath, Is.EqualTo(StagingPath));
            Assert.That(writer.LastBytes.ToArray(), Is.EqualTo(CreateFrameBytes()));
            Assert.That(
                result.Response.Capture.Target,
                Is.EqualTo(ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game)));
            Assert.That(
                result.Response.Capture.SizeMode,
                Is.EqualTo(ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.CurrentSurface)));
            Assert.That(result.Response.Capture.Width, Is.EqualTo(2));
            Assert.That(result.Response.Capture.Height, Is.EqualTo(1));
            Assert.That(
                result.Response.Capture.ColorSpace,
                Is.EqualTo(ContractLiteralCodec.ToValue(IpcScreenshotColorSpace.Linear)));
            Assert.That(result.Response.Capture.DomainReloadGeneration, Is.EqualTo(7));
            Assert.That(
                result.Response.Staging.PixelFormat,
                Is.EqualTo(ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb)));
            Assert.That(
                result.Response.Staging.RowOrder,
                Is.EqualTo(ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown)));
            Assert.That(result.Response.Staging.RowStrideBytes, Is.EqualTo(8));
            Assert.That(result.Response.Staging.SizeBytes, Is.EqualTo(8));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenLifecycleFenceChanges_DoesNotPublishStagingImage ()
        {
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(
                    CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: "7"),
                    CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: "8")),
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
        public void CaptureAsync_WhenAssetRefreshGenerationChanges_DoesNotPublishStagingImage ()
        {
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(
                    CreateSnapshot(
                        DaemonEditorMode.Gui,
                        domainReloadGeneration: "7",
                        assetRefreshGeneration: "11"),
                    CreateSnapshot(
                        DaemonEditorMode.Gui,
                        domainReloadGeneration: "7",
                        assetRefreshGeneration: "12")),
                backend,
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(writer.WriteCallCount, Is.Zero);
        }

        [TestCase("compile-state")]
        [TestCase("play-mode-state")]
        [Category("Size.Small")]
        public void CaptureAsync_WithNonCanonicalCaptureMetadata_FailsBeforePixelCapture (string caseName)
        {
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: "7");
            snapshot = caseName switch
            {
                "compile-state" => snapshot with { CompileState = "ready " },
                "play-mode-state" => snapshot with
                {
                    PlayMode = snapshot.PlayMode with { State = "stopped " },
                },
                _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, null),
            };
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var writer = new StubStagingImageWriter();
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot),
                backend,
                writer);

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
            Assert.That(backend.CallCount, Is.Zero);
            Assert.That(writer.WriteCallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenCancelledAfterPublish_DeletesPublishedStagingImage ()
        {
            using var cancellationTokenSource = new CancellationTokenSource();
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: "7");
            var writer = new StubStagingImageWriter(() => cancellationTokenSource.Cancel());
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                new StubCaptureBackend(CreateBackendSuccess()),
                writer);

            Assert.Catch<OperationCanceledException>(() =>
                service.CaptureAsync(CreateRequest(), cancellationTokenSource.Token)
                    .GetAwaiter()
                    .GetResult());
            Assert.That(writer.DeletedPaths, Is.EqualTo(new[] { StagingPath }));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_WhenWriterReportsWrongSize_DeletesPublishedStagingImage ()
        {
            var snapshot = CreateSnapshot(DaemonEditorMode.Gui, domainReloadGeneration: "7");
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
            Assert.That(writer.DeletedPaths, Is.EqualTo(new[] { StagingPath }));
        }

        [Test]
        [Category("Size.Small")]
        public void CaptureAsync_DuringStablePlayMode_ReturnsLifecycleBlockerBeforePixelCapture ()
        {
            var snapshot = CreateSnapshot(
                DaemonEditorMode.Gui,
                domainReloadGeneration: "7",
                isPlaying: true);
            var backend = new StubCaptureBackend(CreateBackendSuccess());
            var service = new UnityScreenshotCaptureService(
                new SequenceReadinessGate(snapshot, snapshot),
                backend,
                new StubStagingImageWriter());

            var result = service.CaptureAsync(CreateRequest(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorPlaymode));
            Assert.That(backend.CallCount, Is.Zero);
        }

        private static IpcScreenshotCaptureRequest CreateRequest ()
        {
            return new IpcScreenshotCaptureRequest(
                ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                RequestedWidth: null,
                RequestedHeight: null,
                StagingPath,
                TimeoutMilliseconds: 5000);
        }

        private static UnityScreenshotBackendResult CreateBackendSuccess ()
        {
            return UnityScreenshotBackendResult.Success(
                new UnityScreenshotBackendResult.CapturedFrame(
                    Width: 2,
                    Height: 1,
                    ContractLiteralCodec.ToValue(IpcScreenshotColorSpace.Linear),
                    CreateFrameBytes()));
        }

        private static byte[] CreateFrameBytes ()
        {
            return new byte[] { 1, 2, 3, 255, 4, 5, 6, 255 };
        }

        private static UnityEditorLifecycleSnapshot CreateSnapshot (
            DaemonEditorMode editorMode,
            string domainReloadGeneration,
            bool isPlaying = false,
            string assetRefreshGeneration = "5")
        {
            return new UnityEditorLifecycleSnapshot(
                editorMode,
                isPlaying
                    ? IpcEditorLifecycleStateCodec.Playmode
                    : IpcEditorLifecycleStateCodec.Ready,
                BlockingReason: null,
                CompileState: IpcCompileStateCodec.Ready,
                CompileGeneration: "3",
                domainReloadGeneration,
                CanAcceptExecutionRequests: !isPlaying,
                PlayMode: new IpcPlayModeSnapshot(
                    State: ContractLiteralCodec.ToValue(isPlaying
                        ? IpcPlayModeState.Playing
                        : IpcPlayModeState.Stopped),
                    Transition: ContractLiteralCodec.ToValue(IpcPlayModeTransition.None),
                    IsPlaying: isPlaying,
                    IsPlayingOrWillChangePlaymode: isPlaying,
                    Generation: "2"),
                AssetRefreshGeneration: assetRefreshGeneration);
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

            public string LastPath { get; private set; }

            public ReadOnlyMemory<byte> LastBytes { get; private set; }

            public List<string> DeletedPaths { get; } = new List<string>();

            public Task<long> WriteAtomicAsync (
                string path,
                ReadOnlyMemory<byte> bytes,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteCallCount++;
                LastPath = path;
                LastBytes = bytes;
                afterWrite?.Invoke();
                return Task.FromResult(reportedSizeBytes ?? bytes.Length);
            }

            public void DeleteIfExists (string path)
            {
                DeletedPaths.Add(path);
            }
        }

        private sealed class SequenceReadinessGate : IUnityEditorReadinessGate
        {
            private readonly UnityEditorLifecycleSnapshot[] snapshots;

            private int snapshotIndex;

            public SequenceReadinessGate (params UnityEditorLifecycleSnapshot[] snapshots)
            {
                this.snapshots = snapshots;
            }

            public UnityEditorLifecycleSnapshot CaptureSnapshot ()
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
                var snapshot = CaptureSnapshot();
                return Task.FromResult(snapshot.CanAcceptExecutionRequests
                    ? UnityEditorExecutionReadinessResult.Ready(snapshot)
                    : UnityEditorExecutionReadinessPolicy.CreateBlockedResult(snapshot));
            }
        }
    }
}
