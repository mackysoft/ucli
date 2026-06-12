using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;

/// <summary> Validates build-specific semantic invariants inside the common assurance payload shape. </summary>
internal sealed class BuildAssuranceSemanticInvariantRule : IAssuranceSemanticInvariantRule
{
    private static readonly IReadOnlyList<string> RequiredReportKeys =
    [
        BuildReportRefs.Build,
        BuildReportRefs.BuildReport,
        BuildReportRefs.BuildOutputManifest,
        BuildReportRefs.BuildLog,
    ];

    /// <inheritdoc />
    public void ValidateClaim (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        if (!IsBuildClaim(claimId) || !payload.TryGetProperty("build", out var buildElement))
        {
            return;
        }

        ValidateReports(payload, violations);
        ValidateBuildOutput(payload, violations);
        ValidateVerifier(payload, violations);
        ValidateClaimEvidence(claimElement, claimPath, claimId, violations);
        if (BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId))
        {
            ValidateSucceededClaim(buildElement, claimElement, claimPath, violations);
        }
    }

    private static void ValidateReports (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!payload.TryGetProperty("reports", out var reportsElement) || reportsElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        for (var i = 0; i < RequiredReportKeys.Count; i++)
        {
            var reportKey = RequiredReportKeys[i];
            if (!reportsElement.TryGetProperty(reportKey, out var reportElement))
            {
                AddViolation(violations, "$.reports", $"Build payload must contain reports.{reportKey}.");
                continue;
            }

            ValidateReportEntry(reportElement, BuildPropertyPath("$.reports", reportKey), reportKey, violations);
        }
    }

    private static void ValidateReportEntry (
        JsonElement reportElement,
        string reportPath,
        string expectedKind,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (reportElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryReadString(reportElement, "kind", out var kind))
        {
            AddViolation(violations, BuildPropertyPath(reportPath, "kind"), $"Build report {expectedKind} must declare kind.");
        }
        else if (!string.Equals(kind, expectedKind, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(reportPath, "kind"), $"Build report {expectedKind} kind must match its stable report key.");
        }

        if (!TryReadString(reportElement, "path", out _))
        {
            AddViolation(violations, BuildPropertyPath(reportPath, "path"), $"Build report {expectedKind} must declare path.");
        }

        if (!TryReadString(reportElement, "digest", out _))
        {
            AddViolation(violations, BuildPropertyPath(reportPath, "digest"), $"Build report {expectedKind} must declare digest.");
        }
    }

    private static void ValidateBuildOutput (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!payload.TryGetProperty("build", out var buildElement)
            || buildElement.ValueKind != JsonValueKind.Object
            || !buildElement.TryGetProperty("output", out var outputElement)
            || outputElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!TryReadString(outputElement, "manifestRef", out var manifestRef))
        {
            AddViolation(violations, "$.build.output.manifestRef", "Build output must declare manifestRef.");
        }
        else if (!string.Equals(manifestRef, BuildReportRefs.BuildOutputManifest, StringComparison.Ordinal))
        {
            AddViolation(violations, "$.build.output.manifestRef", "Build output manifestRef must resolve to reports.buildOutputManifest.");
        }

        if (!TryReadString(outputElement, "manifestDigest", out _))
        {
            AddViolation(violations, "$.build.output.manifestDigest", "Build output must declare manifestDigest.");
        }
    }

    private static void ValidateVerifier (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!payload.TryGetProperty("verifiers", out var verifiersElement) || verifiersElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var verifierElement in verifiersElement.EnumerateArray())
        {
            var verifierPath = $"$.verifiers[{index}]";
            if (verifierElement.ValueKind == JsonValueKind.Object
                && TryReadString(verifierElement, "id", out var id)
                && string.Equals(id, BuildReportRefs.Build, StringComparison.Ordinal))
            {
                ValidateVerifierEffects(verifierElement, verifierPath, violations);
                ValidatePrimaryClaims(verifierElement, verifierPath, violations);
                return;
            }

            index++;
        }

        AddViolation(violations, "$.verifiers", $"Build payload must contain verifier id '{BuildReportRefs.Build}'.");
    }

    private static void ValidateVerifierEffects (
        JsonElement verifierElement,
        string verifierPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var effectsPath = BuildPropertyPath(verifierPath, "effects");
        if (!verifierElement.TryGetProperty("effects", out var effectsElement) || effectsElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, effectsPath, "Build verifier must declare effects.");
            return;
        }

        var effects = effectsElement
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? string.Empty)
            .ToArray();
        if (!effects.SequenceEqual(ContractLiteralCodec.GetLiterals<BuildEffect>(), StringComparer.Ordinal))
        {
            AddViolation(violations, effectsPath, "Build verifier effects must match the build effect literal set.");
        }
    }

    private static void ValidatePrimaryClaims (
        JsonElement verifierElement,
        string verifierPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var primaryClaimsPath = BuildPropertyPath(verifierPath, "primaryClaims");
        if (!verifierElement.TryGetProperty("primaryClaims", out var primaryClaimsElement) || primaryClaimsElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, primaryClaimsPath, "Build verifier must declare primaryClaims.");
            return;
        }

        var primaryClaims = primaryClaimsElement
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? string.Empty)
            .ToArray();
        if (!primaryClaims.SequenceEqual(BuildClaimCodes.All.Select(static code => code.Value), StringComparer.Ordinal))
        {
            AddViolation(violations, primaryClaimsPath, "Build verifier primaryClaims must match the build claim set.");
        }
    }

    private static void ValidateSucceededClaim (
        JsonElement buildElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadBuildResult(buildElement, out var result) || !TryReadString(claimElement, "status", out var status))
        {
            return;
        }

        var succeededLiteral = ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded);
        var passedLiteral = ContractLiteralCodec.ToValue(BuildClaimStatus.Passed);
        var failedLiteral = ContractLiteralCodec.ToValue(BuildClaimStatus.Failed);
        if (string.Equals(result, succeededLiteral, StringComparison.Ordinal))
        {
            if (!string.Equals(status, passedLiteral, StringComparison.Ordinal))
            {
                AddViolation(violations, BuildPropertyPath(claimPath, "status"), "UNITY_BUILD_SUCCEEDED must pass when BuildReport result is succeeded.");
            }

            return;
        }

        if (!string.Equals(status, failedLiteral, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "UNITY_BUILD_SUCCEEDED must fail when BuildReport result is not succeeded.");
        }
    }

    private static void ValidateClaimEvidence (
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var expectedEvidenceRef = ResolveExpectedEvidenceRef(claimId);
        if (expectedEvidenceRef == null)
        {
            return;
        }

        if (!claimElement.TryGetProperty("evidence", out var evidenceElement) || evidenceElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "evidence"), $"Build claim {claimId} must include evidence.");
            return;
        }

        foreach (var evidence in evidenceElement.EnumerateArray())
        {
            if (evidence.ValueKind == JsonValueKind.Object
                && TryReadString(evidence, "evidenceRef", out var evidenceRef)
                && string.Equals(evidenceRef, expectedEvidenceRef, StringComparison.Ordinal))
            {
                return;
            }
        }

        AddViolation(
            violations,
            BuildPropertyPath(claimPath, "evidence"),
            $"Build claim {claimId} evidence must reference reports.{expectedEvidenceRef}.");
    }

    private static string? ResolveExpectedEvidenceRef (string claimId)
    {
        if (BuildClaimCodes.UnityBuildProfileResolved.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildInputsResolved.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildArtifactsAccounted.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildValidForGeneration.EqualsValue(claimId))
        {
            return BuildReportRefs.Build;
        }

        if (BuildClaimCodes.UnityBuildCompleted.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildReportAccounted.EqualsValue(claimId))
        {
            return BuildReportRefs.BuildReport;
        }

        if (BuildClaimCodes.UnityBuildOutputDigested.EqualsValue(claimId))
        {
            return BuildReportRefs.BuildOutputManifest;
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted.EqualsValue(claimId))
        {
            return BuildReportRefs.BuildLog;
        }

        return null;
    }

    private static bool TryReadBuildResult (
        JsonElement buildElement,
        out string result)
    {
        result = string.Empty;
        if (!buildElement.TryGetProperty("summary", out var summaryElement) || summaryElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return TryReadString(summaryElement, "result", out result);
    }

    private static bool IsBuildClaim (string claimId)
    {
        return BuildClaimCodes.All.Any(code => code.EqualsValue(claimId));
    }

    private static bool TryReadString (
        JsonElement owner,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string BuildPropertyPath (
        string parentPath,
        string propertyName)
    {
        return $"{parentPath}.{propertyName}";
    }

    private static void AddViolation (
        List<AssuranceSemanticInvariantViolation> violations,
        string path,
        string message)
    {
        violations.Add(new AssuranceSemanticInvariantViolation(path, message));
    }
}
