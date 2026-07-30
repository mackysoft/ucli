using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class AssuranceVerdictCalculatorTests
{
    private static readonly AssuranceVerifierId VerifierId = new("verifier");

    private static readonly UcliCode RequiredClaimId = new("REQUIRED_CLAIM");

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenEveryRequiredClaimPassedWithFullCoverage_ReturnsPass ()
    {
        var verdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Full, hasBlockingResidualRisk: false)],
            Array.Empty<FakeResidualRisk>());

        Assert.Equal(Verdict.Pass, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenNoRequiredClaimOrBlockingRiskExists_ReturnsPass ()
    {
        var verdict = AssuranceVerdictCalculator.Calculate(
            Array.Empty<FakeVerifier>(),
            Array.Empty<FakeClaim>(),
            Array.Empty<FakeResidualRisk>());

        Assert.Equal(Verdict.Pass, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenRequiredClaimFailed_ReturnsFail ()
    {
        var verdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Failed, AssuranceCoverage.Full, hasBlockingResidualRisk: false)],
            Array.Empty<FakeResidualRisk>());
        var verdictWithBlockingRisk = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Failed, AssuranceCoverage.Full, hasBlockingResidualRisk: false)],
            [new FakeResidualRisk(Blocking: true)]);

        Assert.Equal(Verdict.Fail, verdict);
        Assert.Equal(Verdict.Fail, verdictWithBlockingRisk);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenRequiredClaimIsNotFullyEstablished_ReturnsIncomplete ()
    {
        var partialCoverageVerdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Partial, hasBlockingResidualRisk: false)],
            Array.Empty<FakeResidualRisk>());
        var indeterminateVerdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Indeterminate, AssuranceCoverage.None, hasBlockingResidualRisk: false)],
            Array.Empty<FakeResidualRisk>());

        Assert.Equal(Verdict.Incomplete, partialCoverageVerdict);
        Assert.Equal(Verdict.Incomplete, indeterminateVerdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenOptionalClaimFails_ReturnsPass ()
    {
        var verdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [
                RequiredClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Full, hasBlockingResidualRisk: false),
                new FakeClaim(
                    new UcliCode("OPTIONAL_CLAIM"),
                    AssuranceClaimStatus.Failed,
                    AssuranceCoverage.Full,
                    Required: false,
                    VerifierId,
                    HasBlockingResidualRisk: false),
            ],
            Array.Empty<FakeResidualRisk>());

        Assert.Equal(Verdict.Pass, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenBlockingRiskExistsWithoutFailedRequiredClaim_ReturnsFail ()
    {
        var claimRiskVerdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Full, hasBlockingResidualRisk: true)],
            Array.Empty<FakeResidualRisk>());
        var payloadRiskVerdict = AssuranceVerdictCalculator.Calculate(
            [RequiredVerifier()],
            [RequiredClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Full, hasBlockingResidualRisk: false)],
            [new FakeResidualRisk(Blocking: true)]);

        Assert.Equal(Verdict.Fail, claimRiskVerdict);
        Assert.Equal(Verdict.Fail, payloadRiskVerdict);
    }

    private static FakeVerifier RequiredVerifier ()
    {
        return new FakeVerifier(VerifierId, Required: true, [RequiredClaimId]);
    }

    private static FakeClaim RequiredClaim (
        AssuranceClaimStatus status,
        AssuranceCoverage coverage,
        bool hasBlockingResidualRisk)
    {
        return new FakeClaim(
            RequiredClaimId,
            status,
            coverage,
            Required: true,
            VerifierId,
            hasBlockingResidualRisk);
    }

    private sealed record FakeVerifier (
        AssuranceVerifierId Id,
        bool Required,
        IReadOnlyList<UcliCode> PrimaryClaims) : IAssuranceVerdictVerifier;

    private sealed record FakeClaim (
        UcliCode Id,
        AssuranceClaimStatus Status,
        AssuranceCoverage Coverage,
        bool Required,
        AssuranceVerifierId VerifierRef,
        bool HasBlockingResidualRisk) : IAssuranceVerdictClaim;

    private sealed record FakeResidualRisk (bool Blocking) : IAssuranceVerdictResidualRisk;
}
