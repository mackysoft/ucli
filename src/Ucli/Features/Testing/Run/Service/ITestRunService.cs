namespace MackySoft.Ucli.Features.Testing.Run.Service;

/// <summary> Executes core test-run flow and returns normalized result payload values. </summary>
internal interface ITestRunService
{
    /// <summary> Executes one test-run operation. </summary>
    /// <param name="input"> The raw test-run command input. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the test-run service result. </returns>
    ValueTask<TestRunServiceResult> Execute (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default);
}