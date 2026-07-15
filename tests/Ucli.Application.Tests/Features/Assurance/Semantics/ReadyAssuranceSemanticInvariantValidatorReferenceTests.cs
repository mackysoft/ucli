using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.ReadyAssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class ReadyAssuranceSemanticInvariantValidatorReferenceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithMissingVerifierReference_ReturnsClaimVerifierPath ()
    {
        var payload = CreateReadyPayload(claims: [CreateClaim(verifierRef: "missing")]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.claims[0].verifierRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithInvalidVerifierIdentifiers_ReturnsIdentifierPaths ()
    {
        var payload = CreateReadyPayload(
            verifiers: [CreateVerifier(id: " ready")],
            claims: [CreateClaim(verifierRef: " ready")]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[0].id");
        AssertViolationPath(result, "$.claims[0].verifierRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnresolvedReportAndEvidenceReferences_ReturnsReferencePaths ()
    {
        var payload = CreateReadyPayload(
            verifiers: [CreateVerifier(reportRef: "missing-report")],
            claims: [CreateClaim(evidence: [CreateLogEvidence("missing-evidence")])]);

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.verifiers[0].reportRef");
        AssertViolationPath(result, "$.claims[0].evidence[0].evidenceRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithDigestOnlyReport_ReturnsReportEntryPath ()
    {
        var payload = CreateReadyPayload(
            reports: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ready-log"] = CreateReport(path: null),
            });

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.reports.ready-log");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithInvalidReportDigest_ReturnsDigestPath ()
    {
        var payload = CreateReadyPayload(
            reports: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["ready-log"] = CreateReport(digest: "not-a-digest"),
            });

        var result = ValidateReadyPayload(payload);

        AssertViolationPath(result, "$.reports.ready-log.digest");
    }
}
