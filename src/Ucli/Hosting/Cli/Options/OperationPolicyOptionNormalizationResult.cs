using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for an operation-policy option. </summary>
internal sealed record OperationPolicyOptionNormalizationResult (
    OperationPolicy? Policy,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    public static OperationPolicyOptionNormalizationResult Success (OperationPolicy? policy)
    {
        return new OperationPolicyOptionNormalizationResult(policy, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    public static OperationPolicyOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new OperationPolicyOptionNormalizationResult(null, error);
    }
}
