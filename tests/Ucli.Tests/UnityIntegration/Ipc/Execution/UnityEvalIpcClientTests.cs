using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityIpcRequestExecutorTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityEvalIpcClientTests
{
    private static readonly IpcEvalPlanRequest PlanRequest = new(
        "context.DeclareNoChanges(); return null;",
        CsEvalSourceKind.Snippet,
        allowDangerous: true,
        allowPlayMode: false);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanDigestDoesNotMatchInput_DoesNotSendCall ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "plan-digest-mismatch");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var transport = new RecordingUnityIpcTransportClient(request => CreateResponse(
            request.RequestId,
            CreatePlan(ProjectIdentity(project), Sha256Digest.Parse(new string('a', 64)), Sha256Digest.Parse(new string('b', 64)))));
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.False(result.CallWasSent);
        Assert.Null(result.Plan);
        Assert.Contains("eval.plan returned an identity", result.Error!.Message, StringComparison.Ordinal);
        IpcRequestAssert.Methods(transport, UnityIpcMethod.EvalPlan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallDigestDoesNotMatchInput_DoesNotPublishCallSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "call-digest-mismatch");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var sourceDigest = EvalExecutionDigestCalculator.ComputeSourceDigest(PlanRequest.Source);
        var executionDigest = EvalExecutionDigestCalculator.ComputeExecutionDigest(PlanRequest.Source, PlanRequest.SourceKind, PlanRequest.AllowDangerous, PlanRequest.AllowPlayMode);
        var transport = new RecordingUnityIpcTransportClient(request =>
        {
            var payload = request.Method == TextVocabulary.GetText(UnityIpcMethod.EvalPlan)
                ? CreatePlan(ProjectIdentity(project), sourceDigest, executionDigest)
                : CreateCall(ProjectIdentity(project), sourceDigest, Sha256Digest.Parse(new string('f', 64)));
            return CreateResponse(request.RequestId, payload);
        });
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.True(result.CallWasSent);
        Assert.NotNull(result.Plan);
        Assert.Null(result.Call);
        Assert.Contains("eval.call returned an identity", result.Error!.Message, StringComparison.Ordinal);
        IpcRequestAssert.Methods(transport, UnityIpcMethod.EvalPlan, UnityIpcMethod.EvalCall);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallTransportDisconnects_DoesNotResendAndMarksCallAsSent ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "call-disconnect");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var sourceDigest = EvalExecutionDigestCalculator.ComputeSourceDigest(PlanRequest.Source);
        var executionDigest = EvalExecutionDigestCalculator.ComputeExecutionDigest(PlanRequest.Source, PlanRequest.SourceKind, PlanRequest.AllowDangerous, PlanRequest.AllowPlayMode);
        var transport = new RecordingUnityIpcTransportClient(request =>
        {
            if (request.Method == TextVocabulary.GetText(UnityIpcMethod.EvalPlan))
            {
                return CreateResponse(request.RequestId, CreatePlan(ProjectIdentity(project), sourceDigest, executionDigest));
            }

            throw new IOException("The eval.call connection closed before its response was received.");
        });
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.True(result.CallWasSent);
        Assert.NotNull(result.Plan);
        Assert.Null(result.Call);
        Assert.Null(result.ErrorResponse);
        Assert.NotNull(result.Error);
        IpcRequestAssert.Methods(transport, UnityIpcMethod.EvalPlan, UnityIpcMethod.EvalCall);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanReturnsStructuredError_PreservesItAndDoesNotSendCall ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "plan-structured-error");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var errorPayload = new IpcEvalErrorResponse(
            ProjectIdentity(project),
            CsEvalPhase.Plan,
            ExecutionApplicationState.NotApplied,
            null,
            null);
        var transport = new RecordingUnityIpcTransportClient(request => new IpcResponse(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcResponseStatus.Error,
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new IpcError(UcliCoreErrorCodes.InvalidArgument, "plan rejected", null)]));
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.False(result.CallWasSent);
        Assert.NotNull(result.ErrorResponse);
        Assert.Equal(CsEvalPhase.Plan, result.ErrorResponse!.Phase);
        IpcRequestAssert.Methods(transport, UnityIpcMethod.EvalPlan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallReturnsStructuredError_PreservesItAndDoesNotPublishSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "call-structured-error");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var sourceDigest = EvalExecutionDigestCalculator.ComputeSourceDigest(PlanRequest.Source);
        var executionDigest = EvalExecutionDigestCalculator.ComputeExecutionDigest(PlanRequest.Source, PlanRequest.SourceKind, PlanRequest.AllowDangerous, PlanRequest.AllowPlayMode);
        var callError = new IpcEvalErrorResponse(
            ProjectIdentity(project),
            CsEvalPhase.Call,
            ExecutionApplicationState.Indeterminate,
            null,
            new ExecutionReadPostcondition([]));
        var transport = new RecordingUnityIpcTransportClient(request => request.Method == TextVocabulary.GetText(UnityIpcMethod.EvalPlan)
            ? CreateResponse(request.RequestId, CreatePlan(ProjectIdentity(project), sourceDigest, executionDigest))
            : new IpcResponse(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcResponseStatus.Error,
                IpcPayloadCodec.SerializeToElement(callError),
                [new IpcError(UcliCoreErrorCodes.InvalidArgument, "entry failed", null)]));
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.True(result.CallWasSent);
        Assert.NotNull(result.Plan);
        Assert.Null(result.Call);
        Assert.NotNull(result.ErrorResponse);
        Assert.Equal(CsEvalPhase.Call, result.ErrorResponse!.Phase);
        Assert.Equal(ExecutionApplicationState.Indeterminate, result.ErrorResponse.ApplicationState);
        IpcRequestAssert.Methods(transport, UnityIpcMethod.EvalPlan, UnityIpcMethod.EvalCall);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPlanErrorReportsCallPhase_RejectsTheContradictoryEvidence ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "plan-error-phase-mismatch");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var errorPayload = new IpcEvalErrorResponse(
            ProjectIdentity(project),
            CsEvalPhase.Call,
            ExecutionApplicationState.Indeterminate,
            null,
            null);
        var transport = new RecordingUnityIpcTransportClient(request => new IpcResponse(
            IpcProtocol.CurrentVersion,
            request.RequestId,
            IpcResponseStatus.Error,
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new IpcError(UcliCoreErrorCodes.InvalidArgument, "contradictory phase", null)]));
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.False(result.CallWasSent);
        Assert.Null(result.ErrorResponse);
        Assert.Contains("does not match the request", result.Error!.Message, StringComparison.Ordinal);
        IpcRequestAssert.Methods(transport, UnityIpcMethod.EvalPlan);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallErrorReportsAnotherProject_RejectsTheContradictoryEvidence ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "call-error-project-mismatch");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var sourceDigest = EvalExecutionDigestCalculator.ComputeSourceDigest(PlanRequest.Source);
        var executionDigest = EvalExecutionDigestCalculator.ComputeExecutionDigest(PlanRequest.Source, PlanRequest.SourceKind, PlanRequest.AllowDangerous, PlanRequest.AllowPlayMode);
        var anotherProject = new UnityProjectIdentity(
            ProjectIdentity(project).ProjectPath,
            ProjectFingerprintTestFactory.Create("another-project"),
            ProjectIdentity(project).UnityVersion);
        var callError = new IpcEvalErrorResponse(
            anotherProject,
            CsEvalPhase.Call,
            ExecutionApplicationState.Indeterminate,
            null,
            null);
        var transport = new RecordingUnityIpcTransportClient(request => request.Method == TextVocabulary.GetText(UnityIpcMethod.EvalPlan)
            ? CreateResponse(request.RequestId, CreatePlan(ProjectIdentity(project), sourceDigest, executionDigest))
            : new IpcResponse(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcResponseStatus.Error,
                IpcPayloadCodec.SerializeToElement(callError),
                [new IpcError(UcliCoreErrorCodes.InvalidArgument, "wrong project", null)]));
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.True(result.CallWasSent);
        Assert.NotNull(result.Plan);
        Assert.Null(result.ErrorResponse);
        Assert.Contains("does not match the request", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallErrorPartialDigestDoesNotMatchInput_RejectsTheContradictoryEvidence ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-eval-ipc-client", "call-error-digest-mismatch");
        var project = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var sourceDigest = EvalExecutionDigestCalculator.ComputeSourceDigest(PlanRequest.Source);
        var executionDigest = EvalExecutionDigestCalculator.ComputeExecutionDigest(PlanRequest.Source, PlanRequest.SourceKind, PlanRequest.AllowDangerous, PlanRequest.AllowPlayMode);
        var partial = new CsEvalPartialErrorResult(
            Sha256Digest.Parse(new string('f', 64)),
            PlanRequest.SourceKind,
            "Snippet.Run",
            executionDigest,
            new CsEvalPlanCompileResult(true, []),
            null,
            null,
            null,
            null);
        var callError = new IpcEvalErrorResponse(
            ProjectIdentity(project),
            CsEvalPhase.Call,
            ExecutionApplicationState.Indeterminate,
            partial,
            null);
        var transport = new RecordingUnityIpcTransportClient(request => request.Method == TextVocabulary.GetText(UnityIpcMethod.EvalPlan)
            ? CreateResponse(request.RequestId, CreatePlan(ProjectIdentity(project), sourceDigest, executionDigest))
            : new IpcResponse(
                IpcProtocol.CurrentVersion,
                request.RequestId,
                IpcResponseStatus.Error,
                IpcPayloadCodec.SerializeToElement(callError),
                [new IpcError(UcliCoreErrorCodes.InvalidArgument, "wrong digest", null)]));
        var client = CreateDaemonClient(project, transport);

        var result = await client.ExecuteAsync(
            UnityExecutionMode.Daemon,
            TimeSpan.FromSeconds(30),
            project,
            PlanRequest,
            failFast: false);

        Assert.False(result.IsSuccess);
        Assert.True(result.CallWasSent);
        Assert.Null(result.ErrorResponse);
        Assert.Contains("does not match the request", result.Error!.Message, StringComparison.Ordinal);
    }

    private static UnityEvalIpcClient CreateDaemonClient (
        ResolvedUnityProjectContext project,
        RecordingUnityIpcTransportClient transport)
    {
        var modeDecision = new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(
            new UnityExecutionModeDecision(
                UnityExecutionMode.Daemon,
                true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromSeconds(30))));
        var readiness = new RecordingDaemonPingInfoClient(CreatePingPayload(
            UnityEditorLifecycleState.Ready,
            project.ProjectFingerprint));
        var clients = CreateClients(
            transport,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Oneshot must not be used for daemon eval.")),
            new QueuedDaemonSessionStore(CreateSessionReadResult("eval-session-token")),
            new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(new StubUnityBatchmodeProcessHandle())));
        return new UnityEvalIpcClient(
            new UnityIpcRequestBuilder(),
            new UnityIpcExecutionTargetResolver(modeDecision, new UnityIpcPluginVerifier(new RecordingUnityUcliPluginLocator())),
            new UnityIpcClientSelector(clients),
            new UnityDaemonReadinessGate(readiness, TimeProvider.System),
            TimeProvider.System);
    }

    private static IpcResponse CreateResponse (Guid requestId, IpcEvalResponse payload) => new(
        IpcProtocol.CurrentVersion,
        requestId,
        IpcResponseStatus.Ok,
        IpcPayloadCodec.SerializeToElement(payload),
        []);

    private static IpcEvalResponse CreatePlan (UnityProjectIdentity project, Sha256Digest sourceDigest, Sha256Digest executionDigest) => new(
        project,
        CsEvalPhase.Plan,
        ExecutionApplicationState.NotApplied,
        new CsEvalPlanSuccessResult(sourceDigest, CsEvalSourceKind.Snippet, "Snippet.Run", executionDigest, new CsEvalPlanCompileResult(true, [])),
        "plan-token",
        null);

    private static IpcEvalResponse CreateCall (UnityProjectIdentity project, Sha256Digest sourceDigest, Sha256Digest executionDigest) => new(
        project,
        CsEvalPhase.Call,
        ExecutionApplicationState.Applied,
        new CsEvalCallSuccessResult(
            sourceDigest,
            CsEvalSourceKind.Snippet,
            "Snippet.Run",
            executionDigest,
            new CsEvalPlanCompileResult(true, []),
            1,
            [],
            CsEvalReturnValue.Null(),
            new CsEvalTouchedResources(true, [], [], [], [])),
        null,
        CreateCallReadPostcondition());

    private static UnityProjectIdentity ProjectIdentity (ResolvedUnityProjectContext project) => new(
        project.UnityProjectRoot.ToString(),
        project.ProjectFingerprint,
        project.UnityVersion);

    private static ExecutionReadPostcondition CreateCallReadPostcondition () => new(
    [
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.AssetSearch, DateTimeOffset.UnixEpoch, null),
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.GuidPath, DateTimeOffset.UnixEpoch, null),
        new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.SceneTreeLite, DateTimeOffset.UnixEpoch, null),
    ]);
}
