namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;

/// <summary> Maps established pipeline variants into Test Run service outcomes. </summary>
internal sealed class TestRunResultMapper : ITestRunResultMapper
{
    public TestRunServiceResult Map (TestRunExecutionPipelineResult pipelineResult)
    {
        ArgumentNullException.ThrowIfNull(pipelineResult);

        return pipelineResult switch
        {
            TestRunExecutionPipelineResult.TestRunExecutionPipelineCompleted completed =>
                TestRunServiceResult.Completed(
                    completed.Conversion,
                    completed.Session),
            TestRunExecutionPipelineResult.TestRunExecutionPipelineFailureBeforeArtifacts beforeArtifacts =>
                TestRunServiceErrorMapper.MapCommandError(beforeArtifacts.Error),
            TestRunExecutionPipelineResult.TestRunExecutionPipelineFailureWithFinalizationFailure
                withFinalizationFailure =>
                TestRunServiceResult.AfterRunCreationErrorWithFinalizationFailure(
                    withFinalizationFailure.PrimaryFailure,
                    withFinalizationFailure.FinalizationFailure,
                    withFinalizationFailure.Session),
            TestRunExecutionPipelineResult.TestRunExecutionPipelineFailureAfterArtifacts afterArtifacts =>
                TestRunServiceResult.AfterRunCreationError(
                    afterArtifacts.PrimaryFailure,
                    afterArtifacts.Session),
            _ => throw new ArgumentOutOfRangeException(
                nameof(pipelineResult),
                pipelineResult.GetType(),
                "Test Run pipeline result type must have an explicit service projection."),
        };
    }

}
