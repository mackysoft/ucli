using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

public sealed class OperationExecuteServiceFailureTests
{
    public static TheoryData<UcliCode, UcliCode, object> TransportFailureCases => new()
    {
        { UcliCoreErrorCodes.InvalidArgument, UcliCoreErrorCodes.InvalidArgument, ApplicationOutcome.InvalidArgument },
        { PlanTokenErrorCodes.PlanTokenInvalid, PlanTokenErrorCodes.PlanTokenInvalid, ApplicationOutcome.InvalidArgument },
        { UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, ApplicationOutcome.ToolError },
    };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAuthorizationFails_DoesNotCallUnityExecutor ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = new RecordingOperationAuthorizationService(OperationAuthorizationResult.Denied(
            OperationAuthorizationErrorCodes.OperationNotAllowed,
            "Operation 'ucli.project.refresh' is blocked by operationPolicy='safe'."));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            new UnexpectedUnityRequestExecutor());

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, error.Code);
        Assert.Equal("refresh", error.OpId);
    }

    [Theory]
    [MemberData(nameof(TransportFailureCases))]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTransportExecutionFails_MapsExitCodeFromErrorCode (
        UcliCode errorCode,
        UcliCode expectedErrorCode,
        object expectedOutcome)
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResultTestFactory.Failure(
            "execution failed",
            errorCode));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedErrorCode, error.Code);
        Assert.Equal("execution failed", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRequiredPlanTokenExecutionFails_ReturnsPlanFailure ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            PlanTokenMode = PlanTokenMode.Required,
        };
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver(config);
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResultTestFactory.Failure(
            "execution failed",
            UcliCoreErrorCodes.InternalError));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        OperationExecuteInvocationAssert.PlanOnlyDispatched(ipcRequestExecutor, UcliCommandIds.Refresh);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Equal("execution failed", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsErrorResponse_PreservesOpResultsAndErrors ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(
            ExecuteUnityRequestResponseTestFactory.Create(
                status: IpcProtocol.StatusError,
                opResults:
                [
                    OperationExecuteServiceTestSupport.CreateCallOperationResult(changed: false),
                ],
                errors:
                [
                    new IpcError(UcliCoreErrorCodes.InvalidArgument, "refresh failed", "refresh"),
                ])));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Single(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Equal("refresh", error.OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityPayloadIsInvalid_ReturnsInternalError ()
    {
        var projectContextResolver = OperationExecuteServiceTestSupport.CreateProjectContextResolver();
        var authorizationService = OperationExecuteServiceTestSupport.CreateAllowedAuthorizationService();
        var ipcRequestExecutor = new RecordingUnityRequestExecutor(UnityRequestExecutionResult.Success(
            UnityRequestResponseTestFactory.Create(new IpcResponse(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: Guid.NewGuid(),
                status: IpcProtocol.StatusOk,
                payload: JsonSerializer.SerializeToElement(new { invalid = true }),
                errors: []))));
        var service = OperationExecuteServiceTestSupport.CreateService(
            projectContextResolver,
            authorizationService,
            ipcRequestExecutor);

        var result = await service.ExecuteAsync(
            OperationExecuteServiceTestSupport.RequestId,
            OperationExecuteServiceTestSupport.RefreshOperation,
            OperationExecuteServiceTestSupport.CreateInput(
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }
}
