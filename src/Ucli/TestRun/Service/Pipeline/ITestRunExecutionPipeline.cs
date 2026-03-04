using MackySoft.Ucli.TestRun.Service.Preflight;

namespace MackySoft.Ucli.TestRun.Service.Pipeline;

/// <summary> Executes the test-run artifacts, Unity execution, and conversion pipeline. </summary>
internal interface ITestRunExecutionPipeline
{
    /// <summary> Executes one test-run pipeline from prepared configuration. </summary>
    /// <param name="context"> The preflight-resolved execution context. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to pipeline output values. </returns>
    ValueTask<TestRunExecutionPipelineResult> Execute (
        TestRunExecutionContext context,
        CancellationToken cancellationToken = default);
}