using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.ScreenshotCapture.Capture;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ScreenshotCaptureUnityIpcMethodHandlerTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithValidRequest_ReturnsCaptureResponse ()
        {
            var service = CreateCaptureService(CreateSuccessResult());
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(
                service,
                new IpcRequestTimeoutScopeFactory());

            var response = await handler.HandleAsync(
                CreateRequest(CreatePayload()),
                CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(service.CallCount, Is.EqualTo(1));
            Assert.That(
                service.LastRequest.Target,
                Is.EqualTo(ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game)));
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
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(
                service,
                new IpcRequestTimeoutScopeFactory());
            var request = CreateRequest(CreatePayload() with
            {
                Target = ContractLiteralCodec.ToValue(IpcScreenshotTarget.Scene),
                RequestedWidth = 640,
                RequestedHeight = 480,
            });

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(service.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WithOversizedRequestedRaster_ReturnsInvalidArgumentWithoutCapture ()
        {
            var service = CreateCaptureService(CreateSuccessResult());
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(
                service,
                new IpcRequestTimeoutScopeFactory());
            var request = CreateRequest(CreatePayload() with
            {
                RequestedWidth = IpcScreenshotCaptureLimits.MaximumDimension,
                RequestedHeight = IpcScreenshotCaptureLimits.MaximumDimension,
            });

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
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
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(
                service,
                new IpcRequestTimeoutScopeFactory());

            var response = await handler.HandleAsync(
                CreateRequest(CreatePayload()),
                CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ScreenshotErrorCodes.ScreenshotCaptureUnsupported));
        }

        [Test]
        [Category("Size.Small")]
        public async Task HandleAsync_WhenCaptureExceedsTimeout_ReturnsIpcTimeout ()
        {
            var service = new StubCaptureService(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                throw new InvalidOperationException("The cancelled delay unexpectedly completed.");
            });
            var handler = new ScreenshotCaptureUnityIpcMethodHandler(
                service,
                new IpcRequestTimeoutScopeFactory());
            var request = CreateRequest(CreatePayload() with { TimeoutMilliseconds = 10 });

            var response = await handler.HandleAsync(request, CancellationToken.None);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcTransportErrorCodes.IpcTimeout));
        }

        private static IpcScreenshotCaptureRequest CreatePayload ()
        {
            return new IpcScreenshotCaptureRequest(
                ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                RequestedWidth: null,
                RequestedHeight: null,
                StagingPath: "/tmp/ucli-screenshot-test/capture.rgba",
                TimeoutMilliseconds: 5000);
        }

        private static IpcRequest CreateRequest (IpcScreenshotCaptureRequest payload)
        {
            return new IpcRequest(
                IpcProtocol.CurrentVersion,
                RequestId: "screenshot-request",
                SessionToken: "session-token",
                IpcMethodNames.ScreenshotCapture,
                IpcPayloadCodec.SerializeToElement(payload),
                IpcResponseMode.Single);
        }

        private static UnityScreenshotCaptureResult CreateSuccessResult ()
        {
            return UnityScreenshotCaptureResult.Success(new IpcScreenshotCaptureResponse(
                new IpcScreenshotCapture(
                    ContractLiteralCodec.ToValue(IpcScreenshotTarget.Game),
                    ContractLiteralCodec.ToValue(IpcScreenshotSizeMode.CurrentSurface),
                    RequestedWidth: null,
                    RequestedHeight: null,
                    Width: 2,
                    Height: 1,
                    ContractLiteralCodec.ToValue(IpcScreenshotColorSpace.Linear),
                    LifecycleStateAtCapture: ContractLiteralCodec.ToValue(IpcEditorLifecycleState.Ready),
                    CompileStateAtCapture: ContractLiteralCodec.ToValue(IpcCompileState.Ready),
                    DomainReloadGeneration: 4,
                    PlayModeState: ContractLiteralCodec.ToValue(IpcPlayModeState.Stopped)),
                new IpcScreenshotStagingImage(
                    "/tmp/ucli-screenshot-test/capture.rgba",
                    ContractLiteralCodec.ToValue(IpcScreenshotPixelFormat.Rgba8Srgb),
                    ContractLiteralCodec.ToValue(IpcScreenshotRowOrder.TopDown),
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
