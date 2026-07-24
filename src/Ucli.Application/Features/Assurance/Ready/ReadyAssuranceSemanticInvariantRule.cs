using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Validates ready-specific semantic invariants inside the common assurance payload shape. </summary>
internal sealed class ReadyAssuranceSemanticInvariantRule : IAssuranceClaimInvariantRule
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

        if (!ReadyClaimCodes.All.Contains(claimId) || !IsReadyPayload(payload))
        {
            return;
        }

        var validityPath = BuildPropertyPath(claimPath, "validity");
        if (!claimElement.TryGetProperty("validity", out var validityElement) || validityElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, validityPath, "Ready claim validity must be an object.");
            return;
        }

        if (!TryReadRequiredString(validityElement, "kind", validityPath, violations, out var kindLiteral))
        {
            return;
        }

        var hasDefinedKind = TextVocabulary.TryGetValue(kindLiteral, out ReadyValidityKind kind);
        if (!hasDefinedKind)
        {
            AddViolation(violations, BuildPropertyPath(validityPath, "kind"), "Ready claim validity kind must be sessionBound or probeOnly.");
        }

        if (!validityElement.TryGetProperty("guaranteesReusableSession", out var guaranteeElement)
            || (guaranteeElement.ValueKind != JsonValueKind.True && guaranteeElement.ValueKind != JsonValueKind.False))
        {
            AddViolation(violations, BuildPropertyPath(validityPath, "guaranteesReusableSession"), "Ready claim validity must declare guaranteesReusableSession.");
            return;
        }

        var guaranteesReusableSession = guaranteeElement.GetBoolean();
        if (hasDefinedKind && kind == ReadyValidityKind.ProbeOnly && guaranteesReusableSession)
        {
            AddViolation(violations, BuildPropertyPath(validityPath, "guaranteesReusableSession"), "Probe-only ready validity must not guarantee a reusable session.");
        }

        if (IsAutoOneshotReadyPayload(payload) && guaranteesReusableSession)
        {
            AddViolation(violations, BuildPropertyPath(validityPath, "guaranteesReusableSession"), "ready --mode auto resolved to oneshot must not guarantee a reusable session.");
        }
    }

    private static bool IsAutoOneshotReadyPayload (JsonElement payload)
    {
        return payload.TryGetProperty("requestedMode", out var requestedModeElement)
            && requestedModeElement.ValueKind == JsonValueKind.String
            && TextVocabulary.TryGetValue(
                requestedModeElement.GetString(),
                out AssuranceRequestedExecutionMode requestedMode)
            && requestedMode == AssuranceRequestedExecutionMode.Auto
            && payload.TryGetProperty("resolvedMode", out var resolvedModeElement)
            && resolvedModeElement.ValueKind == JsonValueKind.String
            && TextVocabulary.TryGetValue(
                resolvedModeElement.GetString(),
                out AssuranceResolvedExecutionMode resolvedMode)
            && resolvedMode == AssuranceResolvedExecutionMode.Oneshot;
    }

    private static bool IsReadyPayload (JsonElement payload)
    {
        return payload.TryGetProperty("target", out _)
            && payload.TryGetProperty("requestedMode", out _)
            && payload.TryGetProperty("resolvedMode", out _)
            && payload.TryGetProperty("sessionKind", out _);
    }

    private static bool TryReadRequiredString (
        JsonElement owner,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations,
        out string value)
    {
        value = string.Empty;
        if (!owner.TryGetProperty(propertyName, out var propertyElement) || propertyElement.ValueKind != JsonValueKind.String)
        {
            AddViolation(violations, BuildPropertyPath(ownerPath, propertyName), "Required string property is missing or invalid.");
            return false;
        }

        value = propertyElement.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            AddViolation(violations, BuildPropertyPath(ownerPath, propertyName), "Required string property must not be empty.");
            return false;
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
        violations.Add(new AssuranceSemanticInvariantViolation(path, message));
    }
}
