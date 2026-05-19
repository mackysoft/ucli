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
            [CreateClaim(VerifyClaimStatusValues.Passed, VerifyCoverageValues.Full, required: true)],
            []);

        Assert.Equal(VerifyVerdictValues.Pass, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenRequiredClaimFailed_ReturnsFail ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(VerifyClaimStatusValues.Failed, VerifyCoverageValues.Full, required: true)],
            []);

        Assert.Equal(VerifyVerdictValues.Fail, verdict);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(VerifyClaimStatusValues.Passed, VerifyCoverageValues.Partial)]
    [InlineData(VerifyClaimStatusValues.Indeterminate, VerifyCoverageValues.None)]
    [InlineData(VerifyClaimStatusValues.Unverified, VerifyCoverageValues.None)]
    public void Calculate_WhenRequiredClaimIsNotComplete_ReturnsIncomplete (
        string status,
        string coverage)
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(status, coverage, required: true)],
            []);

        Assert.Equal(VerifyVerdictValues.Incomplete, verdict);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(VerifyClaimStatusValues.Failed, VerifyCoverageValues.Full)]
    [InlineData(VerifyClaimStatusValues.Passed, VerifyCoverageValues.Partial)]
    [InlineData(VerifyClaimStatusValues.Indeterminate, VerifyCoverageValues.None)]
    public void Calculate_WhenOptionalClaimIsNotPassingWithoutBlockingRisk_ReturnsPass (
        string status,
        string coverage)
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(status, coverage, required: false)],
            []);

        Assert.Equal(VerifyVerdictValues.Pass, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenPayloadResidualRiskIsBlocking_ReturnsFail ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [CreateClaim(VerifyClaimStatusValues.Passed, VerifyCoverageValues.Full, required: true)],
            [new VerifyResidualRiskOutput(VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value, Blocking: true)]);

        Assert.Equal(VerifyVerdictValues.Fail, verdict);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Calculate_WhenClaimResidualRiskIsBlocking_ReturnsFail ()
    {
        var verdict = VerifyVerdictCalculator.Calculate(
            [
                CreateClaim(
                    VerifyClaimStatusValues.Passed,
                    VerifyCoverageValues.Full,
                    required: true,
                    residualRisks: [new VerifyResidualRiskOutput("CLAIM_RISK", Blocking: true)]),
            ],
            []);

        Assert.Equal(VerifyVerdictValues.Fail, verdict);
    }

    private static VerifyClaimOutput CreateClaim (
        string status,
        string coverage,
        bool required,
        IReadOnlyList<VerifyResidualRiskOutput>? residualRisks = null)
    {
        return new VerifyClaimOutput(
            Id: VerifyClaimCodes.PostMutationObserved.Value,
            Status: status,
            Coverage: coverage,
            Required: required,
            VerifierRef: "postRead",
            Statement: "Claim statement.",
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "test",
            },
            Evidence: [],
            ResidualRisks: residualRisks ?? Array.Empty<VerifyResidualRiskOutput>());
    }
}
