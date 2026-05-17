using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Validates compile-specific semantic invariants inside the common assurance payload shape. </summary>
internal sealed class CompileAssuranceSemanticInvariantRule : IAssuranceSemanticInvariantRule
{
    /// <inheritdoc />
    public void ValidateClaim (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        if (!IsCompileClaim(claimId) || !IsCompilePayload(payload))
        {
            return;
        }

        if (!payload.TryGetProperty("compile", out var compileElement) || compileElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, "$.compile", "Compile payload must contain compile evidence.");
            return;
        }

        if (CompileClaimCodes.UnityCompileNoErrors.EqualsValue(claimId))
        {
            ValidateVerifier(payload, violations);
            ValidateRefresh(compileElement, violations);
            ValidateCompileNoErrorsClaim(compileElement, claimElement, claimPath, violations);
        }
        else if (CompileClaimCodes.UnityDomainReloadSettled.EqualsValue(claimId))
        {
            ValidateDomainReloadClaim(compileElement, claimElement, claimPath, violations);
        }
        else if (CompileClaimCodes.UnityLifecycleReadyAfterCompile.EqualsValue(claimId))
        {
            ValidateLifecycleClaim(compileElement, claimElement, claimPath, violations);
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
                && string.Equals(id, "compile", StringComparison.Ordinal))
            {
                ValidateEffects(verifierElement, verifierPath, violations);
                ValidatePrimaryClaims(verifierElement, verifierPath, violations);
                return;
            }

