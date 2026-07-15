using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one verifier entry in a build assurance payload. </summary>
internal sealed record BuildVerifierOutput
{
    public BuildVerifierOutput (
        AssuranceVerifierId Id,
        bool Deterministic,
        bool Required,
        IReadOnlyList<UcliCode> PrimaryClaims,
        IReadOnlyList<AssuranceEffect> Effects,
        BuildArtifactKind ReportRef)
    {
        this.Id = Id ?? throw new ArgumentNullException(nameof(Id));
        ArgumentNullException.ThrowIfNull(PrimaryClaims);
        if (PrimaryClaims.Any(static code => code is null))
        {
            throw new ArgumentException("Primary claim codes must not contain null.", nameof(PrimaryClaims));
        }

        ArgumentNullException.ThrowIfNull(Effects);
        if (Effects.Any(static effect => !ContractLiteralCodec.IsDefined(effect)))
        {
            throw new ArgumentException("Effects must contain only defined assurance effects.", nameof(Effects));
        }

        this.Deterministic = Deterministic;
        this.Required = Required;
        this.PrimaryClaims = Array.AsReadOnly(PrimaryClaims.ToArray());
        this.Effects = Array.AsReadOnly(Effects.ToArray());
        this.ReportRef = ReportRef;
    }

    public AssuranceVerifierId Id { get; }

    public AssuranceVerifierKind Kind { get; } = AssuranceVerifierKind.Build;

    public bool Deterministic { get; }

    public bool Required { get; }

    public IReadOnlyList<UcliCode> PrimaryClaims { get; }

    public IReadOnlyList<AssuranceEffect> Effects { get; }

    public BuildArtifactKind ReportRef { get; }
}
