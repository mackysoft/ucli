using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;

/// <summary> Validates build-specific semantic invariants inside the common assurance payload shape. </summary>
internal sealed class BuildAssuranceSemanticInvariantRule : IAssuranceSemanticInvariantRule
{
    private const string UnknownGeneration = "unknown";
    private const string UnverifiedClaimStatus = "unverified";

    private static readonly IReadOnlyList<string> RequiredReportKeys =
    [
        BuildReportRefs.Build,
        BuildReportRefs.BuildReport,
        BuildReportRefs.BuildOutputManifest,
        BuildReportRefs.BuildLog,
    ];

    private static readonly IReadOnlySet<string> AllowedBuildEffects =
        new HashSet<string>(ContractLiteralCodec.GetLiterals<BuildEffect>(), StringComparer.Ordinal);

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
        ValidateBuildProfile(buildElement, violations);
        ValidateBuildOutput(payload, violations);
        ValidateBuildSummary(buildElement, violations);
        ValidateBuildLogs(buildElement, violations);
        ValidateBuildGenerations(buildElement, violations);
        ValidateBuildLogCompletionReason(buildElement, violations);
        ValidateVerifier(payload, violations);
        ValidateClaimEvidence(buildElement, claimElement, claimPath, claimId, violations);
        if (BuildClaimCodes.UnityBuildCompleted.EqualsValue(claimId))
        {
            ValidateCompletedClaim(buildElement, claimElement, claimPath, violations);
        }

