using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Represents a verify profile step progress stream payload. </summary>
public sealed record VerifyStepProgressEntry
{
    /// <summary> Initializes one verify profile step progress entry. </summary>
    [JsonConstructor]
    public VerifyStepProgressEntry (
        VerifyStepKind Kind,
        bool Required,
        IReadOnlyList<AssuranceEffect> Effects,
        string? SkipReason)
    {
        if (!ContractLiteralCodec.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Verify step kind must be defined.");
        }

        if (Effects == null)
        {
            throw new ArgumentNullException(nameof(Effects));
        }
        if (Effects.Any(static effect => !ContractLiteralCodec.IsDefined(effect)))
        {
            throw new ArgumentException("Effects must contain only defined assurance effects.", nameof(Effects));
        }

        this.Kind = Kind;
        this.Required = Required;
        this.Effects = Effects.Count == 0
            ? Array.Empty<AssuranceEffect>()
            : Array.AsReadOnly(Effects.ToArray());
        this.SkipReason = SkipReason;
    }

    public VerifyStepKind Kind { get; }

    public bool Required { get; }

    public IReadOnlyList<AssuranceEffect> Effects { get; }

    public string? SkipReason { get; }
}
