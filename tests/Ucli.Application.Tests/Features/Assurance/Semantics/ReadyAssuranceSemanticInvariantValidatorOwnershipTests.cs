using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.ReadyAssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class ReadyAssuranceSemanticInvariantValidatorOwnershipTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithRequiredVerifierWithoutPrimaryClaims_ReturnsPrimaryClaimsPath ()
    {
        var payload = CreateReadyPayload(verifiers: [CreateVerifier(primaryClaims: [])]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[0].primaryClaims");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithRequiredClaimOwnedByOptionalVerifier_ReturnsClaimRequiredPath ()
    {
        var payload = CreateReadyPayload(verifiers: [CreateVerifier(required: false)]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.claims[0].required");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithPrimaryClaimOwnedByAnotherVerifier_ReturnsPrimaryClaimPath ()
    {
        var payload = CreateReadyPayload(
            verifiers:
            [
                CreateVerifier(primaryClaims: [CompileClaim]),
                CreateVerifier(id: "compile", kind: "compile", primaryClaims: [CompileClaim]),
            ],
            claims:
            [
                CreateClaim(),
                CreateClaim(id: CompileClaim, verifierRef: "compile"),
            ]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[0].primaryClaims[0]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithOptionalPrimaryClaimOwnedByRequiredVerifier_ReturnsPrimaryClaimPath ()
    {
        var payload = CreateReadyPayload(claims: [CreateClaim(required: false)]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[0].primaryClaims[0]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithVerdictMismatch_ReturnsVerdictPath ()
    {
        var payload = CreateReadyPayload(claims: [CreateClaim(status: "failed")]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verdict");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnknownClaimAndRiskCodes_ReturnsCodePaths ()
    {
        var payload = CreateReadyPayload(
            verdict: "fail",
            verifiers: [CreateVerifier(primaryClaims: ["UNITY_UNKNOWN_CLAIM"])],
            claims:
            [
                CreateClaim(
                    id: "UNITY_UNKNOWN_CLAIM",
                    residualRisks: [CreateRisk("UNITY_UNKNOWN_CLAIM_RISK")]),
            ],
            residualRisks: [CreateRisk("UNITY_UNKNOWN_GLOBAL_RISK")]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.claims[0].id");
        AssertViolationPath(result, "$.claims[0].residualRisks[0].code");
        AssertViolationPath(result, "$.residualRisks[0].code");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithDuplicateVerifierId_ReturnsDuplicateVerifierPath ()
    {
        var payload = CreateReadyPayload(verifiers: [CreateVerifier(), CreateVerifier()]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[1].id");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithDuplicateClaimId_ReturnsDuplicateClaimPath ()
    {
        var payload = CreateReadyPayload(claims: [CreateClaim(), CreateClaim()]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.claims[1].id");
    }
}
