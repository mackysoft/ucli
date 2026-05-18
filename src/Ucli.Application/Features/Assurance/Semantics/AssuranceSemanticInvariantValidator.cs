using System.Text.Json;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Validates cross-field semantic invariants in assurance command payloads. </summary>
internal sealed class AssuranceSemanticInvariantValidator
{
    private const string VerdictPass = "pass";
    private const string VerdictFail = "fail";
    private const string VerdictIncomplete = "incomplete";

    private readonly ICodeCatalog codeCatalog;

    private readonly IReadOnlyList<IAssuranceSemanticInvariantRule> rules;

    /// <summary> Initializes a new instance of the <see cref="AssuranceSemanticInvariantValidator" /> class. </summary>
    /// <param name="codeCatalog"> The code catalog used to resolve claim and risk codes. </param>
    /// <param name="rules"> Command-specific semantic invariant rules. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="codeCatalog" /> is <see langword="null" />. </exception>
    public AssuranceSemanticInvariantValidator (
        ICodeCatalog codeCatalog,
        IEnumerable<IAssuranceSemanticInvariantRule>? rules = null)
    {
        this.codeCatalog = codeCatalog ?? throw new ArgumentNullException(nameof(codeCatalog));
        this.rules = rules?.ToArray() ?? Array.Empty<IAssuranceSemanticInvariantRule>();
    }

    /// <summary> Validates one assurance payload object. </summary>
    /// <param name="payload"> The assurance payload root. </param>
    /// <returns> The semantic invariant validation result. </returns>
    public AssuranceSemanticInvariantValidationResult Validate (JsonElement payload)
    {
        var violations = new List<AssuranceSemanticInvariantViolation>();
        if (payload.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, "$", "Assurance payload must be an object.");
            return new AssuranceSemanticInvariantValidationResult(violations);
        }

        var reports = ReadReports(payload, violations);
        var verifiers = ReadVerifiers(payload, reports, violations);
        var claims = ReadClaims(payload, reports, violations);
        var payloadResidualRisks = ReadResidualRisks(payload, "$.residualRisks", violations);

        ValidateClaimVerifierReferences(claims, verifiers, violations);
        ValidatePrimaryClaims(verifiers, claims, violations);
        ValidateVerdict(payload, claims, payloadResidualRisks, violations);

