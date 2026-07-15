using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.TestRunServiceTestFactory;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunExecutionPipelineTargetTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData((int)UnityExecutionTarget.Oneshot, (int)UnityExecutionMode.Oneshot, false)]
    [InlineData((int)UnityExecutionTarget.Oneshot, (int)UnityExecutionMode.Oneshot, true)]
    [InlineData((int)UnityExecutionTarget.Daemon, (int)UnityExecutionMode.Daemon, false)]
    [InlineData((int)UnityExecutionTarget.Daemon, (int)UnityExecutionMode.Daemon, true)]
    public async Task Execute_WithPreflightResolvedTarget_DispatchesThroughExplicitMode (
        int targetValue,
        int expectedModeValue,
        bool useProgressStream)
    {
        var target = (UnityExecutionTarget)targetValue;
        var expectedMode = (UnityExecutionMode)expectedModeValue;
        var configuration = CreateResolvedConfiguration(UnityExecutionMode.Auto);
        var session = CreateArtifactsSession();
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(new IpcTestRunResponse(0)),
                Array.Empty<OperationExecutionError>())));
        var pipeline = new TestRunExecutionPipeline(
            new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, completionTarget) =>
                {
                    Assert.Equal(target, completionTarget);
                    return ArtifactsCompletionResult.Success();
                }),
            requestExecutor,
            new StubUnityResultsConverter(_ =>
                ValueTask.FromResult(UnityResultsConversionResult.Success(hasFailedTests: false))),
            new StubTestRunArtifactExistenceProbe(),
            requestExecutor);
        var context = new TestRunExecutionContext(
            configuration,
            UcliConfig.CreateDefault(),
            target,
            TimeSpan.FromSeconds(30),
            FailFast: false,
            AllowEmptyTestRun: false);

        var progressSink = useProgressStream ? new CollectingCommandProgressSink() : null;

        var result = await pipeline.ExecuteAsync(context, progressSink);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedMode, Assert.Single(requestExecutor.Invocations).Mode);
        if (useProgressStream)
        {
            Assert.Equal(expectedMode, Assert.Single(requestExecutor.StreamingInvocations).Mode);
        }
        else
        {
            Assert.Empty(requestExecutor.StreamingInvocations);
        }
    }
}
