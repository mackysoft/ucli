namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Resolves test-run configuration from CLI input, profile values, and defaults. </summary>
internal interface ITestRunConfigurationResolver
{
    /// <summary> Resolves test-run configuration for one execution request. </summary>
    /// <param name="input"> The raw command input. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the configuration resolution result. </returns>
    ValueTask<TestRunConfigurationResolutionResult> Resolve (
        TestRunCommandInput input,
        CancellationToken cancellationToken = default);
}