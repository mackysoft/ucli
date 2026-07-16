using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Describes the default execution-state meaning of one error code. </summary>
public sealed record UcliErrorExecutionSemantics
{
    /// <summary> Initializes execution semantics for one error code. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="SafeToRetry" /> is undefined. </exception>
    [JsonConstructor]
    public UcliErrorExecutionSemantics (
        bool? ImpliesNotApplied,
        bool MayBeIndeterminate,
        UcliErrorRetryClass SafeToRetry)
    {
        if (!ContractLiteralCodec.IsDefined(SafeToRetry))
        {
            throw new ArgumentOutOfRangeException(nameof(SafeToRetry), SafeToRetry, "Retry classification must be defined by the error contract.");
        }

        this.ImpliesNotApplied = ImpliesNotApplied;
        this.MayBeIndeterminate = MayBeIndeterminate;
        this.SafeToRetry = SafeToRetry;
    }

    /// <summary> Gets whether the code alone proves that no operation was applied. </summary>
    public bool? ImpliesNotApplied { get; }

    /// <summary> Gets whether the code can leave the request application state unknown. </summary>
    public bool MayBeIndeterminate { get; }

    /// <summary> Gets the default retry classification. </summary>
    public UcliErrorRetryClass SafeToRetry { get; }
}
