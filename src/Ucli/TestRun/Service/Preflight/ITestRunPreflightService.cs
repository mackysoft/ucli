using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Service.Preflight;

/// <summary> Resolves command input and execution mode prerequisites before test-run pipeline execution. </summary>
internal interface ITestRunPreflightService
{
    /// <summary> Executes one preflight flow and returns either resolved configuration or failure output. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to preflight result values. </returns>
    ValueTask<TestRunPreflightResult> Execute (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default);
}