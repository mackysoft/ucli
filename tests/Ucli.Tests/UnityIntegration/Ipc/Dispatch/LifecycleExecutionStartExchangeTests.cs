using System.Text.Json;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using static MackySoft.Ucli.Tests.Ipc.UnityDaemonIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class LifecycleExecutionStartExchangeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateRequest_PreservesRegistrationAndDeliveryBudget ()
    {
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile);
        var requestId = Guid.Parse(
            "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var sessionToken = IpcSessionTokenTestFactory.Create(
            "lifecycle-start-token");
        var requestDeadlineUtc =
            DateTimeOffset.UnixEpoch.AddSeconds(33);

        var request = LifecycleExecutionStartExchange.CreateRequest(
            dispatchRequest,
            sessionToken,
            requestId,
            requestDeadlineUtc,
            requestDeadlineRemainingMilliseconds: 2500);

        Assert.Equal(UnityIpcMethod.LifecycleStart, IpcRequestAssert.ParseMethod(request));
        Assert.Equal(requestId, request.RequestId);
        Assert.Equal(sessionToken.GetEncodedValue(), request.SessionToken);
        Assert.Equal(requestDeadlineUtc, request.RequestDeadlineUtc);
        Assert.Equal(2500, request.RequestDeadlineRemainingMilliseconds);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            request.Payload,
            out IpcLifecycleExecutionStartRequest start,
            out var readError),
            readError.Message);
        Assert.Equal(
            dispatchRequest.Registration!.ExecutionId,
            start.ExecutionId);
        Assert.Equal(
            dispatchRequest.Registration.DeadlineUtc,
            start.DeadlineUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InterpretResponse_WhenConfirmed_ReturnsTypedStartAndActionPayload ()
    {
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile);
        var start = LifecycleExecutionIpcTestResponseFactory
            .CreateStartBinding(
                dispatchRequest.CreateLifecycleStartRequest());
        var response = CreateStartResponse(start);

        var result = LifecycleExecutionStartExchange.InterpretResponse(
            dispatchRequest,
            response);

        var confirmed = Assert.IsType<
            LifecycleExecutionStartExchange.Confirmed>(result);
        Assert.Equal(start, confirmed.Start);
        Assert.True(IpcPayloadCodec.TryDeserialize(
            confirmed.ActionPayload,
            out IpcCompileRequest action,
            out var readError),
            readError.Message);
        Assert.Equal(start, action.Start);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InterpretResponse_WhenProviderRejectsStart_RetainsProviderResponse ()
    {
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile);
        var response = new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            IpcResponseStatus.Error,
            JsonSerializer.SerializeToElement(new { }),
            [
                new IpcError(
                    LifecycleExecutionErrorCodes.DeadlineExceeded,
                    "The execution deadline expired before Start was persisted.",
                    null),
            ]);

        var result = LifecycleExecutionStartExchange.InterpretResponse(
            dispatchRequest,
            response);

        var rejected = Assert.IsType<
            LifecycleExecutionStartExchange.ProviderRejected>(result);
        Assert.Same(response, rejected.Response);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InterpretResponse_WhenRequiredGenerationDiffers_ReturnsTypedMismatch ()
    {
        var registration = UnityIpcRequestBuilderTestSupport
            .CreateLifecycleRegistration(
                LifecycleExecutionKind.Compile);
        var originalStart = LifecycleExecutionIpcTestResponseFactory
            .CreateStartBinding(
                new UnityIpcRequestBuilder()
                    .Build(new UnityRequestPayload.Compile(
                        registration,
                        requiredStart: null))
                    .CreateLifecycleStartRequest());
        var dispatchRequest = new UnityIpcRequestBuilder().Build(
            new UnityRequestPayload.Compile(
                registration,
                originalStart));
        var differentGenerationStart = new LifecycleExecutionStartBinding(
            originalStart.LifecycleExecutionRef,
            originalStart.Project,
            originalStart.Host,
            new UnityEditorGenerationSnapshot(
                originalStart.StartedGeneration.CompileGeneration + 1,
                originalStart.StartedGeneration.DomainReloadGeneration,
                originalStart.StartedGeneration.AssetRefreshGeneration,
                originalStart.StartedGeneration.PlayModeGeneration),
            originalStart.DeadlineUtc,
            originalStart.StartedAtUtc);

        var result = LifecycleExecutionStartExchange.InterpretResponse(
            dispatchRequest,
            CreateStartResponse(differentGenerationStart));

        var mismatched = Assert.IsType<
            LifecycleExecutionStartExchange.Mismatched>(result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.GenerationMismatch,
            mismatched.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void InterpretResponse_WhenPayloadIsInvalid_ReturnsInternalFailure ()
    {
        var dispatchRequest = CreateLifecycleDispatchRequest(
            LifecycleExecutionKind.Compile);
        var response = new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            IpcResponseStatus.Ok,
            JsonSerializer.SerializeToElement(new { start = "invalid" }),
            []);

        var result = LifecycleExecutionStartExchange.InterpretResponse(
            dispatchRequest,
            response);

        var invalid = Assert.IsType<
            LifecycleExecutionStartExchange.Invalid>(result);
        Assert.Equal(UcliCoreErrorCodes.InternalError, invalid.Failure.Code);
    }

    private static IpcResponse CreateStartResponse (
        LifecycleExecutionStartBinding start)
    {
        return new IpcResponse(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            IpcResponseStatus.Ok,
            IpcPayloadCodec.SerializeToElement(
                new IpcLifecycleExecutionStartResponse(start)),
            []);
    }
}
