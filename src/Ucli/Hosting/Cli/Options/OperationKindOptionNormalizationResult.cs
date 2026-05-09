using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for an operation-kind option. </summary>
internal sealed record OperationKindOptionNormalizationResult (
    UcliOperationKind? Kind,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    public static OperationKindOptionNormalizationResult Success (UcliOperationKind? kind)
    {
        return new OperationKindOptionNormalizationResult(kind, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    public static OperationKindOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new OperationKindOptionNormalizationResult(null, error);
    }
}
