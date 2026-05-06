using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.OperationExecute;

public sealed class OperationExecuteServiceTests
{
    private static readonly JsonElement EmptyArgs = JsonSerializer.SerializeToElement(new { });

    private static readonly OperationExecuteDefinition RefreshOperation = new(
        Command: UcliCommandIds.Refresh,
        OperationId: "refresh",
        Descriptor: new UcliOperationDescriptor(
            Name: UcliPrimitiveOperationNames.ProjectRefresh,
            Kind: UcliOperationKind.Command,
            Policy: OperationPolicy.Advanced,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}"""),
        Args: EmptyArgs,
        SuccessMessage: "uCLI refresh completed.",
        FailureMessage: "uCLI refresh failed.");

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenAuthorizationAndUnityExecutionSucceed_UsesFixedOperationRequest ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var timeProvider = new ManualTimeProvider();
        var operationResultPayload = JsonSerializer.SerializeToElement(new
        {
            ok = true,
        });
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched:
                            [
                                new IpcExecuteTouchedResource(
                                    Kind: IpcExecuteTouchedResourceKindNames.Asset,
                                    Path: "Assets/Example.txt",
                                    Guid: "11111111111111111111111111111111"),
                            ])
                        {
                            Result = operationResultPayload,
                        },
                    ],
                    errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore(), timeProvider);

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(Guid.TryParseExact(result.RequestId, "D", out _));
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.Success, result.Outcome);
        Assert.Empty(result.Errors);
        var opResult = Assert.Single(result.OpResults);
        Assert.Equal("refresh", opResult.OpId);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, opResult.Op);
        Assert.Equal(IpcExecuteOperationPhaseNames.Call, opResult.Phase);
        Assert.True(opResult.Applied);
        Assert.True(opResult.Changed);
        Assert.Equal(JsonValueKind.Object, opResult.Result!.Value.ValueKind);
        var touchedResource = Assert.Single(opResult.Touched);
        Assert.Equal(IpcExecuteTouchedResourceKindNames.Asset, touchedResource.Kind);
        Assert.Equal("Assets/Example.txt", touchedResource.Path);
        Assert.Equal("11111111111111111111111111111111", touchedResource.Guid);

        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, authorizationService.CapturedOperation!.Name);
        Assert.Equal(OperationPolicy.Advanced, authorizationService.CapturedOperation.Policy);

        Assert.Equal(UcliCommandIds.Refresh, ipcRequestExecutor.CapturedCommand);
        Assert.Equal(UnityExecutionMode.Daemon, ipcRequestExecutor.CapturedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(120000), ipcRequestExecutor.CapturedTimeout);
        Assert.Equal("/repo", ipcRequestExecutor.CapturedProject!.RepositoryRoot);

        var executeRequest = Assert.IsType<UnityRequestPayload.ExecuteOperation>(ipcRequestExecutor.CapturedPayload);
        Assert.Equal(UcliCommandIds.Call, executeRequest.Command);
        Assert.Equal(result.RequestId, executeRequest.RequestId);
        Assert.True(executeRequest.FailFast);
        Assert.Equal("refresh", executeRequest.OperationId);
        Assert.Equal(UcliPrimitiveOperationNames.ProjectRefresh, executeRequest.OperationName);
        Assert.Equal(JsonValueKind.Object, executeRequest.Args.ValueKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallResponseIncludesReadPostcondition_PersistsAndReturnsIt ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var readPostconditionStore = new TestMutationReadPostconditionStore();
        var readPostcondition = CreateReadPostcondition();
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: true,
                            Touched: []),
                    ],
                    errors: [],
                    readPostcondition: readPostcondition)));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, readPostconditionStore);

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, readPostconditionStore.WriteCallCount);
        Assert.Equal("/repo", readPostconditionStore.LastStorageRoot);
        Assert.Equal("project-fingerprint", readPostconditionStore.LastProjectFingerprint);
        Assert.NotNull(result.ReadPostcondition);
        var requirement = Assert.Single(result.ReadPostcondition!.Requirements);
        Assert.Equal(IpcExecuteReadPostconditionSurfaceNames.AssetSearch, requirement.Surface);
        Assert.Equal(readPostcondition.Requirements[0].MinSafeGeneratedAtUtc, requirement.MinSafeGeneratedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadPostconditionPersistenceFails_ReturnsToolErrorAndPreservesPayload ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var readPostconditionStore = new TestMutationReadPostconditionStore
        {
            WriteResult = MutationReadPostconditionStoreOperationResult.Failure(
                ExecutionError.InternalError("Failed to persist mutation read postcondition.")),
        };
        var readPostcondition = CreateReadPostcondition();
        var opResults = new[]
        {
            new IpcExecuteOperationResult(
                OpId: "refresh",
                Op: UcliPrimitiveOperationNames.ProjectRefresh,
                Phase: IpcExecuteOperationPhaseNames.Call,
                Applied: true,
                Changed: true,
                Touched: []),
        };
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults: opResults,
                    errors: [],
                    readPostcondition: readPostcondition)));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, readPostconditionStore);

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: UnityExecutionMode.Daemon,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Single(result.OpResults);
        Assert.NotNull(result.ReadPostcondition);
        Assert.Equal(1, readPostconditionStore.WriteCallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Equal("Failed to persist mutation read postcondition.", error.Message);
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
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: "plan-token-1")),
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Call,
                            Applied: true,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore());

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 120000,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, ipcRequestExecutor.CallCount);

        var planInvocation = ipcRequestExecutor.Invocations[0];
        Assert.Equal(UcliCommandIds.Refresh, planInvocation.Command);
        var planRequest = Assert.IsType<UnityRequestPayload.ExecuteOperation>(planInvocation.Payload);
        Assert.Equal(UcliCommandIds.Plan, planRequest.Command);
        Assert.Null(planRequest.PlanToken);
        Assert.True(planRequest.FailFast);
        Assert.Equal(result.RequestId, planRequest.RequestId);

        var callInvocation = ipcRequestExecutor.Invocations[1];
        Assert.Equal(UcliCommandIds.Refresh, callInvocation.Command);
        var callRequest = Assert.IsType<UnityRequestPayload.ExecuteOperation>(callInvocation.Payload);
        Assert.Equal(UcliCommandIds.Call, callRequest.Command);
        Assert.Equal("plan-token-1", callRequest.PlanToken);
        Assert.True(callRequest.FailFast);
        Assert.Equal(result.RequestId, callRequest.RequestId);
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
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: UcliPrimitiveOperationNames.ProjectRefresh,
                            Phase: IpcExecuteOperationPhaseNames.Plan,
                            Applied: false,
                            Changed: false,
                            Touched: []),
                    ],
                    errors: [],
                    planToken: "plan-token-1")),
            UnityRequestExecutionResult.Success(
                CreateResponse(
                    status: IpcProtocol.StatusOk,
                    opResults:
                    [
                        new IpcExecuteOperationResult(
                            OpId: "refresh",
                            Op: UcliPrimitiveOperationNames.ProjectRefresh,
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
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore(), timeProvider);

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1200,
                failFast: true),
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
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "refresh",
                        Op: UcliPrimitiveOperationNames.ProjectRefresh,
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
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore(), timeProvider);

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: UnityExecutionMode.Oneshot,
                timeoutMilliseconds: 1200,
                failFast: true),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(1, ipcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
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
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusOk,
                opResults: [],
                errors: [])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore());

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
        Assert.Empty(result.OpResults);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationNotAllowed, error.Code);
        Assert.Equal("refresh", error.OpId);
        Assert.Equal(0, ipcRequestExecutor.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcErrorCodes.InvalidArgument, IpcErrorCodes.InvalidArgument, (int)ApplicationOutcome.InvalidArgument)]
    [InlineData(IpcErrorCodes.PlanTokenInvalid, IpcErrorCodes.PlanTokenInvalid, (int)ApplicationOutcome.InvalidArgument)]
    [InlineData(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, (int)ApplicationOutcome.ToolError)]
    public async Task Execute_WhenTransportExecutionFails_MapsExitCodeFromErrorCode (
        string errorCode,
        string expectedErrorCode,
        int expectedOutcome)
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityRequestExecutionResultTestFactory.Failure(
            "execution failed",
            errorCode));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore());

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal((ApplicationOutcome)expectedOutcome, result.Outcome);
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
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext(config)));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityRequestExecutionResultTestFactory.Failure(
            "execution failed",
            IpcErrorCodes.InternalError));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore());

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
        Assert.Equal(1, ipcRequestExecutor.CallCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(IpcErrorCodes.InternalError, error.Code);
        Assert.Equal("execution failed", error.Message);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenUnityReturnsErrorResponse_PreservesOpResultsAndErrors ()
    {
        var projectContextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateContext()));
        var authorizationService = new SpyOperationAuthorizationService(OperationAuthorizationResult.Allowed());
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityRequestExecutionResult.Success(
            CreateResponse(
                status: IpcProtocol.StatusError,
                opResults:
                [
                    new IpcExecuteOperationResult(
                        OpId: "refresh",
                        Op: UcliPrimitiveOperationNames.ProjectRefresh,
                        Phase: IpcExecuteOperationPhaseNames.Call,
                        Applied: true,
                        Changed: false,
                        Touched: []),
                ],
                errors:
                [
                    new IpcError(IpcErrorCodes.InvalidArgument, "refresh failed", "refresh"),
                ])));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore());

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.InvalidArgument, result.Outcome);
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
        var ipcRequestExecutor = new SpyUnityIpcRequestExecutor(UnityRequestExecutionResult.Success(
            UnityRequestResponseTestFactory.Create(new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "req-1",
                Status: IpcProtocol.StatusOk,
                Payload: JsonSerializer.SerializeToElement(new { invalid = true }),
                Errors: []))));
        var service = new OperationExecuteService(projectContextResolver, authorizationService, ipcRequestExecutor, new TestMutationReadPostconditionStore());

        var result = await service.Execute(
            RefreshOperation,
            CreateInput(
                projectPath: "/repo/UnityProject",
                mode: null,
                timeoutMilliseconds: null,
                failFast: false),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ApplicationOutcome.ToolError, result.Outcome);
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

    private static OperationExecuteInput CreateInput (
        string? projectPath,
        UnityExecutionMode? mode,
        int? timeoutMilliseconds,
        bool failFast)
    {
        return new OperationExecuteInput(
            ProjectPath: projectPath,
            Mode: mode,
            TimeoutMilliseconds: timeoutMilliseconds,
            FailFast: failFast);
    }

    private static UnityRequestResponse CreateResponse (
        string status,
        IReadOnlyList<IpcExecuteOperationResult> opResults,
        IReadOnlyList<IpcError> errors,
        string? planToken = null,
        OperationExecutionReadPostcondition? readPostcondition = null)
    {
        return UnityRequestResponseTestFactory.Create(new IpcResponse(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "req-1",
            Status: status,
            Payload: IpcPayloadCodec.SerializeToElement(new IpcExecuteResponse(opResults)
            {
                PlanToken = planToken,
                ReadPostcondition = readPostcondition == null
                    ? null
                    : new IpcExecuteReadPostcondition(readPostcondition.Requirements.Select(static requirement =>
                        new IpcExecuteReadPostconditionRequirement(requirement.Surface, requirement.MinSafeGeneratedAtUtc)
                        {
                            ScenePath = requirement.ScenePath,
                        }).ToArray()),
            }),
            Errors: errors));
    }

    private static OperationExecutionReadPostcondition CreateReadPostcondition ()
    {
        return ReadPostconditionTestFactory.Create(
        [
            new IpcExecuteReadPostconditionRequirement(
                Surface: IpcExecuteReadPostconditionSurfaceNames.AssetSearch,
                MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T01:02:03+00:00")),
        ]);
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

    private sealed class SpyUnityIpcRequestExecutor : IUnityRequestExecutor
    {
        private readonly Queue<UnityRequestExecutionResult> results;

        private readonly List<Invocation> invocations = [];

        public SpyUnityIpcRequestExecutor (params UnityRequestExecutionResult[] results)
        {
            this.results = new Queue<UnityRequestExecutionResult>(results ?? throw new ArgumentNullException(nameof(results)));
        }

        public Action<InvocationContext>? OnExecute { get; init; }

        public ManualTimeProvider? TimeProvider { get; init; }

        public int CallCount { get; private set; }

        public IReadOnlyList<Invocation> Invocations => invocations;

        public UcliCommand CapturedCommand => invocations[^1].Command;

        public UnityExecutionMode CapturedMode => invocations[^1].Mode;

        public TimeSpan CapturedTimeout => invocations[^1].Timeout;

        public ResolvedUnityProjectContext? CapturedProject => invocations[^1].UnityProject;

        public UnityRequestPayload CapturedPayload => invocations[^1].Payload;

        public ValueTask<UnityRequestExecutionResult> Execute (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            var invocation = new Invocation(command, mode, timeout, unityProject, payload);
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
            UnityRequestPayload Payload);

        public sealed record InvocationContext (
            int Index,
            Invocation Invocation,
            ManualTimeProvider? TimeProvider);
    }
}
