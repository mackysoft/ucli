using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--for</c> ready option. </summary>
internal sealed record ReadyTargetOptionNormalizationResult (
    ReadyTarget? Target,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Target.HasValue && Error is null;

    /// <summary> Creates a successful normalization result. </summary>
    public static ReadyTargetOptionNormalizationResult Success (ReadyTarget target)
    {
        return new ReadyTargetOptionNormalizationResult(target, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    public static ReadyTargetOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadyTargetOptionNormalizationResult(null, error);
    }
}
