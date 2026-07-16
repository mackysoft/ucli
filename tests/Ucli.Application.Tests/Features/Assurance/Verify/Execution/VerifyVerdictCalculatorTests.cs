using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.Execution;

public sealed class VerifyVerdictCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenAllRequiredClaimsPassedWithFullCoverage_ReturnsPass ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Full, required: true)],
            []);

        Assert.Equal(AssuranceVerdict.Pass, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenRequiredClaimFailed_ReturnsFail ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(AssuranceClaimStatus.Failed, AssuranceCoverage.Full, required: true)],
            []);

        Assert.Equal(AssuranceVerdict.Fail, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenRequiredClaimIsNotComplete_ReturnsIncomplete ()
    {
        var testCases = new[]
        {
            (AssuranceClaimStatus.Passed, AssuranceCoverage.Partial),
            (AssuranceClaimStatus.Indeterminate, AssuranceCoverage.None),
            (AssuranceClaimStatus.Unverified, AssuranceCoverage.None),
        };

        foreach (var (status, coverage) in testCases)
        {
            var verdict = VerifyVerdictCalculator.Calculate(
                [CreateClaim(status, coverage, required: true)],
                []);

            Assert.Equal(AssuranceVerdict.Incomplete, verdict);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenOptionalClaimIsNotPassingWithoutBlockingRisk_ReturnsPass ()
    {
        var testCases = new[]
        {
            (AssuranceClaimStatus.Failed, AssuranceCoverage.Full),
            (AssuranceClaimStatus.Passed, AssuranceCoverage.Partial),
            (AssuranceClaimStatus.Indeterminate, AssuranceCoverage.None),
        };

        foreach (var (status, coverage) in testCases)
        {
            var verdict = VerifyVerdictCalculator.Calculate(
                [CreateClaim(status, coverage, required: false)],
                []);

            Assert.Equal(AssuranceVerdict.Pass, verdict);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenPayloadResidualRiskIsBlocking_ReturnsFail ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(AssuranceClaimStatus.Passed, AssuranceCoverage.Full, required: true)],
            [new VerifyResidualRiskOutput(VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value, Blocking: true)]);

        Assert.Equal(AssuranceVerdict.Fail, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenClaimResidualRiskIsBlocking_ReturnsFail ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [
                CreateClaim(
                    AssuranceClaimStatus.Passed,
                    AssuranceCoverage.Full,
                    required: true,
                    residualRisks: [new VerifyResidualRiskOutput("CLAIM_RISK", Blocking: true)]),
            ],
            []);

        Assert.Equal(AssuranceVerdict.Fail, verdict);
    }

    private static VerifyClaimOutput CreateClaim (
        AssuranceClaimStatus status,
        AssuranceCoverage coverage,
        bool required,
        IReadOnlyList<VerifyResidualRiskOutput>? residualRisks = null)
    {
        return new VerifyClaimOutput(
            Id: VerifyClaimCodes.PostMutationObserved,
            Status: status,
            Coverage: coverage,
            Required: required,
            VerifierRef: new AssuranceVerifierId("postRead"),
            Statement: "Claim statement.",
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "test",
            },
            Evidence: [],
            ResidualRisks: residualRisks ?? Array.Empty<VerifyResidualRiskOutput>());
    }
}
