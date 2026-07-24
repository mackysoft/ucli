
namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Represents the claim fields used to calculate an assurance verdict. </summary>
internal readonly record struct AssuranceVerdictClaimState
{
    /// <summary> Initializes one validated claim state. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Status" /> or <paramref name="Coverage" /> is not defined by the assurance contract. </exception>
    public AssuranceVerdictClaimState (
        AssuranceClaimStatus Status,
        AssuranceCoverage Coverage,
        bool Required,
        bool HasBlockingResidualRisk)
    {
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
        this.HasBlockingResidualRisk = HasBlockingResidualRisk;
    }

    public AssuranceClaimStatus Status { get; }

    public AssuranceCoverage Coverage { get; }

    public bool Required { get; }

    public bool HasBlockingResidualRisk { get; }
}
