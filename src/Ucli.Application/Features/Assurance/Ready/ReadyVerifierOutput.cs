using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one verifier entry in a ready assurance payload. </summary>
internal sealed record ReadyVerifierOutput
{
    public ReadyVerifierOutput (
        AssuranceVerifierId Id,
        bool Deterministic,
        bool Required,
        IReadOnlyList<UcliCode> PrimaryClaims)
    {
        this.Id = Id ?? throw new ArgumentNullException(nameof(Id));
        ArgumentNullException.ThrowIfNull(PrimaryClaims);
        if (PrimaryClaims.Any(static code => code is null))
        {
            throw new ArgumentException("Primary claim codes must not contain null.", nameof(PrimaryClaims));
        }

        this.Deterministic = Deterministic;
        this.Required = Required;
        this.PrimaryClaims = Array.AsReadOnly(PrimaryClaims.ToArray());
    }

    public AssuranceVerifierId Id { get; }

    public AssuranceVerifierKind Kind { get; } = AssuranceVerifierKind.Ready;

    public bool Deterministic { get; }

    public bool Required { get; }

    public IReadOnlyList<UcliCode> PrimaryClaims { get; }

    public IReadOnlyList<AssuranceEffect> Effects { get; } = AssuranceEffectSets.None;
}
