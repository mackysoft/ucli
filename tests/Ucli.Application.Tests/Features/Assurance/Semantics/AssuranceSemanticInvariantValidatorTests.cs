using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class AssuranceSemanticInvariantValidatorTests
{
    private const string ReadyClaim = "UNITY_READY_EXECUTION";
    private const string CompileClaim = "UNITY_COMPILE_NO_ERRORS";
    private const string LogUnavailableRisk = "UNITY_LOG_UNAVAILABLE";

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidAssurancePayload_ReturnsNoViolations ()
    {
        using var document = JsonDocument.Parse(ValidPayload());
        var result = CreateValidator().Validate(document.RootElement);

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithValidReadyProbeOnlyPayload_ReturnsNoViolations ()
    {
        var result = Validate(ValidReadyPayload("""
            "validity": {
              "kind": "probeOnly",
              "guaranteesReusableSession": false
            },
            """));

        Assert.True(result.IsValid);
        Assert.Empty(result.Violations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithReadyClaimMissingValidity_ReturnsValidityPath ()
    {
        var result = Validate(ValidReadyPayload(validityJson: string.Empty));

        AssertViolationPath(result, "$.claims[0].validity");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithAutoOneshotReadyClaimGuaranteeingReusableSession_ReturnsGuaranteePath ()
    {
        var result = Validate(ValidReadyPayload("""
            "validity": {
              "kind": "probeOnly",
              "guaranteesReusableSession": true
            },
            """));

        AssertViolationPath(result, "$.claims[0].validity.guaranteesReusableSession");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithMissingVerifierReference_ReturnsClaimVerifierPath ()
    {
        var payload = """
            {
              "verdict": "incomplete",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "missing",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.claims[0].verifierRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithRequiredVerifierWithoutPrimaryClaims_ReturnsPrimaryClaimsPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.verifiers[0].primaryClaims");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithRequiredClaimOwnedByOptionalVerifier_ReturnsClaimRequiredPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": false,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.claims[0].required");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithPrimaryClaimOwnedByAnotherVerifier_ReturnsPrimaryClaimPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_COMPILE_NO_ERRORS"
                  ],
                  "reportRef": "ready-log"
                },
                {
                  "id": "compile",
                  "kind": "compile",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_COMPILE_NO_ERRORS"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                },
                {
                  "id": "UNITY_COMPILE_NO_ERRORS",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "compile",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.verifiers[0].primaryClaims[0]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithOptionalPrimaryClaimOwnedByRequiredVerifier_ReturnsPrimaryClaimPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": false,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.verifiers[0].primaryClaims[0]");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithVerdictMismatch_ReturnsVerdictPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "failed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.verdict");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnresolvedReportAndEvidenceReferences_ReturnsReferencePaths ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "missing-report"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "missing-evidence"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.verifiers[0].reportRef");
        AssertViolationPath(result, "$.claims[0].evidence[0].evidenceRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithDigestOnlyReport_ReturnsReportEntryPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "digest": "abc123"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.reports.ready-log");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithUnknownClaimAndRiskCodes_ReturnsCodePaths ()
    {
        var payload = """
            {
              "verdict": "fail",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_UNKNOWN_CLAIM"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_UNKNOWN_CLAIM",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": [
                    {
                      "code": "UNITY_UNKNOWN_CLAIM_RISK",
                      "blocking": true
                    }
                  ]
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": [
                {
                  "code": "UNITY_UNKNOWN_GLOBAL_RISK",
                  "blocking": true
                }
              ]
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.claims[0].id");
        AssertViolationPath(result, "$.claims[0].residualRisks[0].code");
        AssertViolationPath(result, "$.residualRisks[0].code");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithDuplicateVerifierId_ReturnsDuplicateVerifierPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                },
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.verifiers[1].id");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithDuplicateClaimId_ReturnsDuplicateClaimPath ()
    {
        var payload = """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                },
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log"
                }
              },
              "residualRisks": []
            }
            """;

        var result = Validate(payload);

        AssertViolationPath(result, "$.claims[1].id");
    }

    private static AssuranceSemanticInvariantValidationResult Validate (string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return CreateValidator().Validate(document.RootElement);
    }

    private static string ValidPayload ()
    {
        return """
            {
              "verdict": "pass",
              "verifiers": [
                {
                  "id": "ready",
                  "kind": "ready",
                  "deterministic": true,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "reportRef": "ready-log"
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready",
                  "evidence": [
                    {
                      "kind": "log",
                      "evidenceRef": "ready-log"
                    }
                  ],
                  "residualRisks": []
                }
              ],
              "reports": {
                "ready-log": {
                  "kind": "log",
                  "path": "artifacts/ready.log",
                  "digest": "abc123"
                }
              },
              "residualRisks": []
            }
            """;
    }

    private static AssuranceSemanticInvariantValidator CreateValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new StubCodeCatalog(
            [
                CreateDescriptor(ReadyClaim, CodeCatalogKindValues.Claim),
                CreateDescriptor(CompileClaim, CodeCatalogKindValues.Claim),
                CreateDescriptor(LogUnavailableRisk, CodeCatalogKindValues.Risk),
            ]),
            [new ReadyAssuranceSemanticInvariantRule()]);
    }

    private static string ValidReadyPayload (string validityJson)
    {
        return $$"""
            {
              "verdict": "pass",
              "target": "execution",
              "requestedMode": "auto",
              "resolvedMode": "oneshot",
              "sessionKind": "transientProbe",
              "verifiers": [
                {
                  "id": "ready.lifecycle",
                  "kind": "ready.lifecycle",
                  "deterministic": false,
                  "required": true,
                  "primaryClaims": [
                    "UNITY_READY_EXECUTION"
                  ],
                  "effects": []
                }
              ],
              "claims": [
                {
                  "id": "UNITY_READY_EXECUTION",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready.lifecycle",
                  {{validityJson}}
                  "evidence": [],
                  "residualRisks": []
                }
              ],
              "reports": {},
              "residualRisks": []
            }
            """;
    }

    private static CodeCatalogDescriptor CreateDescriptor (
        string code,
        string kind)
    {
        return new CodeCatalogDescriptor(
            new UcliCode(code),
            kind,
            Category: "test",
            Summary: code,
            Meaning: null,
            AppearsIn: [],
            AppliesTo: [],
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: null,
            Inspect: [],
            RelatedCodes: []);
    }

    private static void AssertViolationPath (
        AssuranceSemanticInvariantValidationResult result,
        string path)
    {
        Assert.Contains(result.Violations, violation => string.Equals(violation.Path, path, StringComparison.Ordinal));
    }

    private sealed class StubCodeCatalog : ICodeCatalog
    {
        private readonly Dictionary<string, CodeCatalogDescriptor> descriptorsByCode;

        public StubCodeCatalog (IEnumerable<CodeCatalogDescriptor> descriptors)
        {
            descriptorsByCode = descriptors.ToDictionary(
                static descriptor => descriptor.Code.Value,
                static descriptor => descriptor,
                StringComparer.Ordinal);
            Descriptors = descriptorsByCode.Values
                .OrderBy(static descriptor => descriptor.Code.Value, StringComparer.Ordinal)
                .ToArray();
        }

        public IReadOnlyList<CodeCatalogDescriptor> Descriptors { get; }

        public bool TryFind (
            UcliCode code,
            out CodeCatalogDescriptor descriptor)
        {
            if (code.IsValid && descriptorsByCode.TryGetValue(code.Value, out descriptor!))
            {
                return true;
            }

            descriptor = null!;
            return false;
        }
    }
}
