using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Executes the test-run artifacts, Unity execution, and conversion pipeline. </summary>
internal interface ITestRunExecutionPipeline
{
    /// <summary> Executes one test-run pipeline from prepared configuration. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="progressSink"> The optional command-neutral sink that receives live progress entries. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to pipeline output values. </returns>
    ValueTask<TestRunExecutionPipelineResult> ExecuteAsync (
        TestRunExecutionContext context,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);
}
