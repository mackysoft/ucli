using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Semantics;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.Semantics;

public sealed class VerifyAssuranceSemanticInvariantRuleTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithErrorDiagnosticPassedClaim_ReturnsStatusViolation ()
    {
        var result = Validate(CreatePayload(
            """
            {
              "id": "READ_SURFACE_SAFE",
              "status": "passed",
              "coverage": "full",
              "required": true,
              "verifierRef": "postRead",
              "statement": "Read surface was safe.",
              "subject": {
                "kind": "postRead"
              },
              "evidence": [
                {
                  "kind": "fromResultSummary",
                  "data": {
                    "diagnosticImpact": "error"
                  }
                }
              ],
              "residualRisks": []
            }
            """));

        Assert.Contains(result.Violations, static violation => string.Equals(violation.Path, "$.claims[0].status", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnknownDiagnosticImpact_ReturnsImpactViolation ()
    {
        var result = Validate(CreatePayload(
            """
            {
              "id": "READ_SURFACE_SAFE",
              "status": "passed",
              "coverage": "full",
              "required": true,
              "verifierRef": "postRead",
              "statement": "Read surface was safe.",
              "subject": {
                "kind": "postRead"
              },
              "evidence": [
                {
                  "kind": "fromResultSummary",
                  "data": {
                    "diagnosticImpact": "external"
                  }
                }
              ],
              "residualRisks": []
            }
            """));

        Assert.Contains(
            result.Violations,
            static violation => string.Equals(
                violation.Path,
                "$.claims[0].evidence[0].data.diagnosticImpact",
                StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithPartialDiagnosticFullCoverageClaim_ReturnsViolation ()
    {
        var result = Validate(CreatePayload(
            """
            {
              "id": "READ_SURFACE_SAFE",
              "status": "passed",
              "coverage": "full",
              "required": true,
              "verifierRef": "postRead",
              "statement": "Read surface was safe.",
              "subject": {
                "kind": "postRead"
              },
              "evidence": [
                {
                  "kind": "fromResultSummary",
                  "data": {
                    "diagnosticImpact": "partial"
                  }
                }
              ],
              "residualRisks": []
            }
            """));

        Assert.Contains(result.Violations, static violation => string.Equals(violation.Path, "$.claims[0].coverage", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithRequiredUnavailablePostMutationClaim_ReturnsViolation ()
    {
        var result = Validate(CreatePayload(
            """
            {
              "id": "POST_MUTATION_OBSERVED",
              "status": "passed",
              "coverage": "full",
              "required": true,
              "verifierRef": "postRead",
              "statement": "Post mutation was observed.",
              "subject": {
                "kind": "postRead",
                "reason": "expectedPostStateUnavailable"
              },
              "evidence": [
                {
                  "kind": "fromResultSummary",
                  "data": {
                    "diagnosticImpact": "none"
                  }
                }
              ],
              "residualRisks": []
            }
            """));

        Assert.Contains(result.Violations, static violation => string.Equals(violation.Path, "$.claims[0].required", StringComparison.Ordinal));
        Assert.Contains(result.Violations, static violation => string.Equals(violation.Path, "$.claims[0].status", StringComparison.Ordinal));
        Assert.Contains(result.Violations, static violation => string.Equals(violation.Path, "$.claims[0].coverage", StringComparison.Ordinal));
    }

    private static AssuranceSemanticInvariantValidationResult Validate (string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return CreateValidator().Validate(document.RootElement);
    }

    private static AssuranceSemanticInvariantValidator CreateValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog([new VerifyCodeCatalogContributor()]),
            [new BuildAssuranceSemanticInvariantRule()],
            [new VerifyAssuranceSemanticInvariantRule()]);
    }

    private static string CreatePayload (string claimJson)
    {
        return $$"""
        {
          "verdict": "pass",
          "profile": {
            "source": "builtIn",
            "name": "mutation",
            "path": null,
            "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
          },
          "timeoutMilliseconds": 10000,
          "verifiers": [
            {
              "id": "postRead",
              "kind": "postRead",
              "deterministic": true,
              "required": true,
              "primaryClaims": [
                "{{ReadClaimId(claimJson)}}"
              ],
              "effects": []
            }
          ],
          "claims": [
            {{claimJson}}
          ],
          "reports": {},
          "residualRisks": []
        }
        """;
    }

    private static string ReadClaimId (string claimJson)
    {
        using var document = JsonDocument.Parse(claimJson);
        return document.RootElement.GetProperty("id").GetString()!;
    }
}
