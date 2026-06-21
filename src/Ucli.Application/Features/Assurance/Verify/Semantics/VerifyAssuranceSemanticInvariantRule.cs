using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Semantics;

/// <summary> Validates verify-specific semantic invariants inside the common assurance payload shape. </summary>
internal sealed class VerifyAssuranceSemanticInvariantRule : IAssuranceSemanticInvariantRule
{
    /// <inheritdoc />
    public void ValidatePayload (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);
    }

    /// <inheritdoc />
    public void ValidateClaim (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        if (!IsVerifyClaim(claimId) || !IsVerifyPayload(payload))
        {
            return;
        }

        ValidateUnavailablePostMutationClaim(claimElement, claimPath, claimId, violations);
        ValidateDiagnosticImpactPropagation(claimElement, claimPath, violations);
    }

    private static void ValidateUnavailablePostMutationClaim (
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!VerifyClaimCodes.PostMutationObserved.EqualsValue(claimId)
            || !claimElement.TryGetProperty("subject", out var subjectElement)
            || subjectElement.ValueKind != JsonValueKind.Object
            || !TryReadString(subjectElement, "reason", out var reason)
            || !string.Equals(reason, "expectedPostStateUnavailable", StringComparison.Ordinal))
        {
            return;
        }

        if (TryReadBoolean(claimElement, "required", out var required) && required)
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "required"), "Unavailable post-state claims must not be required.");
        }

        if (TryReadString(claimElement, "status", out var status)
            && !string.Equals(status, VerifyClaimStatusValues.OutOfScope, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "Unavailable post-state claims must be outOfScope.");
        }

        if (TryReadString(claimElement, "coverage", out var coverage)
            && !string.Equals(coverage, VerifyCoverageValues.None, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "coverage"), "Unavailable post-state claims must have coverage none.");
        }
    }

    private static void ValidateDiagnosticImpactPropagation (
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadFromResultDiagnosticImpact(claimElement, out var diagnosticImpact)
            || string.Equals(diagnosticImpact, IpcExecuteDiagnosticCoverageImpactNames.None, StringComparison.Ordinal))
        {
            return;
        }

        if (!TryReadString(claimElement, "status", out var status)
            || !TryReadString(claimElement, "coverage", out var coverage))
        {
            return;
        }

        if (string.Equals(diagnosticImpact, IpcExecuteDiagnosticCoverageImpactNames.Partial, StringComparison.Ordinal)
            && string.Equals(status, VerifyClaimStatusValues.Passed, StringComparison.Ordinal)
            && string.Equals(coverage, VerifyCoverageValues.Full, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "coverage"), "Partial diagnostics must not produce a clean full-coverage claim.");
            return;
        }

        if (string.Equals(diagnosticImpact, IpcExecuteDiagnosticCoverageImpactNames.Indeterminate, StringComparison.Ordinal))
        {
            if (string.Equals(status, VerifyClaimStatusValues.Passed, StringComparison.Ordinal))
            {
                AddViolation(violations, BuildPropertyPath(claimPath, "status"), "Indeterminate diagnostics must not produce a passed claim.");
            }

            if (!string.Equals(coverage, VerifyCoverageValues.None, StringComparison.Ordinal))
            {
                AddViolation(violations, BuildPropertyPath(claimPath, "coverage"), "Indeterminate diagnostics must produce coverage none.");
            }

            return;
        }

        if (string.Equals(diagnosticImpact, IpcExecuteDiagnosticSeverityNames.Error, StringComparison.Ordinal)
            && !string.Equals(status, VerifyClaimStatusValues.Failed, StringComparison.Ordinal))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "Error diagnostics must produce a failed claim.");
        }
    }

    private static bool TryReadFromResultDiagnosticImpact (
        JsonElement claimElement,
        out string diagnosticImpact)
    {
        diagnosticImpact = string.Empty;
        if (!claimElement.TryGetProperty("evidence", out var evidenceElement) || evidenceElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var evidenceItem in evidenceElement.EnumerateArray())
        {
            if (evidenceItem.ValueKind != JsonValueKind.Object
                || !TryReadString(evidenceItem, "kind", out var kind)
                || !string.Equals(kind, "fromResultSummary", StringComparison.Ordinal)
                || !evidenceItem.TryGetProperty("data", out var dataElement)
                || dataElement.ValueKind != JsonValueKind.Object
                || !TryReadString(dataElement, "diagnosticImpact", out diagnosticImpact))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsVerifyClaim (string claimId)
    {
        return VerifyClaimCodes.All.Any(code => code.EqualsValue(claimId));
    }

    private static bool IsVerifyPayload (JsonElement payload)
    {
        return payload.TryGetProperty("profile", out _)
            && payload.TryGetProperty("timeoutMilliseconds", out _);
    }

    private static bool TryReadString (
        JsonElement owner,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        return owner.TryGetProperty(propertyName, out var propertyElement)
            && propertyElement.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value = propertyElement.GetString() ?? string.Empty);
    }

    private static bool TryReadBoolean (
        JsonElement owner,
        string propertyName,
        out bool value)
    {
        value = false;
        if (!owner.TryGetProperty(propertyName, out var propertyElement))
        {
            return false;
        }

        if (propertyElement.ValueKind == JsonValueKind.True || propertyElement.ValueKind == JsonValueKind.False)
        {
            value = propertyElement.GetBoolean();
            return true;
        }

        return false;
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
