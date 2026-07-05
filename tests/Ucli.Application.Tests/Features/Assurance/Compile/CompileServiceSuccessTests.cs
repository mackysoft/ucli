using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Assurance;
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
        Assert.Equal(CompileVerdictValues.Pass, output.Verdict);
        Assert.Equal("run-1", output.Compile.RunId);
        Assert.Equal("oneshot", output.ResolvedMode);
        Assert.Equal("transientProbe", output.SessionKind);
        Assert.Equal(3, output.Claims.Count);
        UnityRequestExecutorInvocationAssert.CompileOnce(
            unityRequestExecutor,
            expectedRunId: "run-1");
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
