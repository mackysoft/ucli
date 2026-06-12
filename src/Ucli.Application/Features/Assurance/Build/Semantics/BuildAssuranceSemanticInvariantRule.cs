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
        "build",
        "buildReport",
        "buildOutputManifest",
        "buildLog",
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
        ValidateVerifier(payload, violations);
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
            if (!reportsElement.TryGetProperty(RequiredReportKeys[i], out _))
            {
                AddViolation(violations, "$.reports", $"Build payload must contain reports.{RequiredReportKeys[i]}.");
            }
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
                && string.Equals(id, "build", StringComparison.Ordinal))
            {
                ValidateVerifierEffects(verifierElement, verifierPath, violations);
                ValidatePrimaryClaims(verifierElement, verifierPath, violations);
                return;
            }

            index++;
        }

        AddViolation(violations, "$.verifiers", "Build payload must contain verifier id 'build'.");
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