            index++;
        }

        AddViolation(violations, "$.verifiers", "Compile payload must contain verifier id 'compile'.");
    }

    private static void ValidateEffects (
        JsonElement verifierElement,
        string verifierPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var effectsPath = BuildPropertyPath(verifierPath, "effects");
        if (!verifierElement.TryGetProperty("effects", out var effectsElement) || effectsElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, effectsPath, "Compile verifier must declare computed effects.");
            return;
        }

        var effects = effectsElement
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? string.Empty)
            .ToArray();
        if (!effects.SequenceEqual(CompileEffectValues.All, StringComparer.Ordinal))
        {
            AddViolation(violations, effectsPath, "Compile verifier effects must be assetDatabaseRefresh, scriptCompilation, domainReload.");
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
            AddViolation(violations, primaryClaimsPath, "Compile verifier must declare primaryClaims.");
            return;
        }

        var primaryClaims = primaryClaimsElement
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString() ?? string.Empty)
            .ToArray();
        if (!primaryClaims.SequenceEqual(CompileClaimCodes.AllValues, StringComparer.Ordinal))
        {
            AddViolation(violations, primaryClaimsPath, "Compile verifier primaryClaims must match the compile claim set.");
        }
    }

    private static void ValidateRefresh (
        JsonElement compileElement,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var refreshPath = "$.compile.refresh";
        if (!TryReadObject(compileElement, "refresh", refreshPath, violations, out var refreshElement))
        {
            return;
        }

        if (!TryReadString(refreshElement, "origin", out var origin))
        {
            AddViolation(violations, BuildPropertyPath(refreshPath, "origin"), "Compile refresh origin must be present.");
            return;
        }

        if (!string.Equals(origin, CompileEffectValues.AssetDatabaseRefresh, StringComparison.Ordinal)
            && !string.Equals(origin, "diagnosticsRead", StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(refreshPath, "origin"), "Compile refresh origin must distinguish assetDatabaseRefresh from diagnosticsRead.");
        }
    }

    private static void ValidateCompileNoErrorsClaim (
        JsonElement compileElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadDiagnostics(compileElement, violations, out var errorCount))
        {
            return;
        }

        if (!TryReadString(claimElement, "status", out var status))
        {
            return;
        }

        if (errorCount > 0 && !string.Equals(status, CompileClaimStatusValues.Failed, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "UNITY_COMPILE_NO_ERRORS must fail when diagnostics.errorCount is greater than zero.");
        }

        if (errorCount == 0 && !string.Equals(status, CompileClaimStatusValues.Passed, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "UNITY_COMPILE_NO_ERRORS must pass when diagnostics.errorCount is zero.");
        }
    }

    private static void ValidateDomainReloadClaim (
        JsonElement compileElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var domainReloadPath = "$.compile.domainReload";
        if (!TryReadObject(compileElement, "domainReload", domainReloadPath, violations, out var domainReloadElement))
        {
            return;
        }

        if (TryReadBoolean(domainReloadElement, "reloadRequired", out var reloadRequired)
            && TryReadBoolean(domainReloadElement, "reloadObserved", out var reloadObserved)
            && !reloadRequired
            && reloadObserved)
        {
            AddViolation(violations, BuildPropertyPath(domainReloadPath, "reloadObserved"), "reloadObserved must be false when reloadRequired is false.");
        }

        if (TryReadBoolean(domainReloadElement, "settled", out var settled)
            && TryReadString(claimElement, "status", out var status)
            && !IsExpectedBooleanClaimStatus(settled, status))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "UNITY_DOMAIN_RELOAD_SETTLED must match domainReload.settled.");
        }

        if (reloadRequired)
        {
            return;
        }

        if (TryReadString(domainReloadElement, "generationBefore", out var before)
            && TryReadString(domainReloadElement, "generationAfter", out var after)
            && !string.Equals(before, after, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(domainReloadPath, "generationAfter"), "No-reload compile evidence must keep domain reload generation unchanged.");
        }
    }

    private static void ValidateLifecycleClaim (
        JsonElement compileElement,
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var lifecyclePath = "$.compile.lifecycle";
        if (!TryReadObject(compileElement, "lifecycle", lifecyclePath, violations, out var lifecycleElement))
        {
            return;
        }

        if (TryReadBoolean(lifecycleElement, "canAcceptExecutionRequests", out var canAcceptExecutionRequests)
            && TryReadString(claimElement, "status", out var status)
            && !IsExpectedBooleanClaimStatus(canAcceptExecutionRequests, status))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "UNITY_LIFECYCLE_READY_AFTER_COMPILE must match lifecycle.canAcceptExecutionRequests.");
        }
    }

    private static bool IsExpectedBooleanClaimStatus (
        bool evidencePassed,
        string status)
    {
        return evidencePassed
            ? string.Equals(status, CompileClaimStatusValues.Passed, StringComparison.Ordinal)
            : string.Equals(status, CompileClaimStatusValues.Failed, StringComparison.Ordinal);
    }

    private static bool TryReadDiagnostics (
        JsonElement compileElement,
        List<AssuranceSemanticInvariantViolation> violations,
        out int errorCount)
    {
        errorCount = 0;
        if (!TryReadObject(compileElement, "scriptCompilation", "$.compile.scriptCompilation", violations, out var scriptCompilationElement)
            || !TryReadObject(scriptCompilationElement, "diagnostics", "$.compile.scriptCompilation.diagnostics", violations, out var diagnosticsElement))
        {
            return false;
        }

        if (!diagnosticsElement.TryGetProperty("errorCount", out var errorCountElement) || errorCountElement.ValueKind != JsonValueKind.Number)
        {
            AddViolation(violations, "$.compile.scriptCompilation.diagnostics.errorCount", "Compile diagnostics must declare errorCount.");
            return false;
        }

        return errorCountElement.TryGetInt32(out errorCount);
    }

    private static bool IsCompileClaim (string claimId)
    {
        return CompileClaimCodes.UnityCompileNoErrors.EqualsValue(claimId)
            || CompileClaimCodes.UnityDomainReloadSettled.EqualsValue(claimId)
            || CompileClaimCodes.UnityLifecycleReadyAfterCompile.EqualsValue(claimId);
    }

    private static bool IsCompilePayload (JsonElement payload)
    {
        return payload.TryGetProperty("compile", out _);
    }

    private static bool TryReadObject (
        JsonElement owner,
        string propertyName,
        string path,
        List<AssuranceSemanticInvariantViolation> violations,
        out JsonElement value)
    {
        if (!owner.TryGetProperty(propertyName, out value) || value.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, path, "Required object property is missing or invalid.");
            return false;
        }

        return true;
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

    private static bool TryReadBoolean (
        JsonElement owner,
        string propertyName,
        out bool value)
    {
        value = false;
        if (!owner.TryGetProperty(propertyName, out var element)
            || (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        value = element.GetBoolean();
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
        violations.Add(new AssuranceSemanticInvariantViolation(path, message));
    }
}
