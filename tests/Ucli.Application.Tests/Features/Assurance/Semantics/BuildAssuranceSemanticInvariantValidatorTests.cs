using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.BuildAssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class BuildAssuranceSemanticInvariantValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidBuildPayload_ReturnsNoViolations ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload());

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidUnityBuildProfileBuildPayload_ReturnsNoViolations ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(useUnityBuildProfileInput: true));

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildVerifierEffectsSubset_ReturnsNoViolations ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(verifierEffects: ["unityBuildPipeline"]));

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildVerifierUnknownEffect_ReturnsEffectsPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(verifierEffects: ["unknownEffect"]));

        AssertViolationPath(result, "$.verifiers[0].effects[0]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildVerifierNonStringEffect_ReturnsEffectsPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(verifierEffects: [1]));

        AssertViolationPath(result, "$.verifiers[0].effects[0]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildVerifierDuplicateEffect_ReturnsEffectsPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(verifierEffects: ["unityBuildPipeline", "unityBuildPipeline"]));

        AssertViolationPath(result, "$.verifiers[0].effects[1]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadMissingGenerations_ReturnsGenerationsPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildGenerations: false));

        AssertViolationPath(result, "$.build.generations");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithIncompleteBuildGenerationAndPassedClaim_ReturnsGenerationClaimStatusPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(validForAssetRefreshGeneration: "unknown"));

        AssertViolationPath(result, BuildGenerationClaimPath("status"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithIncompleteBuildGenerationAndUnverifiedClaim_ReturnsNoViolations ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            verdict: "incomplete",
            buildGenerationClaimStatus: "unverified",
            validForAssetRefreshGeneration: "unknown"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithCompleteBuildGenerationAndUnverifiedClaim_ReturnsGenerationClaimStatusPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            verdict: "incomplete",
            buildGenerationClaimStatus: "unverified"));

        AssertViolationPath(result, BuildGenerationClaimPath("status"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildGenerationValidForMismatch_ReturnsValidForPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(validForAssetRefreshGeneration: "asset-other"));

        AssertViolationPath(result, "$.build.generations.validFor");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildGenerationDataOnlyEvidence_ReturnsNoViolations ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildGenerationEvidenceDataOnly: true));

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildGenerationDataOnlyEvidenceMismatch_ReturnsGenerationClaimEvidencePath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            buildGenerationEvidenceDataOnly: true,
            buildGenerationEvidenceDataValidForAssetRefreshGeneration: "asset-evidence"));

        AssertViolationPath(result, BuildGenerationClaimPath("evidence"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildClaimEvidenceRefMismatch_ReturnsClaimEvidencePath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildSucceededEvidenceRef: "build"));

        AssertViolationPath(result, BuildSucceededClaimPath("evidence"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithFailedBuildResultAndPassedSucceededClaim_ReturnsSucceededClaimStatusPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            buildResult: "failed",
            buildCompletionReason: "failed",
            buildSucceededClaimStatus: "passed"));

        AssertViolationPath(result, BuildSucceededClaimPath("status"));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("failed", "failed")]
    [InlineData("canceled", "canceled")]
    public void Validate_WithTerminalUnsuccessfulBuildResultAndExpectedClaims_ReturnsNoViolations (
        string buildResult,
        string buildCompletionReason)
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            verdict: "fail",
            buildResult: buildResult,
            buildCompletionReason: buildCompletionReason,
            buildSucceededClaimStatus: "failed"));

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("failed", "failed")]
    [InlineData("canceled", "canceled")]
    public void Validate_WithTerminalBuildResultAndFailedCompletedClaim_ReturnsCompletedClaimStatusPath (
        string buildResult,
        string buildCompletionReason)
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            verdict: "fail",
            buildResult: buildResult,
            buildCompletionReason: buildCompletionReason,
            buildCompletedClaimStatus: "failed",
            buildSucceededClaimStatus: "failed"));

        AssertViolationPath(result, BuildCompletedClaimPath("status"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnknownBuildResultAndPassedCompletedClaim_ReturnsCompletedClaimStatusPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            verdict: "fail",
            buildResult: "unknown",
            buildCompletionReason: "failed",
            buildCompletedClaimStatus: "passed",
            buildSucceededClaimStatus: "failed"));

        AssertViolationPath(result, BuildCompletedClaimPath("status"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildCompletionReasonMismatch_ReturnsLogsCompletionReasonPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            buildResult: "failed",
            buildCompletionReason: "completed",
            buildSucceededClaimStatus: "failed"));

        AssertViolationPath(result, "$.build.logs.completionReason");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildSummaryResultMismatch_ReturnsSummaryResultPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            buildResult: "succeeded",
            summaryResult: "failed"));

        AssertViolationPath(result, "$.build.summary.result");
    }

}
