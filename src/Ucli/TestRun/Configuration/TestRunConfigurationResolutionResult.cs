using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Represents one test-run configuration resolution result. </summary>
/// <param name="Configuration"> The resolved configuration on success; otherwise <see langword="null" />. </param>
/// <param name="Errors"> The structured errors on failure; otherwise an empty array. </param>
internal sealed record TestRunConfigurationResolutionResult (
    ResolvedTestRunConfiguration? Configuration,
    IReadOnlyList<ExecutionError> Errors)
{
    /// <summary> Gets a value indicating whether configuration resolution succeeded. </summary>
    public bool IsSuccess => Configuration is not null && Errors.Count == 0;

    /// <summary> Creates a successful configuration resolution result. </summary>
    /// <param name="configuration"> The resolved configuration. </param>
    /// <returns> The successful result. </returns>
    public static TestRunConfigurationResolutionResult Success (ResolvedTestRunConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new TestRunConfigurationResolutionResult(configuration, Array.Empty<ExecutionError>());
    }

    /// <summary> Creates a failed configuration resolution result. </summary>
    /// <param name="errors"> The structured errors. </param>
    /// <returns> The failed result. </returns>
    public static TestRunConfigurationResolutionResult Failure (IReadOnlyList<ExecutionError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new TestRunConfigurationResolutionResult(null, errors);
    }
}