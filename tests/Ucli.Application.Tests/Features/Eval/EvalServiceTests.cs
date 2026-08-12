using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Eval;

public sealed class EvalServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenEnabled_UsesTheDedicatedEvalClientWithTheExplicitSourceKind ()
    {
        var context = ProjectContextTestFactory.Create(config: UcliConfig.CreateDefault() with { EvalEnabled = true });
        var client = new RecordingEvalClient(UnityEvalExecutionResult.PlanFailure(ExecutionError.Timeout("Plan timed out.")));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = new EvalService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            client,
            postconditionStore);

        var result = await service.ExecuteAsync(
            Guid.Parse("eb2bfca7-7f39-46ca-975d-1e70dd31ef07"),
            new EvalCommandInput(
                ProjectPath: null,
                Mode: UnityExecutionMode.Auto,
                TimeoutMilliseconds: 1000,
                AllowDangerous: true,
                AllowPlayMode: false,
                FailFast: true,
                Source: "public static class Evaluation { }",
                SourceKind: CsEvalSourceKind.CompilationUnit));

        var invocation = Assert.Single(client.Invocations);
        Assert.Equal(CsEvalSourceKind.CompilationUnit, invocation.Request.SourceKind);
        Assert.Equal("public static class Evaluation { }", invocation.Request.Source);
        Assert.True(invocation.Request.AllowDangerous);
        Assert.True(invocation.FailFast);
        Assert.False(result.IsSuccess);
        Assert.Equal("eb2bfca7-7f39-46ca-975d-1e70dd31ef07", result.RequestId?.ToString());
        Assert.Equal(context.UnityProject.UnityProjectRoot.Value, result.Project?.ProjectPath);
        Assert.Empty(postconditionStore.InvalidationInvocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenEvalIsDisabled_RejectsBeforeCallingUnityAndRetainsTheResolvedProject ()
    {
        var context = ProjectContextTestFactory.Create(config: UcliConfig.CreateDefault());
        var client = new RecordingEvalClient(UnityEvalExecutionResult.PlanFailure(ExecutionError.Timeout("Must not execute.")));
        var service = new EvalService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            client,
            new TestMutationReadPostconditionStore());

        var result = await service.ExecuteAsync(
            Guid.Parse("9bcaf634-d329-4f2b-bf7c-53009878ae4c"),
            new EvalCommandInput(null, null, null, true, false, false, "return null;", CsEvalSourceKind.Snippet));

        Assert.Empty(client.Invocations);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Project);
        Assert.Equal("9bcaf634-d329-4f2b-bf7c-53009878ae4c", result.RequestId?.ToString());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(ExecutionApplicationState.Applied)]
    [InlineData(ExecutionApplicationState.Indeterminate)]
    [InlineData(ExecutionApplicationState.Unknown)]
    public async Task ExecuteAsync_WhenCallWasSentWithoutSafeReadPostcondition_InvalidatesAllPersistedReadSurfaces (
        ExecutionApplicationState applicationState)
    {
        var context = ProjectContextTestFactory.Create(config: UcliConfig.CreateDefault() with { EvalEnabled = true });
        var project = new UnityProjectIdentity(
            context.UnityProject.UnityProjectRoot.Value,
            context.UnityProject.ProjectFingerprint,
            context.UnityProject.UnityVersion);
        var plan = CreatePlan(project);
        var client = new RecordingEvalClient(UnityEvalExecutionResult.CallFailure(
            plan,
            ExecutionError.Timeout("The eval.call response was not recovered."),
            callWasSent: true,
            new IpcEvalErrorResponse(project, CsEvalPhase.Call, applicationState, null, null)));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = new EvalService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            client,
            postconditionStore);

        var result = await service.ExecuteAsync(
            Guid.Parse("9d7b8d1a-3d7f-4938-bc9e-535be6c2d536"),
            CreateEnabledInput());

        Assert.True(result.CallWasSent);
        var invocation = Assert.Single(postconditionStore.InvalidationInvocations);
        Assert.Equal(context.UnityProject.RepositoryRoot.Value, invocation.StorageRoot.Value);
        Assert.Equal(context.UnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
        Assert.Equal(CancellationToken.None, invocation.CancellationToken);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenCallWasSentAndNotApplied_DoesNotInvalidatePersistedReadSurfaces ()
    {
        var context = ProjectContextTestFactory.Create(config: UcliConfig.CreateDefault() with { EvalEnabled = true });
        var project = new UnityProjectIdentity(
            context.UnityProject.UnityProjectRoot.Value,
            context.UnityProject.ProjectFingerprint,
            context.UnityProject.UnityVersion);
        var plan = CreatePlan(project);
        var client = new RecordingEvalClient(UnityEvalExecutionResult.CallFailure(
            plan,
            ExecutionError.InvalidArgument("The eval.call entry was not invoked."),
            callWasSent: true,
            new IpcEvalErrorResponse(project, CsEvalPhase.Call, ExecutionApplicationState.NotApplied, null, null)));
        var postconditionStore = new TestMutationReadPostconditionStore();
        var service = new EvalService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            client,
            postconditionStore);

        await service.ExecuteAsync(
            Guid.Parse("e84d6478-c06e-4e0c-87fe-bc9256c8e583"),
            CreateEnabledInput());

        Assert.Empty(postconditionStore.InvalidationInvocations);
    }

    private static EvalCommandInput CreateEnabledInput () => new(
        ProjectPath: null,
        Mode: UnityExecutionMode.Auto,
        TimeoutMilliseconds: 1000,
        AllowDangerous: true,
        AllowPlayMode: false,
        FailFast: false,
        Source: "return null;",
        SourceKind: CsEvalSourceKind.Snippet);

    private static IpcEvalResponse CreatePlan (UnityProjectIdentity project) => new(
        project,
        CsEvalPhase.Plan,
        ExecutionApplicationState.NotApplied,
        new CsEvalPlanSuccessResult(
            Sha256Digest.Parse(new string('a', 64)),
            CsEvalSourceKind.Snippet,
            "Snippet.Run",
            Sha256Digest.Parse(new string('b', 64)),
            new CsEvalPlanCompileResult(succeeded: true, diagnostics: [])),
        "plan-token",
        null);

    private sealed class RecordingEvalClient : IUnityEvalClient
    {
        private readonly UnityEvalExecutionResult result;

        public RecordingEvalClient (UnityEvalExecutionResult result)
        {
            this.result = result;
        }

        public List<Invocation> Invocations { get; } = [];

        public ValueTask<UnityEvalExecutionResult> ExecuteAsync (
            UnityExecutionMode mode,
            TimeSpan timeout,
            ResolvedUnityProjectContext unityProject,
            IpcEvalPlanRequest planRequest,
            bool failFast,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(mode, timeout, unityProject, planRequest, failFast));
            return ValueTask.FromResult(result);
        }

        internal sealed record Invocation (
            UnityExecutionMode Mode,
            TimeSpan Timeout,
            ResolvedUnityProjectContext Project,
            IpcEvalPlanRequest Request,
            bool FailFast);
    }
}
