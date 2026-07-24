using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--for</c> ready option. </summary>
internal sealed record ReadyTargetOptionNormalizationResult
{
    private ReadyTargetOptionNormalizationResult (
        ReadyTarget? target,
        ExecutionError? error)
    {
        Target = target;
        Error = error;
    }

    public ReadyTarget? Target { get; }

    public ExecutionError? Error { get; }

    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Target.HasValue;

    /// <summary> Creates a successful normalization result. </summary>
    public static ReadyTargetOptionNormalizationResult Success (ReadyTarget target)
    {
        if (!TextVocabulary.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Ready target must be defined.");
        }

        return new ReadyTargetOptionNormalizationResult(target, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    public static ReadyTargetOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadyTargetOptionNormalizationResult(null, error);
    }
}
