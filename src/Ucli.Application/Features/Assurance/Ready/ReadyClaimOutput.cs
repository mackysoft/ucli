using System.Collections.ObjectModel;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one claim entry in a ready assurance payload. </summary>
internal sealed record ReadyClaimOutput
{
    public ReadyClaimOutput (
        UcliCode Id,
        AssuranceClaimStatus Status,
        AssuranceCoverage Coverage,
        bool Required,
        AssuranceVerifierId VerifierRef,
        string Statement,
        IReadOnlyDictionary<string, object?> Subject,
        ReadyClaimValidityOutput Validity,
        IReadOnlyList<ReadyEvidenceOutput> Evidence,
        IReadOnlyList<ReadyResidualRiskOutput> ResidualRisks)
    {
        this.Id = Id ?? throw new ArgumentNullException(nameof(Id));
        if (!ContractLiteralCodec.IsDefined(Status))
        {
            throw new ArgumentOutOfRangeException(nameof(Status), Status, "Claim status must be defined by the assurance contract.");
        }

        if (!ContractLiteralCodec.IsDefined(Coverage))
        {
            throw new ArgumentOutOfRangeException(nameof(Coverage), Coverage, "Claim coverage must be defined by the assurance contract.");
        }

        this.Status = Status;
        this.Coverage = Coverage;
        this.Required = Required;
        this.VerifierRef = VerifierRef ?? throw new ArgumentNullException(nameof(VerifierRef));
        this.Statement = string.IsNullOrWhiteSpace(Statement)
            ? throw new ArgumentException("Claim statement must not be empty.", nameof(Statement))
            : Statement;
        ArgumentNullException.ThrowIfNull(Subject);
        ArgumentNullException.ThrowIfNull(Evidence);
        ArgumentNullException.ThrowIfNull(ResidualRisks);
        if (Evidence.Any(static item => item is null))
        {
            throw new ArgumentException("Claim evidence must not contain null.", nameof(Evidence));
        }

        if (ResidualRisks.Any(static item => item is null))
        {
            throw new ArgumentException("Claim residual risks must not contain null.", nameof(ResidualRisks));
        }

        this.Subject = new ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>(Subject, StringComparer.Ordinal));
        this.Validity = Validity ?? throw new ArgumentNullException(nameof(Validity));
        this.Evidence = Array.AsReadOnly(Evidence.ToArray());
        this.ResidualRisks = Array.AsReadOnly(ResidualRisks.ToArray());
    }

    public UcliCode Id { get; }

    public AssuranceClaimStatus Status { get; }

    public AssuranceCoverage Coverage { get; }

    public bool Required { get; }

    public AssuranceVerifierId VerifierRef { get; }

    public string Statement { get; }

    public IReadOnlyDictionary<string, object?> Subject { get; }

    public ReadyClaimValidityOutput Validity { get; }

    public IReadOnlyList<ReadyEvidenceOutput> Evidence { get; }

    public IReadOnlyList<ReadyResidualRiskOutput> ResidualRisks { get; }
}
