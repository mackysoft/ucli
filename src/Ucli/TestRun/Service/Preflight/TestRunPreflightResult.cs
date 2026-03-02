using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Service.Preflight;

/// <summary> Represents one preflight outcome for test-run execution. </summary>
/// <param name="Configuration"> The resolved configuration when preflight succeeds; otherwise <see langword="null" />. </param>
/// <param name="Failure"> The failure output when preflight fails; otherwise <see langword="null" />. </param>
internal sealed record TestRunPreflightResult (
    ResolvedTestRunConfiguration? Configuration,
    TestRunServiceResult? Failure)
{
    /// <summary> Gets a value indicating whether preflight succeeded. </summary>
    public bool IsSuccess => Configuration is not null && Failure is null;

    /// <summary> Creates one successful preflight result. </summary>
    /// <param name="configuration"> The resolved configuration. </param>
    /// <returns> The successful preflight result. </returns>
    public static TestRunPreflightResult Success (ResolvedTestRunConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new TestRunPreflightResult(configuration, null);
    }

    /// <summary> Creates one failed preflight result. </summary>
    /// <param name="failure"> The failure output. </param>
    /// <returns> The failed preflight result. </returns>
    public static TestRunPreflightResult FailureResult (TestRunServiceResult failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new TestRunPreflightResult(null, failure);
    }
}