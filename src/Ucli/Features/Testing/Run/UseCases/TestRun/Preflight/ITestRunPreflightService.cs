using MackySoft.Ucli.Features.Testing.Run.Configuration;

using MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun;

namespace MackySoft.Ucli.Features.Testing.Run.UseCases.TestRun.Preflight;

/// <summary> Resolves command input and execution mode prerequisites before test-run pipeline execution. </summary>
internal interface ITestRunPreflightService
{
    /// <summary> Executes one preflight flow and returns either resolved configuration or failure output. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to preflight result values. </returns>
    ValueTask<TestRunPreflightResult> Execute (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default);
}
