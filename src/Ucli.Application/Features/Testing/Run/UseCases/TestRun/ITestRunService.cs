using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;

/// <summary> Executes core test-run flow and returns normalized result payload values. </summary>
internal interface ITestRunService
{
    /// <summary> Executes one test-run operation. </summary>
    /// <param name="input"> The interpreted test-run command input. </param>
    /// <param name="progressSink"> The optional command-neutral sink that receives live progress entries. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the test-run service result. </returns>
    ValueTask<TestRunServiceResult> ExecuteAsync (
        TestRunCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);
}
