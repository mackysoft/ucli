using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--onStartupBlocked</c> option. </summary>
internal sealed record DaemonStartupBlockedProcessPolicyOptionNormalizationResult (
    DaemonStartupBlockedProcessPolicy Policy,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="policy"> The normalized startup-blocked process policy. </param>
    /// <returns> The successful result. </returns>
    public static DaemonStartupBlockedProcessPolicyOptionNormalizationResult Success (
        DaemonStartupBlockedProcessPolicy policy)
    {
        return new DaemonStartupBlockedProcessPolicyOptionNormalizationResult(policy, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    public static DaemonStartupBlockedProcessPolicyOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonStartupBlockedProcessPolicyOptionNormalizationResult(
            DaemonStartupBlockedProcessPolicy.Auto,
            error);
    }
}
