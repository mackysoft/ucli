using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class AssuranceSemanticInvariantValidatorTests
{
    private const string ReadyClaim = "UNITY_READY_EXECUTION";
    private const string CompileClaim = "UNITY_COMPILE_NO_ERRORS";
    private const string LogUnavailableRisk = "UNITY_LOG_UNAVAILABLE";
    private static readonly UcliCode[] BuildPipelineClaimCodes =
    [
        BuildClaimCodes.UnityBuildProfileResolved,
        BuildClaimCodes.UnityReadyForBuild,
        BuildClaimCodes.UnityBuildInputsResolved,
        BuildClaimCodes.UnityBuildRunnerResolved,
        BuildClaimCodes.UnityBuildCompleted,
        BuildClaimCodes.UnityBuildSucceeded,
        BuildClaimCodes.UnityBuildResultAccounted,
        BuildClaimCodes.UnityBuildReportAccounted,
        BuildClaimCodes.UnityBuildArtifactsAccounted,
        BuildClaimCodes.UnityBuildOutputDigested,
        BuildClaimCodes.UnityBuildLogsAccounted,
        BuildClaimCodes.UnityBuildProjectMutationAccounted,
        BuildClaimCodes.UnityBuildValidForGeneration,
    ];

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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Packages/BuildProfiles/Linux.asset")]
    [InlineData("Assets/BuildProfiles/Linux.asset.meta")]
    [InlineData("Assets/../BuildProfiles/Linux.asset")]
    public void Validate_WithUnityBuildProfileInvalidPath_ReturnsPathViolation (string path)
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            useUnityBuildProfileInput: true,
            unityBuildProfilePath: path));

        AssertViolationPath(result, "$.build.inputs.unityBuildProfile.path");
    }


    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadMissingStableReport_ReturnsReportsPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogReport: false));

        AssertViolationPath(result, "$.reports");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportMissingDigest_ReturnsReportDigestPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogDigest: false));

        AssertViolationPath(result, "$.reports.buildLog.digest");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadDigestOnlyReport_ReturnsReportPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogPath: false));

        AssertViolationPath(result, "$.reports.buildLog.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportInvalidDigest_ReturnsReportDigestPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildLogDigest: "sha256:dddd"));

        AssertViolationPath(result, "$.reports.buildLog.digest");
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("/workspace/.ucli/build.log")]
    [InlineData("C:/workspace/.ucli/build.log")]
    [InlineData("C:workspace/.ucli/build.log")]
    [InlineData("../build.log")]
    [InlineData("artifacts/../build.log")]
    [InlineData("artifacts//build.log")]
    [InlineData("artifacts\\build.log")]
    [InlineData(".")]
    [InlineData("")]
    public void Validate_WithBuildPayloadReportNonArtifactRootRelativePath_ReturnsReportPath (string buildLogPath)
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildLogPath: buildLogPath));

        AssertViolationPath(result, "$.reports.buildLog.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportKind_ReturnsReportKindPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogKind: true));

        AssertViolationPath(result, "$.reports.buildLog.kind");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportNonArtifactRootRelativePathAndNoClaims_ReturnsReportPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            includeBuildClaims: false,
            buildLogPath: "../build.log"));

        AssertViolationPath(result, "$.reports.buildLog.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadMissingProfile_ReturnsProfilePath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildProfile: false));

        AssertViolationPath(result, "$.build.profile");
        Assert.Single(result.Violations, static violation => string.Equals(violation.Path, "$.build.profile", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildOutputManifestRefMismatch_ReturnsManifestRefPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildManifestRef: "build"));

        AssertViolationPath(result, "$.build.output.manifestRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildSummaryReportRefMismatch_ReturnsSummaryReportRefPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(summaryReportRef: "build"));

        AssertViolationPath(result, "$.build.summary.reportRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildLogsReportRefMismatch_ReturnsLogsReportRefPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(logsReportRef: "buildReport"));

        AssertViolationPath(result, "$.build.logs.reportRef");
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
                  "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
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
                  "digest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
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

    private static AssuranceSemanticInvariantValidator CreateBuildValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new StubCodeCatalog(BuildClaimCodes.All.Select(static code => CreateDescriptor(code.Value, CodeCatalogKindValues.Claim))),
            [new BuildAssuranceSemanticInvariantRule()]);
    }

    private static AssuranceSemanticInvariantValidationResult ValidateBuildPayload (string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return CreateBuildValidator().Validate(document.RootElement);
    }

    private static string CreateBuildPayload (
        string verdict = "pass",
        string buildResult = "succeeded",
        string buildCompletionReason = "completed",
        string buildCompletedClaimStatus = "passed",
        string buildSucceededClaimStatus = "passed",
        string buildGenerationClaimStatus = "passed",
        bool includeBuildLogReport = true,
        bool includeBuildLogDigest = true,
        string? buildSucceededEvidenceRef = null,
        bool buildGenerationEvidenceDataOnly = false,
        string? buildGenerationEvidenceDataValidForAssetRefreshGeneration = null,
        bool includeBuildProfile = true,
        string buildManifestRef = "buildOutputManifest",
        string summaryReportRef = "buildReport",
        string? summaryResult = null,
        string logsReportRef = "buildLog",
        IReadOnlyList<object>? verifierEffects = null,
        bool includeBuildGenerations = true,
        string validForAssetRefreshGeneration = "asset-after",
        bool includeBuildLogPath = true,
        bool includeBuildClaims = true,
        string? buildLogPath = null,
        bool includeBuildLogKind = false,
        string buildLogDigest = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
        bool useUnityBuildProfileInput = false,
        string unityBuildProfilePath = "Assets/BuildProfiles/Linux.asset")
    {
        var reports = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["build"] = new
            {
                path = "build.json",
                digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            },
            ["buildReport"] = new
            {
                path = "build-report.json",
                digest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            },
            ["buildOutputManifest"] = new
            {
                path = "output-manifest.json",
                digest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            },
        };
        if (includeBuildLogReport)
        {
            var buildLogReport = includeBuildLogDigest
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = includeBuildLogPath ? buildLogPath ?? "build.log" : null,
                    ["digest"] = buildLogDigest,
                }
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = buildLogPath ?? "build.log",
                };
            if (includeBuildLogKind)
            {
                buildLogReport["kind"] = "buildLog";
            }

            reports["buildLog"] = buildLogReport;
        }

        object? profile = includeBuildProfile
            ? new
            {
                path = ".ucli/build/player.json",
                digest = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
            }
            : null;
        object? generations = includeBuildGenerations
            ? CreateBuildGenerations(validForAssetRefreshGeneration)
            : null;
        var generationEvidenceData = buildGenerationEvidenceDataValidForAssetRefreshGeneration == null
            ? generations
            : CreateBuildGenerations(buildGenerationEvidenceDataValidForAssetRefreshGeneration);
        var inputScenes = new
        {
            source = useUnityBuildProfileInput ? "unityBuildProfile" : "explicit",
            paths = new[] { "Assets/Scenes/Main.unity" },
        };
        var inputOptions = new
        {
            development = true,
        };
        var inputs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["inputKind"] = useUnityBuildProfileInput ? "unityBuildProfile" : "explicit",
            ["target"] = new
            {
                stableName = "standaloneLinux64",
                unityBuildTarget = "StandaloneLinux64",
            },
            ["scenes"] = inputScenes,
            ["options"] = inputOptions,
        };
        if (useUnityBuildProfileInput)
        {
            inputs["unityBuildProfile"] = new
            {
                path = unityBuildProfilePath,
                digest = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            };
        }

        var claims = includeBuildClaims
            ? BuildPipelineClaimCodes
                .Select(code =>
                {
                    var status = ResolveBuildClaimStatus(
                        code,
                        buildCompletedClaimStatus,
                        buildSucceededClaimStatus,
                        buildGenerationClaimStatus);
                    return (object)new
                    {
                        id = code.Value,
                        status,
                        coverage = ResolveBuildClaimCoverage(status),
                        required = true,
                        verifierRef = "build",
                        evidence = CreateBuildEvidence(
                            code.Value,
                            buildResult,
                            buildSucceededEvidenceRef,
                            buildGenerationEvidenceDataOnly,
                            generationEvidenceData),
                        residualRisks = Array.Empty<object>(),
                    };
                })
                .ToArray()
            : Array.Empty<object>();
        return JsonSerializer.Serialize(new
        {
            verdict,
            build = new
            {
                profile,
                inputs,
                runner = new
                {
                    kind = "buildPipeline",
                    method = (string?)null,
                    invocation = new
                    {
                        arguments = new Dictionary<string, string>(StringComparer.Ordinal),
                        environment = new
                        {
                            variables = Array.Empty<string>(),
                            secrets = Array.Empty<string>(),
                        },
                    },
                },
                runnerResult = new
                {
                    source = "buildPipelineBuildReport",
                    status = buildResult,
                },
                output = new
                {
                    manifestRef = buildManifestRef,
                    manifestDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                },
                generations,
                summary = new
                {
                    result = summaryResult ?? buildResult,
                    reportRef = summaryReportRef,
                },
                logs = new
                {
                    reportRef = logsReportRef,
                    entryCount = 1,
                    errorCount = 0,
                    warningCount = 0,
                    completionReason = buildCompletionReason,
                    window = new
                    {
                        startedAtUtc = "2026-06-12T00:00:00+00:00",
                        completedAtUtc = "2026-06-12T00:00:03+00:00",
                        cursorStart = (string?)null,
                        cursorEnd = (string?)null,
                    },
                },
            },
            verifiers = new[]
            {
                new
                {
                    id = "build",
                    kind = "build",
                    deterministic = false,
                    required = true,
                    primaryClaims = BuildPipelineClaimCodes.Select(static code => code.Value).ToArray(),
                    effects = verifierEffects ?? Enum
                        .GetValues<BuildEffect>()
                        .Select(static effect => (object)ContractLiteralCodec.ToValue(effect))
                        .ToArray(),
                    reportRef = "build",
                },
            },
            claims,
            reports,
            residualRisks = Array.Empty<object>(),
        });
    }

    private static object CreateBuildGenerations (string validForAssetRefreshGeneration)
    {
        return new
        {
            before = new
            {
                compileGeneration = "compile-before",
                domainReloadGeneration = "domain-before",
                assetRefreshGeneration = "asset-before",
            },
            after = new
            {
                compileGeneration = "compile-after",
                domainReloadGeneration = "domain-after",
                assetRefreshGeneration = "asset-after",
            },
            validFor = new
            {
                compileGeneration = "compile-after",
                domainReloadGeneration = "domain-after",
                assetRefreshGeneration = validForAssetRefreshGeneration,
            },
        };
    }

    private static string ResolveBuildClaimStatus (
        UcliCode code,
        string buildCompletedClaimStatus,
        string buildSucceededClaimStatus,
        string buildGenerationClaimStatus)
    {
        if (code.Equals(BuildClaimCodes.UnityBuildCompleted))
        {
            return buildCompletedClaimStatus;
        }

        if (code.Equals(BuildClaimCodes.UnityBuildSucceeded))
        {
            return buildSucceededClaimStatus;
        }

        if (code.Equals(BuildClaimCodes.UnityBuildValidForGeneration))
        {
            return buildGenerationClaimStatus;
        }

        return "passed";
    }

    private static string ResolveBuildClaimCoverage (string status)
    {
        return string.Equals(status, "indeterminate", StringComparison.Ordinal)
            || string.Equals(status, "unverified", StringComparison.Ordinal)
            ? "none"
            : "full";
    }

    private static string BuildSucceededClaimPath (string propertyName)
    {
        for (var i = 0; i < BuildPipelineClaimCodes.Length; i++)
        {
            if (BuildPipelineClaimCodes[i].Equals(BuildClaimCodes.UnityBuildSucceeded))
            {
                return $"$.claims[{i}].{propertyName}";
            }
        }

        throw new InvalidOperationException("Build claim catalog must contain UNITY_BUILD_SUCCEEDED.");
    }

    private static string BuildCompletedClaimPath (string propertyName)
    {
        for (var i = 0; i < BuildPipelineClaimCodes.Length; i++)
        {
            if (BuildPipelineClaimCodes[i].Equals(BuildClaimCodes.UnityBuildCompleted))
            {
                return $"$.claims[{i}].{propertyName}";
            }
        }

        throw new InvalidOperationException("Build claim catalog must contain UNITY_BUILD_COMPLETED.");
    }

    private static string BuildGenerationClaimPath (string propertyName)
    {
        for (var i = 0; i < BuildPipelineClaimCodes.Length; i++)
        {
            if (BuildPipelineClaimCodes[i].Equals(BuildClaimCodes.UnityBuildValidForGeneration))
            {
                return $"$.claims[{i}].{propertyName}";
            }
        }

        throw new InvalidOperationException("Build claim catalog must contain UNITY_BUILD_VALID_FOR_GENERATION.");
    }

    private static object[] CreateBuildEvidence (
        string claimId,
        string buildResult,
        string? buildSucceededEvidenceRef,
        bool buildGenerationEvidenceDataOnly,
        object? buildGenerationEvidenceData)
    {
        if (BuildClaimCodes.UnityBuildValidForGeneration.EqualsValue(claimId) && buildGenerationEvidenceDataOnly)
        {
            return
            [
                new
                {
                    kind = "evidence",
                    data = buildGenerationEvidenceData,
                },
            ];
        }

        var evidenceRef = ResolveBuildEvidenceRef(claimId);
        if (BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId) && buildSucceededEvidenceRef != null)
        {
            evidenceRef = buildSucceededEvidenceRef;
        }

        if (BuildClaimCodes.UnityBuildResultAccounted.EqualsValue(claimId))
        {
            return
            [
                new
                {
                    kind = "evidence",
                    evidenceRef,
                    data = new
                    {
                        source = "buildPipelineBuildReport",
                        status = buildResult,
                    },
                },
            ];
        }

        return
        [
            new
            {
                kind = "evidence",
                evidenceRef,
            },
        ];
    }

    private static string ResolveBuildEvidenceRef (string claimId)
    {
        if (BuildClaimCodes.UnityBuildCompleted.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildReportAccounted.EqualsValue(claimId))
        {
            return "buildReport";
        }

        if (BuildClaimCodes.UnityBuildOutputDigested.EqualsValue(claimId))
        {
            return "buildOutputManifest";
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted.EqualsValue(claimId))
        {
            return "buildLog";
        }

        return "build";
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
