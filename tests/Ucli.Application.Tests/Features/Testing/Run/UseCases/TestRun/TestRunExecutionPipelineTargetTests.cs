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
    [MemberData(nameof(GetExecutionTargetCases))]
    public async Task Execute_WithPreflightResolvedTarget_DispatchesThroughExplicitMode (
        ExecutionTargetCase testCase)
    {
        var configuration = CreateResolvedConfiguration(UnityExecutionMode.Auto);
        var session = CreateArtifactsSession();
        var requestExecutor = new RecordingUnityRequestExecutor(
            UnityRequestExecutionResult.Success(new UnityRequestResponse(
                IpcPayloadCodec.SerializeToElement(new IpcTestRunResponse(0)),
                Array.Empty<OperationExecutionError>())));
        var resultsConverter = new StubUnityResultsConverter(_ =>
            ValueTask.FromResult<UnityResultsConversionResult>(TestRunResultTestValues.CreateConversion(Verdict.Pass)));
        var pipeline = new TestRunExecutionPipeline(
            new StubTestRunArtifactsService(
                prepare: _ => ArtifactsPreparationResult.Success(session),
                complete: (_, _, completionTarget) =>
                {
                    Assert.Equal(testCase.Target, completionTarget);
                    return ArtifactsCompletionResult.Success();
                }),
            requestExecutor,
            resultsConverter,
            StubTestRunArtifactExistenceProbe.ReturningSuccess(),
            requestExecutor);
        var context = new TestRunExecutionContext(
            configuration,
            UcliConfig.CreateDefault(),
            testCase.Target,
            TimeSpan.FromSeconds(30),
            FailFast: false,
            AllowEmptyTestRun: testCase.AllowEmptyTestRun);

        var progressSink = testCase.UseProgressStream ? new CollectingCommandProgressSink() : null;

        _ = Assert.IsType<TestRunExecutionPipelineResult.TestRunExecutionPipelineCompleted>(
            await pipeline.ExecuteAsync(context, progressSink));

        Assert.Equal(testCase.AllowEmptyTestRun, resultsConverter.LastAllowEmptyTestRun);
        Assert.Equal(testCase.ExpectedMode, Assert.Single(requestExecutor.Invocations).Mode);
        if (testCase.UseProgressStream)
        {
            Assert.Equal(testCase.ExpectedMode, Assert.Single(requestExecutor.StreamingInvocations).Mode);
        }
        else
        {
            Assert.Empty(requestExecutor.StreamingInvocations);
        }
    }

    public static TheoryData<ExecutionTargetCase> GetExecutionTargetCases ()
    {
        return new TheoryData<ExecutionTargetCase>
        {
            new(UnityExecutionTarget.Oneshot, UnityExecutionMode.Oneshot, useProgressStream: false, allowEmptyTestRun: false),
            new(UnityExecutionTarget.Oneshot, UnityExecutionMode.Oneshot, useProgressStream: true, allowEmptyTestRun: true),
            new(UnityExecutionTarget.Daemon, UnityExecutionMode.Daemon, useProgressStream: false, allowEmptyTestRun: false),
            new(UnityExecutionTarget.Daemon, UnityExecutionMode.Daemon, useProgressStream: true, allowEmptyTestRun: true),
        };
    }

    public sealed class ExecutionTargetCase
    {
        internal ExecutionTargetCase (
            UnityExecutionTarget target,
            UnityExecutionMode expectedMode,
            bool useProgressStream,
            bool allowEmptyTestRun)
        {
            Target = target;
            ExpectedMode = expectedMode;
            UseProgressStream = useProgressStream;
            AllowEmptyTestRun = allowEmptyTestRun;
        }

        internal UnityExecutionTarget Target { get; }

        internal UnityExecutionMode ExpectedMode { get; }

        internal bool UseProgressStream { get; }

        internal bool AllowEmptyTestRun { get; }
    }
}