        return new AssuranceSemanticInvariantValidationResult(violations);
    }

    private Dictionary<string, ReportInfo> ReadReports (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var reports = new Dictionary<string, ReportInfo>(StringComparer.Ordinal);
        if (!payload.TryGetProperty("reports", out var reportsElement) || reportsElement.ValueKind != JsonValueKind.Object)
        {
            AddViolation(violations, "$.reports", "Assurance payload reports must be an object.");
            return reports;
        }

        foreach (var reportProperty in reportsElement.EnumerateObject())
        {
            var reportPath = BuildPropertyPath("$.reports", reportProperty.Name);
            if (reportProperty.Value.ValueKind != JsonValueKind.Object)
            {
                AddViolation(violations, reportPath, "Report entry must be an object.");
                continue;
            }

            if (!TryReadRequiredString(reportProperty.Value, "kind", reportPath, violations, out var kind))
            {
                continue;
            }

            var hasPath = TryReadOptionalString(reportProperty.Value, "path", reportPath, violations, out var path);
            var hasUri = TryReadOptionalString(reportProperty.Value, "uri", reportPath, violations, out var uri);
            if (hasPath == hasUri)
            {
                AddViolation(violations, reportPath, "Report entry must contain exactly one locator: path or uri.");
            }

            reports[reportProperty.Name] = new ReportInfo(kind, path, uri);
        }

        return reports;
    }

    private List<VerifierInfo> ReadVerifiers (
        JsonElement payload,
        IReadOnlyDictionary<string, ReportInfo> reports,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var verifiers = new List<VerifierInfo>();
        if (!payload.TryGetProperty("verifiers", out var verifiersElement) || verifiersElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, "$.verifiers", "Assurance payload verifiers must be an array.");
            return verifiers;
        }

        var index = 0;
        foreach (var verifierElement in verifiersElement.EnumerateArray())
        {
            var verifierPath = $"$.verifiers[{index}]";
            if (verifierElement.ValueKind != JsonValueKind.Object)
            {
                AddViolation(violations, verifierPath, "Verifier entry must be an object.");
                index++;
                continue;
            }

            var id = TryReadRequiredString(verifierElement, "id", verifierPath, violations, out var readId)
                ? readId
                : string.Empty;
            var required = TryReadBoolean(verifierElement, "required", defaultValue: false);
            var primaryClaims = ReadStringArray(verifierElement, "primaryClaims", verifierPath, violations);

            if (required && primaryClaims.Count == 0)
            {
                AddViolation(violations, BuildPropertyPath(verifierPath, "primaryClaims"), "Required verifier must declare at least one primary claim.");
            }

            if (TryReadOptionalString(verifierElement, "reportRef", verifierPath, violations, out var reportRef)
                && !reports.ContainsKey(reportRef!))
            {
                AddViolation(violations, BuildPropertyPath(verifierPath, "reportRef"), $"Verifier reportRef '{reportRef}' does not resolve to payload.reports.");
            }

            verifiers.Add(new VerifierInfo(index, verifierPath, id, required, primaryClaims));
            index++;
        }

        return verifiers;
    }

    private List<ClaimInfo> ReadClaims (
        JsonElement payload,
        IReadOnlyDictionary<string, ReportInfo> reports,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var claims = new List<ClaimInfo>();
        if (!payload.TryGetProperty("claims", out var claimsElement) || claimsElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, "$.claims", "Assurance payload claims must be an array.");
            return claims;
        }

        var index = 0;
        foreach (var claimElement in claimsElement.EnumerateArray())
        {
            var claimPath = $"$.claims[{index}]";
            if (claimElement.ValueKind != JsonValueKind.Object)
            {
                AddViolation(violations, claimPath, "Claim entry must be an object.");
                index++;
                continue;
            }

            var id = TryReadRequiredString(claimElement, "id", claimPath, violations, out var readId)
                ? readId
                : string.Empty;
            ValidateCatalogCode(id, CodeCatalogKindValues.Claim, BuildPropertyPath(claimPath, "id"), violations);

            var verifierRef = TryReadRequiredString(claimElement, "verifierRef", claimPath, violations, out var readVerifierRef)
                ? readVerifierRef
                : string.Empty;
            var status = TryReadRequiredString(claimElement, "status", claimPath, violations, out var readStatus)
                ? readStatus
                : string.Empty;
            var coverage = TryReadRequiredString(claimElement, "coverage", claimPath, violations, out var readCoverage)
                ? readCoverage
                : string.Empty;
            var required = TryReadBoolean(claimElement, "required", defaultValue: false);

            ValidateCommandSpecificRules(payload, claimElement, claimPath, id, violations);
            ResolveEvidenceReferences(claimElement, claimPath, reports, violations);
            var residualRisks = ReadResidualRisks(claimElement, BuildPropertyPath(claimPath, "residualRisks"), violations);

            claims.Add(new ClaimInfo(index, claimPath, id, verifierRef, status, coverage, required, residualRisks));
            index++;
        }

        return claims;
    }

    private List<ResidualRiskInfo> ReadResidualRisks (
        JsonElement owner,
        string residualRisksPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var risks = new List<ResidualRiskInfo>();
        var propertyName = GetLastPropertyName(residualRisksPath);
        if (!owner.TryGetProperty(propertyName, out var residualRisksElement))
        {
            return risks;
        }

        if (residualRisksElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, residualRisksPath, "Residual risks must be an array.");
            return risks;
        }

        var index = 0;
        foreach (var riskElement in residualRisksElement.EnumerateArray())
        {
            var riskPath = $"{residualRisksPath}[{index}]";
            if (riskElement.ValueKind != JsonValueKind.Object)
            {
                AddViolation(violations, riskPath, "Residual risk entry must be an object.");
                index++;
                continue;
            }

            if (TryReadRequiredString(riskElement, "code", riskPath, violations, out var code))
            {
                ValidateCatalogCode(code, CodeCatalogKindValues.Risk, BuildPropertyPath(riskPath, "code"), violations);
            }

            risks.Add(new ResidualRiskInfo(TryReadBoolean(riskElement, "blocking", defaultValue: false)));
            index++;
        }

        return risks;
    }

    private static void ValidateClaimVerifierReferences (
        IReadOnlyList<ClaimInfo> claims,
        IReadOnlyList<VerifierInfo> verifiers,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var verifierById = BuildVerifierIndex(verifiers, violations);

        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            if (!verifierById.TryGetValue(claim.VerifierRef, out var verifier))
            {
                AddViolation(violations, BuildPropertyPath(claim.Path, "verifierRef"), $"Claim verifierRef '{claim.VerifierRef}' does not resolve to payload.verifiers.");
                continue;
            }

            if (claim.Required && !verifier.Required)
            {
                AddViolation(violations, BuildPropertyPath(claim.Path, "required"), "Required claim must reference a required verifier.");
            }
        }
    }

    private static void ValidatePrimaryClaims (
        IReadOnlyList<VerifierInfo> verifiers,
        IReadOnlyList<ClaimInfo> claims,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var claimsById = BuildClaimIndex(claims, violations);

        for (var i = 0; i < verifiers.Count; i++)
        {
            var verifier = verifiers[i];
            for (var j = 0; j < verifier.PrimaryClaims.Count; j++)
            {
                var claimId = verifier.PrimaryClaims[j];
                var claimPath = $"{BuildPropertyPath(verifier.Path, "primaryClaims")}[{j}]";
                if (!claimsById.TryGetValue(claimId, out var claim))
                {
                    AddViolation(violations, claimPath, $"Verifier primary claim '{claimId}' does not resolve to payload.claims.");
                    continue;
                }

                if (!string.Equals(claim.VerifierRef, verifier.Id, StringComparison.Ordinal))
                {
                    AddViolation(violations, claimPath, $"Verifier primary claim '{claimId}' is owned by verifierRef '{claim.VerifierRef}'.");
                }

                if (verifier.Required && !claim.Required)
                {
                    AddViolation(violations, claimPath, $"Required verifier primary claim '{claimId}' must be required.");
                }
            }
        }
    }

    private static Dictionary<string, VerifierInfo> BuildVerifierIndex (
        IReadOnlyList<VerifierInfo> verifiers,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var verifierById = new Dictionary<string, VerifierInfo>(StringComparer.Ordinal);
        for (var i = 0; i < verifiers.Count; i++)
        {
            var verifier = verifiers[i];
            if (string.IsNullOrWhiteSpace(verifier.Id))
            {
                continue;
            }

            if (!verifierById.TryAdd(verifier.Id, verifier))
            {
                AddViolation(violations, BuildPropertyPath(verifier.Path, "id"), $"Verifier id '{verifier.Id}' must be unique.");
            }
        }

        return verifierById;
    }

    private static Dictionary<string, ClaimInfo> BuildClaimIndex (
        IReadOnlyList<ClaimInfo> claims,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var claimById = new Dictionary<string, ClaimInfo>(StringComparer.Ordinal);
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            if (string.IsNullOrWhiteSpace(claim.Id))
            {
                continue;
            }

            if (!claimById.TryAdd(claim.Id, claim))
            {
                AddViolation(violations, BuildPropertyPath(claim.Path, "id"), $"Claim id '{claim.Id}' must be unique.");
            }
        }

        return claimById;
    }

    private static void ValidateVerdict (
        JsonElement payload,
        IReadOnlyList<ClaimInfo> claims,
        IReadOnlyList<ResidualRiskInfo> payloadResidualRisks,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadRequiredString(payload, "verdict", "$", violations, out var actualVerdict))
        {
            return;
        }

        var expectedVerdict = RecalculateVerdict(claims, payloadResidualRisks);
        if (!string.Equals(actualVerdict, expectedVerdict, StringComparison.Ordinal))
        {
            AddViolation(violations, "$.verdict", $"Verdict must be '{expectedVerdict}' when recalculated from claims and residual risks.");
        }
    }

    private void ValidateCatalogCode (
        string code,
        string expectedKind,
        string path,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        if (!UcliCode.TryCreate(code, out var codeValue))
        {
            AddViolation(violations, path, UcliCode.InvalidValueMessage);
            return;
        }

        if (!codeCatalog.TryFind(codeValue, out var descriptor))
        {
            AddViolation(violations, path, $"Code '{code}' is not registered in the code catalog.");
            return;
        }

        if (!string.Equals(descriptor.Kind, expectedKind, StringComparison.Ordinal))
        {
            AddViolation(violations, path, $"Code '{code}' must be registered as kind '{expectedKind}'.");
        }
    }

    private static void ResolveEvidenceReferences (
        JsonElement claimElement,
        string claimPath,
        IReadOnlyDictionary<string, ReportInfo> reports,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!claimElement.TryGetProperty("evidence", out var evidenceElement))
        {
            return;
        }

        if (evidenceElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, BuildPropertyPath(claimPath, "evidence"), "Claim evidence must be an array.");
            return;
        }

        var index = 0;
        foreach (var evidenceItem in evidenceElement.EnumerateArray())
        {
            var evidencePath = $"{BuildPropertyPath(claimPath, "evidence")}[{index}]";
            if (evidenceItem.ValueKind != JsonValueKind.Object)
            {
                AddViolation(violations, evidencePath, "Evidence entry must be an object.");
                index++;
                continue;
            }

            if (TryReadOptionalString(evidenceItem, "evidenceRef", evidencePath, violations, out var evidenceRef)
                && !reports.ContainsKey(evidenceRef!))
            {
                AddViolation(violations, BuildPropertyPath(evidencePath, "evidenceRef"), $"Evidence reference '{evidenceRef}' does not resolve to payload.reports.");
            }

            index++;
        }
    }

    private void ValidateCommandSpecificRules (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        string claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            rules[i].ValidateClaim(payload, claimElement, claimPath, claimId, violations);
        }
    }

    private static string RecalculateVerdict (
        IReadOnlyList<ClaimInfo> claims,
        IReadOnlyList<ResidualRiskInfo> payloadResidualRisks)
    {
        if (payloadResidualRisks.Any(static risk => risk.Blocking)
            || claims.Any(static claim => claim.ResidualRisks.Any(static risk => risk.Blocking)))
        {
            return VerdictFail;
        }

        var requiredClaims = claims.Where(static claim => claim.Required).ToArray();
        if (requiredClaims.Any(static claim => string.Equals(claim.Status, "failed", StringComparison.Ordinal)))
        {
            return VerdictFail;
        }

        if (requiredClaims.Any(static claim =>
                !string.Equals(claim.Status, "passed", StringComparison.Ordinal)
                || !string.Equals(claim.Coverage, "full", StringComparison.Ordinal)))
        {
            return VerdictIncomplete;
        }

        return VerdictPass;
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

    private static bool TryReadOptionalString (
        JsonElement owner,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations,
        out string? value)
    {
        value = null;
        if (!owner.TryGetProperty(propertyName, out var propertyElement) || propertyElement.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (propertyElement.ValueKind != JsonValueKind.String)
        {
            AddViolation(violations, BuildPropertyPath(ownerPath, propertyName), "Optional string property must be a string when present.");
            return false;
        }

        value = propertyElement.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadBoolean (
        JsonElement owner,
        string propertyName,
        bool defaultValue)
    {
        return owner.TryGetProperty(propertyName, out var propertyElement)
            && (propertyElement.ValueKind == JsonValueKind.True || propertyElement.ValueKind == JsonValueKind.False)
            ? propertyElement.GetBoolean()
            : defaultValue;
    }

    private static IReadOnlyList<string> ReadStringArray (
        JsonElement owner,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!owner.TryGetProperty(propertyName, out var arrayElement))
        {
            return Array.Empty<string>();
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, BuildPropertyPath(ownerPath, propertyName), "Property must be an array.");
            return Array.Empty<string>();
        }

        var values = new List<string>();
        var index = 0;
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                AddViolation(violations, $"{BuildPropertyPath(ownerPath, propertyName)}[{index}]", "Array item must be a string.");
            }
            else
            {
                values.Add(item.GetString() ?? string.Empty);
            }

            index++;
        }

        return values;
    }

    private static string GetLastPropertyName (string path)
    {
        var index = path.LastIndexOf(".", StringComparison.Ordinal);
        return index >= 0 ? path[(index + 1)..] : path;
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

    private sealed record ReportInfo (
        string Kind,
        string? Path,
        string? Uri);

    private sealed record VerifierInfo (
        int Index,
        string Path,
        string Id,
        bool Required,
        IReadOnlyList<string> PrimaryClaims);

    private sealed record ClaimInfo (
        int Index,
        string Path,
        string Id,
        string VerifierRef,
        string Status,
        string Coverage,
        bool Required,
        IReadOnlyList<ResidualRiskInfo> ResidualRisks);

    private sealed record ResidualRiskInfo (bool Blocking);
}
