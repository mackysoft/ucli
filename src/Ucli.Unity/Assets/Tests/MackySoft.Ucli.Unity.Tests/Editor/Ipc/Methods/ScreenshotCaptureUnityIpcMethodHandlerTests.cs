using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ScreenshotCaptureUnityIpcMethodHandlerTests
    {
        private static readonly Guid RequestId = Guid.Parse("00000000-0000-0000-0000-000000000614");

        private static readonly Guid CaptureId = Guid.Parse("ab66cdfa-d4bd-49bd-b727-a1201d4426f4");

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithValidRequest_ReturnsCaptureResponse ()
        {
            var service = CreateCaptureService(CreateSuccessResult());
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(service, new ImmediateUnityMutationLaneControl());

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler,
                CreateRequest(CreatePayload()),
                CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(service.CallCount, Is.EqualTo(1));
            Assert.That(
                service.LastRequest.CaptureId,
                Is.EqualTo(CaptureId));
            Assert.That(
                service.LastRequest.Target,
                Is.EqualTo(IpcScreenshotTarget.Game));
            Assert.That(
                IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out IpcScreenshotCaptureResponse payload,
                    out _),
                Is.True);
            Assert.That(payload.Capture.Width, Is.EqualTo(2));
            Assert.That(payload.Staging.SizeBytes, Is.EqualTo(8));
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithSceneRequestedSize_ReturnsInvalidArgumentWithoutCapture ()
        {
            var service = CreateCaptureService(CreateSuccessResult());
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(service, new ImmediateUnityMutationLaneControl());
            var request = CreateRequest(new
            {
                captureId = CaptureId,
                target = "scene",
                requestedWidth = 640,
                requestedHeight = 480,
            });

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(service.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithOversizedRequestedRaster_ReturnsInvalidArgumentWithoutCapture ()
        {
            var service = CreateCaptureService(CreateSuccessResult());
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(service, new ImmediateUnityMutationLaneControl());
            var request = CreateRequest(new
            {
                captureId = CaptureId,
                target = "game",
                requestedWidth = IpcScreenshotCaptureLimits.MaximumDimension,
                requestedHeight = IpcScreenshotCaptureLimits.MaximumDimension,
            });

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(service.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenCaptureReportsUnsupported_PreservesErrorCode ()
        {
            var service = CreateCaptureService(UnityScreenshotCaptureResult.Failure(
                ScreenshotErrorCodes.ScreenshotCaptureUnsupported,
                "unsupported tuple"));
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(service, new ImmediateUnityMutationLaneControl());

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler,
                CreateRequest(CreatePayload()),
                CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenCaptureExceedsDeadline_ReturnsIpcTimeout ()
        {
            var service = new StubCaptureService(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("The cancelled delay unexpectedly completed.");
            });
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(service, new ImmediateUnityMutationLaneControl());
            var request = CreateRequest(CreatePayload(), requestDeadlineRemainingMilliseconds: 10);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(handler, request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
        }

        private static IpcScreenshotCaptureRequest CreatePayload ()
        {
            return new IpcScreenshotCaptureRequest(
                CaptureId,
                IpcScreenshotTarget.Game,
                RequestedWidth: null,
                RequestedHeight: null);
        }

        private static IpcRequestEnvelope CreateRequest<TPayload> (
            TPayload payload,
            int requestDeadlineRemainingMilliseconds = 5000)
        {
            return new IpcRequestEnvelope(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: RequestId,
                sessionToken: "session-token",
                method: ContractLiteralCodec.ToValue(UnityIpcMethod.ScreenshotCapture),
                payload: IpcPayloadCodec.SerializeToElement(payload),
                responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single),
                requestDeadlineUtc: DateTimeOffset.UtcNow
                    + TimeSpan.FromMilliseconds(requestDeadlineRemainingMilliseconds),
                requestDeadlineRemainingMilliseconds: requestDeadlineRemainingMilliseconds);
        }

        private static UnityScreenshotCaptureResult CreateSuccessResult ()
        {
            return UnityScreenshotCaptureResult.Success(new IpcScreenshotCaptureResponse(
                CaptureId,
                new IpcScreenshotCapture(
                    IpcScreenshotTarget.Game,
                    IpcScreenshotSizeMode.CurrentSurface,
                    RequestedWidth: null,
                    RequestedHeight: null,
                    Width: 2,
                    Height: 1,
                    IpcScreenshotColorSpace.Linear,
                    State: new UnityEditorStateSnapshot(
                        editorMode: DaemonEditorMode.Gui,
                        lifecycleState: IpcEditorLifecycleState.Ready,
                        compileState: IpcCompileState.Ready,
                        generations: new IpcUnityGenerationSnapshot(1, 4, 2, 3),
                        playMode: new IpcPlayModeSnapshot(
                            State: IpcPlayModeState.Stopped,
                            Transition: IpcPlayModeTransition.None,
                            IsPlaying: false,
                            IsPlayingOrWillChangePlaymode: false))),
                new IpcScreenshotStagingImage(
                    Width: 2,
                    Height: 1,
                    IpcScreenshotPixelFormat.Rgba8Srgb,
                    IpcScreenshotRowOrder.TopDown,
                    RowStrideBytes: 8,
                    SizeBytes: 8)));
        }

        private static StubCaptureService CreateCaptureService (UnityScreenshotCaptureResult result)
        {
            return new StubCaptureService((_, _) => Task.FromResult(result));
        }

        private sealed class StubCaptureService : IUnityScreenshotCaptureService
        {
            private readonly Func<IpcScreenshotCaptureRequest, CancellationToken, Task<UnityScreenshotCaptureResult>> capture;

            public StubCaptureService (
                Func<IpcScreenshotCaptureRequest, CancellationToken, Task<UnityScreenshotCaptureResult>> capture)
            {
                this.capture = capture;
            }

            public int CallCount { get; private set; }

            public IpcScreenshotCaptureRequest LastRequest { get; private set; }

            public Task<UnityScreenshotCaptureResult> CaptureAsync (
                IpcScreenshotCaptureRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                LastRequest = request;
                return capture(request, cancellationToken);
            }
        }
    }
}
