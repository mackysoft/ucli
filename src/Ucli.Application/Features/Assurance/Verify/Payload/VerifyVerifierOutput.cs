using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one verifier entry in a verify assurance payload. </summary>
internal sealed record VerifyVerifierOutput
{
    public VerifyVerifierOutput (
        AssuranceVerifierId Id,
        AssuranceVerifierKind Kind,
        bool Deterministic,
        bool Required,
        IReadOnlyList<UcliCode> PrimaryClaims,
        IReadOnlyList<AssuranceEffect> Effects)
    {
        this.Id = Id ?? throw new ArgumentNullException(nameof(Id));
        if (!TextVocabulary.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Verifier kind must be defined by the assurance contract.");
        }

        ArgumentNullException.ThrowIfNull(PrimaryClaims);
        if (PrimaryClaims.Any(static code => code is null))
        {
            throw new ArgumentException("Primary claim codes must not contain null.", nameof(PrimaryClaims));
        }

        ArgumentNullException.ThrowIfNull(Effects);
        if (Effects.Any(static effect => !TextVocabulary.IsDefined(effect)))
        {
            throw new ArgumentException("Effects must contain only defined assurance effects.", nameof(Effects));
        }

        this.Kind = Kind;
        this.Deterministic = Deterministic;
        this.Required = Required;
        this.PrimaryClaims = Array.AsReadOnly(PrimaryClaims.ToArray());
        this.Effects = Array.AsReadOnly(Effects.ToArray());
    }

    public AssuranceVerifierId Id { get; }

    public AssuranceVerifierKind Kind { get; }

    public bool Deterministic { get; }

    public bool Required { get; }

    public IReadOnlyList<UcliCode> PrimaryClaims { get; }

    public IReadOnlyList<AssuranceEffect> Effects { get; }

    /// <summary> Gets the optional report reference produced by this verifier. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReportRef { get; init; }
}
