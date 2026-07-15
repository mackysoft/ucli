using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Semantics;

/// <summary> Validates verify-specific semantic invariants inside the common assurance payload shape. </summary>
internal sealed class VerifyAssuranceSemanticInvariantRule : IAssuranceClaimInvariantRule
{
    /// <inheritdoc />
    public void ValidateClaim (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        UcliCode claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        if (!VerifyClaimCodes.All.Contains(claimId) || !IsVerifyPayload(payload))
        {
            return;
        }

        ValidateUnavailablePostMutationClaim(claimElement, claimPath, claimId, violations);
        ValidateDiagnosticImpactPropagation(claimElement, claimPath, violations);
    }

    private static void ValidateUnavailablePostMutationClaim (
        JsonElement claimElement,
        string claimPath,
        UcliCode claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (VerifyClaimCodes.PostMutationObserved != claimId
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

        if (TryReadString(claimElement, "status", out var statusLiteral)
            && (!ContractLiteralCodec.TryParse(statusLiteral, out AssuranceClaimStatus status)
                || status != AssuranceClaimStatus.OutOfScope))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "Unavailable post-state claims must be outOfScope.");
        }

        if (TryReadString(claimElement, "coverage", out var coverageLiteral)
            && (!ContractLiteralCodec.TryParse(coverageLiteral, out AssuranceCoverage coverage)
                || coverage != AssuranceCoverage.None))
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "coverage"), "Unavailable post-state claims must have coverage none.");
        }
    }

    private static void ValidateDiagnosticImpactPropagation (
        JsonElement claimElement,
        string claimPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadFromResultDiagnosticImpact(
                claimElement,
                claimPath,
                out var diagnosticImpact,
                out var invalidImpactPath))
        {
            if (invalidImpactPath != null)
            {
                AddViolation(violations, invalidImpactPath, "Diagnostic impact must be a supported verify diagnostic impact.");
            }

            return;
        }

        if (diagnosticImpact == VerifyDiagnosticImpact.None)
        {
            return;
        }

        if (!TryReadString(claimElement, "status", out var statusLiteral)
            || !ContractLiteralCodec.TryParse(statusLiteral, out AssuranceClaimStatus status)
            || !TryReadString(claimElement, "coverage", out var coverageLiteral)
            || !ContractLiteralCodec.TryParse(coverageLiteral, out AssuranceCoverage coverage))
        {
            return;
        }

        if (diagnosticImpact == VerifyDiagnosticImpact.Partial
            && status == AssuranceClaimStatus.Passed
            && coverage == AssuranceCoverage.Full)
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "coverage"), "Partial diagnostics must not produce a clean full-coverage claim.");
            return;
        }

        if (diagnosticImpact == VerifyDiagnosticImpact.Indeterminate)
        {
            if (status == AssuranceClaimStatus.Passed)
            {
                AddViolation(violations, BuildPropertyPath(claimPath, "status"), "Indeterminate diagnostics must not produce a passed claim.");
            }

            if (coverage != AssuranceCoverage.None)
            {
                AddViolation(violations, BuildPropertyPath(claimPath, "coverage"), "Indeterminate diagnostics must produce coverage none.");
            }

            return;
        }

        if (diagnosticImpact == VerifyDiagnosticImpact.Error
            && status != AssuranceClaimStatus.Failed)
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "status"), "Error diagnostics must produce a failed claim.");
        }
    }

    private static bool TryReadFromResultDiagnosticImpact (
        JsonElement claimElement,
        string claimPath,
        out VerifyDiagnosticImpact diagnosticImpact,
        out string? invalidImpactPath)
    {
        diagnosticImpact = default;
        invalidImpactPath = null;
        if (!claimElement.TryGetProperty("evidence", out var evidenceElement) || evidenceElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var index = 0;
        foreach (var evidenceItem in evidenceElement.EnumerateArray())
        {
            if (evidenceItem.ValueKind != JsonValueKind.Object
                || !TryReadString(evidenceItem, "kind", out var kind)
                || !string.Equals(kind, "fromResultSummary", StringComparison.Ordinal)
                || !evidenceItem.TryGetProperty("data", out var dataElement)
                || dataElement.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            invalidImpactPath = $"{claimPath}.evidence[{index}].data.diagnosticImpact";
            return TryReadString(dataElement, "diagnosticImpact", out var diagnosticImpactLiteral)
                && ContractLiteralCodec.TryParse(diagnosticImpactLiteral, out diagnosticImpact);
        }

        return false;
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
