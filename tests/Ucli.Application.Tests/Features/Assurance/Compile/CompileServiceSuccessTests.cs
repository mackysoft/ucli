using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Compile;

public sealed class CompileServiceSuccessTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSuccessfulCompileResponse_ReturnsPassOutput ()
    {
        var unityRequestExecutor = new RecordingUnityRequestExecutor(CreateCompileResponseResult(CreateSummary()));
        var progressSink = new CollectingCommandProgressSink();
        var service = CreateService(unityRequestExecutor: unityRequestExecutor);

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000), progressSink);

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(AssuranceVerdict.Pass, output.Verdict);
        Assert.Equal(RunId, output.Compile.RunId);
        Assert.Equal(AssuranceResolvedExecutionMode.Oneshot, output.ResolvedMode);
        Assert.Equal(AssuranceSessionKind.TransientProbe, output.SessionKind);
        Assert.Equal(3, output.Claims.Count);
        UnityRequestExecutorInvocationAssert.CompileOnce(
            unityRequestExecutor,
            expectedRunId: RunId);
        EventSequenceAssert.EmittedEventsInOrder(
            progressSink.Entries,
            CompileProgressEventNames.Started,
            CompileProgressEventNames.RefreshStarted,
            CompileProgressEventNames.Completed);
        CompileProgressAssert.SuccessfulCompileProgressPayloads(progressSink);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithoutTimeoutOption_UsesCompileConfigOverride ()
    {
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.Compile.Name] = 4321,
        };
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
        var unityRequestExecutor = new RecordingUnityRequestExecutor(CreateCompileResponseResult(CreateSummary()));
        var service = CreateService(
            projectContextResolver: new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create(
                config: config))),
            unityRequestExecutor: unityRequestExecutor,
            timeProvider: new ManualTimeProvider());

        var result = await service.ExecuteAsync(new CompileCommandInput(
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: null));

        Assert.True(result.IsSuccess);
        UnityRequestExecutorInvocationAssert.CompileOnce(
            unityRequestExecutor,
            expectedTimeout: TimeSpan.FromMilliseconds(4321));
    }
}
