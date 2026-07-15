using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents one verifier entry in a compile assurance payload. </summary>
internal sealed record CompileVerifierOutput
{
    public CompileVerifierOutput (
        AssuranceVerifierId Id,
        bool Deterministic,
        bool Required,
        IReadOnlyList<UcliCode> PrimaryClaims,
        IReadOnlyList<AssuranceEffect> Effects,
        string ReportRef)
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

    public AssuranceVerifierKind Kind { get; } = AssuranceVerifierKind.Compile;

    public bool Deterministic { get; }

    public bool Required { get; }

    public IReadOnlyList<UcliCode> PrimaryClaims { get; }

    public IReadOnlyList<AssuranceEffect> Effects { get; }

    public string ReportRef { get; }
}
