using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
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
        var service = new EvalService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            client);

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
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenEvalIsDisabled_RejectsBeforeCallingUnityAndRetainsTheResolvedProject ()
    {
        var context = ProjectContextTestFactory.Create(config: UcliConfig.CreateDefault());
        var client = new RecordingEvalClient(UnityEvalExecutionResult.PlanFailure(ExecutionError.Timeout("Must not execute.")));
        var service = new EvalService(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(context)),
            client);

        var result = await service.ExecuteAsync(
            Guid.Parse("9bcaf634-d329-4f2b-bf7c-53009878ae4c"),
            new EvalCommandInput(null, null, null, true, false, false, "return null;", CsEvalSourceKind.Snippet));

        Assert.Empty(client.Invocations);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Project);
        Assert.Equal("9bcaf634-d329-4f2b-bf7c-53009878ae4c", result.RequestId?.ToString());
    }

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
