using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;

/// <summary> Represents one claim in a verify assurance payload. </summary>
internal sealed record VerifyClaimOutput : IAssuranceVerdictClaim
{
    public VerifyClaimOutput (
        UcliCode Id,
        AssuranceClaimStatus Status,
        AssuranceCoverage Coverage,
        bool Required,
        AssuranceVerifierId VerifierRef,
        string Statement,
        IReadOnlyDictionary<string, object?> Subject,
        ReadyClaimValidityOutput? Validity,
        IReadOnlyList<VerifyEvidenceOutput> Evidence,
        IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks)
    {
        this.Id = Id ?? throw new ArgumentNullException(nameof(Id));
        if (!TextVocabulary.IsDefined(Status))
        {
            throw new ArgumentOutOfRangeException(nameof(Status), Status, "Claim status must be defined by the assurance contract.");
        }

        if (!TextVocabulary.IsDefined(Coverage))
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
        if (Evidence.Count == 0)
        {
            throw new ArgumentException("Claim evidence must not be empty.", nameof(Evidence));
        }

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
        this.Validity = Validity;
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

    [ItemCount(1, int.MaxValue)]
    public IReadOnlyList<VerifyEvidenceOutput> Evidence { get; }

    public IReadOnlyList<VerifyResidualRiskOutput> ResidualRisks { get; }

    /// <summary> Gets optional claim-validity details, used when verify projects ready claims. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReadyClaimValidityOutput? Validity { get; }

    bool IAssuranceVerdictClaim.HasBlockingResidualRisk =>
        ResidualRisks.Any(static risk => risk.Blocking);
}
