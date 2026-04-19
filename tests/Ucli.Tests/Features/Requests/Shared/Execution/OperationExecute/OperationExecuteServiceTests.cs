using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Hosting.Cli;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Tests.Execution.OperationExecute;

public sealed class OperationExecuteServiceTests
{
    private static readonly JsonElement EmptyArgs = JsonSerializer.SerializeToElement(new { });

    private static readonly OperationExecuteDefinition RefreshOperation = new(
        Command: UcliCommandIds.Refresh,
        OperationId: "refresh",
        Descriptor: new UcliOperationDescriptor(
            Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
            Kind: UcliOperationKind.Mutation,
            Policy: OperationPolicy.Advanced,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}"""),
        Args: EmptyArgs);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAuthorizationAndUnityExecutionSucceed_UsesFixedOperationRequest ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var timeProvider = new ManualTimeProvider();
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityIpcRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched:
                            [
                                new IpcExecuteTouchedResource(
                                    Kind: IpcExecuteTouchedResourceKindNames.Asset,
                                    Path: "Assets/Example.txt",
                                    Guid: "11111111111111111111111111111111"),
                            ]),
                    ],
                    errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, timeProvider);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: "daemon",
            timeout: "120000",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.Equal(IpcProtocol.CurrentVersion, result.ProtocolVersion);
        Assert.True(Guid.TryParseExact(result.RequestId, "D", out _));
        Assert.True(result.IsSuccess);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Empty(result.Errors);
        Assert.Single(result.OpResults);

        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh, authorizationService.CapturedOperation!.Name);
        Assert.Equal(OperationPolicy.Advanced, authorizationService.CapturedOperation.Policy);

        Assert.Equal(UcliCommandIds.Refresh, ipcRequestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Daemon, ipcRequestExecutor.CapturedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(120000), ipcRequestExecutor.CapturedTimeout);
        Assert.Equal(IpcMethodNames.Execute, ipcRequestExecutor.CapturedMethod);
        Assert.Equal("/repo", ipcRequestExecutor.CapturedProject!.RepositoryRoot);

        Assert.True(IpcPayloadCodec.TryDeserialize(
            ipcRequestExecutor.CapturedPayload,
            out IpcExecuteRequest executeRequest,
            out var payloadError));
        Assert.Equal(IpcPayloadReadError.None, payloadError);
        Assert.Equal(UcliCommandIds.Call, executeRequest.Command);
        Assert.Equal(JsonValueKind.Object, executeRequest.Arguments.ValueKind);
        Assert.Equal(IpcProtocol.CurrentVersion, executeRequest.Arguments.GetProperty("protocolVersion").GetInt32());
        Assert.Equal(result.RequestId, executeRequest.Arguments.GetProperty("requestId").GetString());
        Assert.True(executeRequest.FailFast);
        var step = Assert.Single(executeRequest.Arguments.GetProperty("steps").EnumerateArray());
        Assert.Equal("op", step.GetProperty("kind").GetString());
        Assert.Equal("refresh", step.GetProperty("id").GetString());
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh, step.GetProperty("op").GetString());
        Assert.Equal(JsonValueKind.Object, step.GetProperty("args").ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanTokenModeIsRequired_IssuesPlanBeforeCallWithIssuedToken ()
    {
        var config = UcliConfig.CreateDefault() with
        {
            PlanTokenMode = PlanTokenMode.Required,
        };
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext(config)));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityIpcRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: "plan-token-1")),
            UnityIpcRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "120000",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, ipcRequestExecutor.CallCount);

        var planInvocation = ipcRequestExecutor.Invocations[0];
        Assert.Equal(UcliCommandIds.Refresh, planInvocation.Command);
        Assert.Equal(IpcMethodNames.Execute, planInvocation.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(planInvocation.Payload, out IpcExecuteRequest planRequest, out var planPayloadError));
        Assert.Equal(IpcPayloadReadError.None, planPayloadError);
        Assert.Equal(UcliCommandIds.Plan, planRequest.Command);
        Assert.Null(planRequest.PlanToken);
        Assert.True(planRequest.FailFast);
        Assert.Equal(result.RequestId, planRequest.Arguments.GetProperty("requestId").GetString());

        var callInvocation = ipcRequestExecutor.Invocations[1];
        Assert.Equal(UcliCommandIds.Refresh, callInvocation.Command);
        Assert.Equal(IpcMethodNames.Execute, callInvocation.Method);
        Assert.True(IpcPayloadCodec.TryDeserialize(callInvocation.Payload, out IpcExecuteRequest callRequest, out var callPayloadError));
        Assert.Equal(IpcPayloadReadError.None, callPayloadError);
        Assert.Equal(UcliCommandIds.Call, callRequest.Command);
        Assert.Equal("plan-token-1", callRequest.PlanToken);
        Assert.True(callRequest.FailFast);
        Assert.Equal(result.RequestId, callRequest.Arguments.GetProperty("requestId").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanConsumesTimeoutBudget_PassesRemainingTimeoutToCall ()
    {
        var timeProvider = new ManualTimeProvider();
        var config = UcliConfig.CreateDefault() with
        {
            PlanTokenMode = PlanTokenMode.Required,
        };
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext(config)));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityIpcRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: "plan-token-1")),
            UnityIpcRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [])))
        {
            TimeProvider = timeProvider,
            OnExecute = static context =>
            {
                if (context.Index == 1)
                {
                    context.TimeProvider!.Advance(TimeSpan.FromMilliseconds(200));
                }
            },
        };
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, timeProvider);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1200",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), ipcRequestExecutor.Invocations[0].Timeout);
        Assert.Equal(TimeSpan.FromMilliseconds(1000), ipcRequestExecutor.Invocations[1].Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanConsumesEntireTimeoutBudget_ReturnsTimeoutBeforeCall ()
    {
        var timeProvider = new ManualTimeProvider();
        var config = UcliConfig.CreateDefault() with
        {
            PlanTokenMode = PlanTokenMode.Required,
        };
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext(config)));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "refresh",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        Phase: IpcExecuteOperationPhaseNames.Plan,
                        Applied: false,
                        Changed: false,
                        Touched: []),
                ],
                errors: [],
                planToken: "plan-token-1")))
        {
            TimeProvider = timeProvider,
            OnExecute = static context =>
            {
                if (context.Index == 1)
                {
                    context.TimeProvider!.Advance(TimeSpan.FromMilliseconds(1200));
                }
            },
        };
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, timeProvider);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1200",
            failFast: true,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, ipcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(CliErrorCodes.IpcTimeout, error.Code);
        Assert.Equal("Timed out before Unity IPC execute request could begin.", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAuthorizationFails_DoesNotCallUnityExecutor ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Denied(
            ValidationErrorCodes.OperationNotAllowed,
            "Operation 'ucli.project.refresh' is blocked by operationPolicy='safe'."));
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults: [],
                errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: null,
            timeout: null,
            failFast: false,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationNotAllowed, error.Code);
        Assert.Equal("refresh", error.OpId);
        Assert.Equal(0, ipcRequestExecutor.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcErrorCodes.InvalidArgument, (int)CliExitCode.InvalidArgument)]
    [InlineData(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, (int)CliExitCode.ToolError)]
    public async Task Execute_WhenTransportExecutionFails_MapsExitCodeFromErrorCode (
        string errorCode,
        int expectedExitCode)
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Failure(
            message: "execution failed",
            errorCode: errorCode));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: null,
            timeout: null,
            failFast: false,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(errorCode, error.Code);
        Assert.Equal("execution failed", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsErrorResponse_PreservesOpResultsAndErrors ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusError,
                opResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "refresh",
                        Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        Phase: IpcExecuteOperationPhaseNames.Call,
                        Applied: true,
                        Changed: false,
                        Touched: []),
                ],
                errors:
                [
                    new IpcError(IpcErrorCodes.InvalidArgument, "refresh failed", "refresh"),
                ])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: null,
            timeout: null,
            failFast: false,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Single(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, error.Code);
        Assert.Equal("refresh", error.OpId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityPayloadIsInvalid_ReturnsInternalError ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityIpcRequestExecutionResult.Success(
            new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-1",
                Status: IpcProtocol.StatusOk,
                Payload: JsonSerializer.SerializeToElement(new { invalid = true }),
                Errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor);

        var result = await service.Execute(
            RefreshOperation,
            projectPath: "/repo/UnityProject",
            mode: null,
            timeout: null,
            failFast: false,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Contains("payload is invalid", error.Message, StringComparison.Ordinal);
    }

    private static ProjectContext CreateContext (UcliConfig? config = null)
    {
        return new ProjectContext(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: "/repo/UnityProject",
                RepositoryRoot: "/repo",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Config: config ?? UcliConfig.CreateDefault(),
            ConfigSource: ConfigSource.Default);
    }

    private static IpcResponse CreateResponse (
        string status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null)
    {
        return new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-1",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(opResults)
            {
                PlanToken = planToken,
            }),
            Errors: errors);
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyOperationAuthorizationService : IOperationAuthorizationService
    {
        private readonly OperationAuthorizationResult result;

        public SpyOperationAuthorizationService (OperationAuthorizationResult result)
        {
            this.result = result;
        }

        public UcliOperationDescriptor? CapturedOperation { get; private set; }

        public ValueTask<OperationAuthorizationResult> Authorize (
            UcliOperationDescriptor operation,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedOperation = operation;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyUnityIpcRequestExecutor : IUnityIpcRequestExecutor
    {
        private readonly Queue<UnityIpcRequestExecutionResult> results;

        private readonly List<Invocation> invocations = [];

        public SpyUnityIpcRequestExecutor (params UnityIpcRequestExecutionResult[] results)
        {
            this.results = new Queue<UnityIpcRequestExecutionResult>(results ?? throw new ArgumentNullException(nameof(results)));
        }

        public Action<InvocationContext>? OnExecute { get; init; }

        public ManualTimeProvider? TimeProvider { get; init; }

        public int CallCount { get; private set; }

        public IReadOnlyList<Invocation> Invocations => invocations;

        public UcliCommand CapturedCommand => invocations[^1].Command;

        public UnityExecutionMode CapturedMode => invocations[^1].Mode;

        public TimeSpan CapturedTimeout => invocations[^1].Timeout;

        public ResolvedUnityProjectContext? CapturedProject => invocations[^1].UnityProject;

        public string? CapturedMethod => invocations[^1].Method;

        public JsonElement CapturedPayload => invocations[^1].Payload;

        public ValueTask<UnityIpcRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            string method,
            JsonElement payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            var invocation = new Invocation(command, mode, timeout, unityProject, method, payload);
            invocations.Add(invocation);
            OnExecute?.Invoke(new InvocationContext(CallCount, invocation, TimeProvider));
            if (!results.TryDequeue(out var result))
            {
                throw new InvalidOperationException("No queued Unity IPC execution result is available.");
            }

            return ValueTask.FromResult(result);
        }

        public sealed record Invocation (
            UcliCommand Command,
            UnityExecutionMode Mode,
            TimeSpan Timeout,
            ResolvedUnityProjectContext UnityProject,
            string Method,
            JsonElement Payload);

        public sealed record InvocationContext (
            int Index,
            Invocation Invocation,
            ManualTimeProvider? TimeProvider);
    }
}