        if (BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId))
        {
            ValidateSucceededClaim(buildElement, claimElement, claimPath, violations);
        }

        if (BuildClaimCodes.UnityBuildValidForGeneration.EqualsValue(claimId))
        {
            ValidateGenerationClaim(buildElement, claimElement, claimPath, violations);
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

        if (!TryReadString(reportElement, "digest", out var digest))
        {
            AddViolation(violations, BuildPropertyPath(reportPath, "digest"), $"Build report {expectedKind} must declare digest.");
        }
        else if (!IsSha256LowerHex(digest))
        {
            AddViolation(violations, BuildPropertyPath(reportPath, "digest"), $"Build report {expectedKind} digest must be lowercase SHA-256 hex.");
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

    private static void ValidateBuildProfile (
        JsonElement buildElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!buildElement.TryGetProperty("profile", out var profileElement) || profileElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, "$.build.profile", "Build payload must declare profile.");
            return;
        }

        if (!TryReadString(profileElement, "path", out _))
        {
            AddViolation(violations, "$.build.profile.path", "Build profile must declare path.");
        }

        if (!TryReadString(profileElement, "digest", out _))
        {
            AddViolation(violations, "$.build.profile.digest", "Build profile must declare digest.");
        }
    }

    private static void ValidateBuildSummary (
        JsonElement buildElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!buildElement.TryGetProperty("summary", out var summaryElement) || summaryElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, "$.build.summary", "Build payload must declare summary.");
            return;
        }

        if (!TryReadString(summaryElement, "reportRef", out var reportRef))
        {
            AddViolation(violations, "$.build.summary.reportRef", "Build summary must declare reportRef.");
        }
        else if (!string.Equals(reportRef, BuildReportRefs.BuildReport, StringComparison.Ordinal))
        {
            AddViolation(violations, "$.build.summary.reportRef", "Build summary reportRef must resolve to reports.buildReport.");
        }
    }

    private static void ValidateBuildLogs (
        JsonElement buildElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!buildElement.TryGetProperty("logs", out var logsElement) || logsElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, "$.build.logs", "Build payload must declare logs.");
            return;
        }

        if (!TryReadString(logsElement, "reportRef", out var reportRef))
        {
            AddViolation(violations, "$.build.logs.reportRef", "Build logs must declare reportRef.");
        }
        else if (!string.Equals(reportRef, BuildReportRefs.BuildLog, StringComparison.Ordinal))
        {
            AddViolation(violations, "$.build.logs.reportRef", "Build logs reportRef must resolve to reports.buildLog.");
        }
    }

    private static void ValidateBuildGenerations (
        JsonElement buildElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadGenerations(buildElement, out var generationsElement))
        {
            AddViolation(violations, "$.build.generations", "Build payload must declare generations.");
            return;
        }

        ValidateGenerationSnapshot(generationsElement, "before", "$.build.generations.before", violations);
        ValidateGenerationSnapshot(generationsElement, "after", "$.build.generations.after", violations);
        ValidateGenerationSnapshot(generationsElement, "validFor", "$.build.generations.validFor", violations);
        ValidateValidForGenerationSnapshot(generationsElement, violations);
    }

    private static void ValidateBuildLogCompletionReason (
        JsonElement buildElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadBuildResult(buildElement, out var result)
            || !buildElement.TryGetProperty("logs", out var logsElement)
            || logsElement.ValueKind != JsonValueKind.Object
            || !TryReadString(logsElement, "completionReason", out var completionReason)
            || !ContractLiteralCodec.TryParse<IpcBuildReportResult>(result, out var parsedResult)
            || !ContractLiteralCodec.TryParse<IpcBuildLogCompletionReason>(completionReason, out var parsedCompletionReason))
        {
            return;
        }

        var expectedCompletionReason = IpcBuildLogCompletionReasonResolver.FromReportResult(parsedResult);
        if (parsedCompletionReason != expectedCompletionReason)
        {
            AddViolation(
                violations,
                "$.build.logs.completionReason",
                "Build log completionReason must match the BuildReport result.");
        }
    }

    private static void ValidateGenerationClaim (
        JsonElement buildElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadString(claimElement, "status", out var status))
        {
            return;
        }

        if (HasCompleteGenerationSnapshots(buildElement))
        {
            var passedLiteral = ContractLiteralCodec.ToValue(BuildClaimStatus.Passed);
            if (!string.Equals(status, passedLiteral, StringComparison.Ordinal))
            {
                AddViolation(
                    violations,
                    BuildPropertyPath(claimPath, "status"),
                    "UNITY_BUILD_VALID_FOR_GENERATION must be passed for complete generation snapshots.");
            }

            return;
        }

        var indeterminateLiteral = ContractLiteralCodec.ToValue(BuildClaimStatus.Indeterminate);
        if (!string.Equals(status, indeterminateLiteral, StringComparison.Ordinal)
            && !string.Equals(status, UnverifiedClaimStatus, StringComparison.Ordinal))
        {
            AddViolation(
                violations,
                BuildPropertyPath(claimPath, "status"),
                "UNITY_BUILD_VALID_FOR_GENERATION must be indeterminate or unverified when generation snapshots are incomplete.");
        }
    }

    private static bool HasCompleteGenerationSnapshots (JsonElement buildElement)
    {
        if (!TryReadGenerations(buildElement, out var generationsElement))
        {
            return false;
        }

        return HasCompleteGenerationSnapshot(generationsElement, "before")
            && HasCompleteGenerationSnapshot(generationsElement, "after")
            && HasCompleteGenerationSnapshot(generationsElement, "validFor")
            && GenerationSnapshotsMatch(generationsElement, "after", "validFor");
    }

    private static bool HasCompleteGenerationSnapshot (
        JsonElement generationsElement,
        string propertyName)
    {
        return TryReadGenerationSnapshot(generationsElement, propertyName, out var snapshot)
            && IsKnownGeneration(snapshot.CompileGeneration)
            && IsKnownGeneration(snapshot.DomainReloadGeneration)
            && IsKnownGeneration(snapshot.AssetRefreshGeneration);
    }

    private static bool TryReadGenerations (
        JsonElement buildElement,
        out JsonElement generationsElement)
    {
        return buildElement.TryGetProperty("generations", out generationsElement)
            && generationsElement.ValueKind == JsonValueKind.Object;
    }

    private static void ValidateGenerationSnapshot (
        JsonElement generationsElement,
        string propertyName,
        string propertyPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!generationsElement.TryGetProperty(propertyName, out var snapshotElement) || snapshotElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, propertyPath, $"Build generations must declare {propertyName}.");
            return;
        }

        ValidateGenerationValue(snapshotElement, "compileGeneration", propertyPath, violations);
        ValidateGenerationValue(snapshotElement, "domainReloadGeneration", propertyPath, violations);
        ValidateGenerationValue(snapshotElement, "assetRefreshGeneration", propertyPath, violations);
    }

    private static void ValidateValidForGenerationSnapshot (
        JsonElement generationsElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!HasCompleteGenerationSnapshot(generationsElement, "after")
            || !HasCompleteGenerationSnapshot(generationsElement, "validFor"))
        {
            return;
        }

        if (!GenerationSnapshotsMatch(generationsElement, "after", "validFor"))
        {
            AddViolation(
                violations,
                "$.build.generations.validFor",
                "Build generations.validFor must match generations.after.");
        }
    }

    private static void ValidateGenerationValue (
        JsonElement snapshotElement,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadString(snapshotElement, propertyName, out _))
        {
            AddViolation(violations, BuildPropertyPath(ownerPath, propertyName), "Build generation value must be a non-empty string.");
        }
    }

    private static bool IsKnownGeneration (string generation)
    {
        return !string.Equals(generation, UnknownGeneration, StringComparison.Ordinal);
    }

    private static bool GenerationSnapshotsMatch (
        JsonElement generationsElement,
        string leftPropertyName,
        string rightPropertyName)
    {
        return TryReadGenerationSnapshot(generationsElement, leftPropertyName, out var left)
            && TryReadGenerationSnapshot(generationsElement, rightPropertyName, out var right)
            && GenerationSnapshotsMatch(left, right);
    }

    private static bool GenerationSnapshotsMatch (
        (string CompileGeneration, string DomainReloadGeneration, string AssetRefreshGeneration) left,
        (string CompileGeneration, string DomainReloadGeneration, string AssetRefreshGeneration) right)
    {
        return string.Equals(left.CompileGeneration, right.CompileGeneration, StringComparison.Ordinal)
            && string.Equals(left.DomainReloadGeneration, right.DomainReloadGeneration, StringComparison.Ordinal)
            && string.Equals(left.AssetRefreshGeneration, right.AssetRefreshGeneration, StringComparison.Ordinal);
    }

    private static bool TryReadGenerationSnapshot (
        JsonElement generationsElement,
        string propertyName,
        out (string CompileGeneration, string DomainReloadGeneration, string AssetRefreshGeneration) snapshot)
    {
        snapshot = default;
        if (!generationsElement.TryGetProperty(propertyName, out var snapshotElement) || snapshotElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryReadString(snapshotElement, "compileGeneration", out var compileGeneration)
            || !TryReadString(snapshotElement, "domainReloadGeneration", out var domainReloadGeneration)
            || !TryReadString(snapshotElement, "assetRefreshGeneration", out var assetRefreshGeneration))
        {
            return false;
        }

        snapshot = (compileGeneration, domainReloadGeneration, assetRefreshGeneration);
        return true;
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

        var seenEffects = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var effectElement in effectsElement.EnumerateArray())
        {
            if (effectElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(effectElement.GetString()))
            {
                AddViolation(violations, $"{effectsPath}[{index}]", "Build verifier effect must be a non-empty string.");
                index++;
                continue;
            }

            var effect = effectElement.GetString()!;
            if (!AllowedBuildEffects.Contains(effect))
            {
                AddViolation(violations, $"{effectsPath}[{index}]", "Build verifier effect must be one of the build effect literals.");
            }
            else if (!seenEffects.Add(effect))
            {
                AddViolation(violations, $"{effectsPath}[{index}]", "Build verifier effects must not contain duplicates.");
            }

            index++;
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

    private static void ValidateCompletedClaim (
        JsonElement buildElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadBuildResult(buildElement, out var result)
            || !TryReadString(claimElement, "status", out var status)
            || !ContractLiteralCodec.TryParse<IpcBuildReportResult>(result, out var parsedResult))
        {
            return;
        }

        var passedLiteral = ContractLiteralCodec.ToValue(BuildClaimStatus.Passed);
        var indeterminateLiteral = ContractLiteralCodec.ToValue(BuildClaimStatus.Indeterminate);
        var expectedStatus = parsedResult is IpcBuildReportResult.Succeeded or IpcBuildReportResult.Failed or IpcBuildReportResult.Canceled
            ? passedLiteral
            : indeterminateLiteral;
        if (!string.Equals(status, expectedStatus, StringComparison.Ordinal))
        {
            AddViolation(
                violations,
                BuildPropertyPath(claimPath, "status"),
                $"UNITY_BUILD_COMPLETED must be {expectedStatus} for the BuildReport result.");
        }
    }

    private static void ValidateClaimEvidence (
        JsonElement buildElement,
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (BuildClaimCodes.UnityBuildValidForGeneration.EqualsValue(claimId))
        {
            ValidateGenerationClaimEvidence(buildElement, claimElement, claimPath, violations);
            return;
        }

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

    private static void ValidateGenerationClaimEvidence (
        JsonElement buildElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!claimElement.TryGetProperty("evidence", out var evidenceElement) || evidenceElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "evidence"), "Build claim UNITY_BUILD_VALID_FOR_GENERATION must include evidence.");
            return;
        }

        foreach (var evidence in evidenceElement.EnumerateArray())
        {
            if (evidence.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (TryReadString(evidence, "evidenceRef", out var evidenceRef)
                && string.Equals(evidenceRef, BuildReportRefs.Build, StringComparison.Ordinal))
            {
                return;
            }

            if (EvidenceDataMatchesBuildGenerations(buildElement, evidence))
            {
                return;
            }
        }

        AddViolation(
            violations,
            BuildPropertyPath(claimPath, "evidence"),
            "UNITY_BUILD_VALID_FOR_GENERATION evidence must reference reports.build or include payload.build.generations data.");
    }

    private static bool EvidenceDataMatchesBuildGenerations (
        JsonElement buildElement,
        JsonElement evidenceElement)
    {
        if (!TryReadGenerations(buildElement, out var generationsElement)
            || !evidenceElement.TryGetProperty("data", out var dataElement)
            || dataElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return GenerationSnapshotsMatch(generationsElement, "before", dataElement, "before")
            && GenerationSnapshotsMatch(generationsElement, "after", dataElement, "after")
            && GenerationSnapshotsMatch(generationsElement, "validFor", dataElement, "validFor");
    }

    private static bool GenerationSnapshotsMatch (
        JsonElement leftGenerationsElement,
        string leftPropertyName,
        JsonElement rightGenerationsElement,
        string rightPropertyName)
    {
        return TryReadGenerationSnapshot(leftGenerationsElement, leftPropertyName, out var left)
            && TryReadGenerationSnapshot(rightGenerationsElement, rightPropertyName, out var right)
            && GenerationSnapshotsMatch(left, right);
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

    private static bool IsSha256LowerHex (string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
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
        for (var i = 0; i < violations.Count; i++)
        {
            var violation = violations[i];
            if (string.Equals(violation.Path, path, StringComparison.Ordinal)
                && string.Equals(violation.Message, message, StringComparison.Ordinal))
            {
                return;
            }
        }

        violations.Add(new AssuranceSemanticInvariantViolation(path, message));
    }
}
