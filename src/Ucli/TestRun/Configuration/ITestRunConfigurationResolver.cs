namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Resolves test-run configuration from CLI input, profile values, and defaults. </summary>
internal interface ITestRunConfigurationResolver
{
    /// <summary> Resolves test-run configuration for one execution request. </summary>
    /// <param name="input"> The raw command input. </param>
    /// <returns> The configuration resolution result. </returns>
    TestRunConfigurationResolutionResult Resolve (TestRunCommandInput input);
}