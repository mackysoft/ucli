using System.Text.Json;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Semantics;

/// <summary> Validates cross-field semantic invariants in assurance command payloads. </summary>
internal sealed class AssuranceSemanticInvariantValidator
{
    private readonly ICodeCatalog codeCatalog;

    private readonly IReadOnlyList<IAssurancePayloadInvariantRule> payloadRules;

    private readonly IReadOnlyList<IAssuranceClaimInvariantRule> claimRules;

    /// <summary> Initializes a new instance of the <see cref="AssuranceSemanticInvariantValidator" /> class. </summary>
    /// <param name="codeCatalog"> The code catalog used to resolve claim and risk codes. </param>
    /// <param name="payloadRules"> Command-specific payload invariant rules. </param>
    /// <param name="claimRules"> Command-specific claim invariant rules. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="codeCatalog" /> is <see langword="null" />. </exception>
    public AssuranceSemanticInvariantValidator (
        ICodeCatalog codeCatalog,
        IEnumerable<IAssurancePayloadInvariantRule> payloadRules,
        IEnumerable<IAssuranceClaimInvariantRule> claimRules)
    {
        this.codeCatalog = codeCatalog ?? throw new ArgumentNullException(nameof(codeCatalog));
        this.payloadRules = CopyRequiredRules(payloadRules, nameof(payloadRules));
        this.claimRules = CopyRequiredRules(claimRules, nameof(claimRules));
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

        ValidateCommandSpecificPayload(payload, violations);
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

            var hasPath = TryReadOptionalString(reportProperty.Value, "path", reportPath, violations, out var path);
            var hasUri = TryReadOptionalString(reportProperty.Value, "uri", reportPath, violations, out var uri);
            if (hasPath == hasUri)
            {
                AddViolation(violations, reportPath, "Report entry must contain exactly one locator: path or uri.");
            }

            ValidateOptionalDigest(reportProperty.Value, reportPath, violations);

            reports[reportProperty.Name] = new ReportInfo(path, uri);
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

            var id = ReadAssuranceVerifierId(verifierElement, "id", verifierPath, violations);
            _ = TryReadRequiredContractLiteral(
                verifierElement,
                "kind",
                verifierPath,
                violations,
                out AssuranceVerifierKind _);
            var required = TryReadBoolean(verifierElement, "required", defaultValue: false);
            var primaryClaims = ReadCodeArray(verifierElement, "primaryClaims", verifierPath, violations);

            if (required && !primaryClaims.Any(static claim => claim is not null))
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

            UcliCode? id = null;
            if (TryReadRequiredString(claimElement, "id", claimPath, violations, out var readId))
            {
                var idPath = BuildPropertyPath(claimPath, "id");
                if (!UcliCode.TryCreate(readId, out id))
                {
                    AddViolation(violations, idPath, UcliCode.InvalidValueMessage);
                }
                else
                {
                    ValidateCatalogCode(id, CodeCatalogKind.Claim, idPath, violations);
                }
            }

            var verifierRef = ReadAssuranceVerifierId(claimElement, "verifierRef", claimPath, violations);
            AssuranceClaimStatus? status = TryReadRequiredContractLiteral(
                claimElement,
                "status",
                claimPath,
                violations,
                out AssuranceClaimStatus readStatus)
                    ? readStatus
                    : null;
            AssuranceCoverage? coverage = TryReadRequiredContractLiteral(
                claimElement,
                "coverage",
                claimPath,
                violations,
                out AssuranceCoverage readCoverage)
                    ? readCoverage
                    : null;
            var required = TryReadBoolean(claimElement, "required", defaultValue: false);

            if (id is not null)
            {
                ValidateCommandSpecificClaims(payload, claimElement, claimPath, id, violations);
            }
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
                var codePath = BuildPropertyPath(riskPath, "code");
                if (!UcliCode.TryCreate(code, out var codeValue))
                {
                    AddViolation(violations, codePath, UcliCode.InvalidValueMessage);
                }
                else
                {
                    ValidateCatalogCode(codeValue, CodeCatalogKind.Risk, codePath, violations);
                }
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
            if (claim.VerifierRef is null)
            {
                continue;
            }

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
                if (claimId is null)
                {
                    continue;
                }

                if (!claimsById.TryGetValue(claimId, out var claim))
                {
                    AddViolation(violations, claimPath, $"Verifier primary claim '{claimId}' does not resolve to payload.claims.");
                    continue;
                }

                if (claim.VerifierRef is not null
                    && verifier.Id is not null
                    && claim.VerifierRef != verifier.Id)
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

    private static Dictionary<AssuranceVerifierId, VerifierInfo> BuildVerifierIndex (
        IReadOnlyList<VerifierInfo> verifiers,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var verifierById = new Dictionary<AssuranceVerifierId, VerifierInfo>();
        for (var i = 0; i < verifiers.Count; i++)
        {
            var verifier = verifiers[i];
            if (verifier.Id is null)
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

    private static AssuranceVerifierId? ReadAssuranceVerifierId (
        JsonElement owner,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!TryReadRequiredString(owner, propertyName, ownerPath, violations, out var value))
        {
            return null;
        }

        if (AssuranceVerifierId.TryCreate(value, out var verifierId))
        {
            return verifierId;
        }

        AddViolation(
            violations,
            BuildPropertyPath(ownerPath, propertyName),
            "Verifier identifier is invalid.");
        return null;
    }

    private static Dictionary<UcliCode, ClaimInfo> BuildClaimIndex (
        IReadOnlyList<ClaimInfo> claims,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        var claimById = new Dictionary<UcliCode, ClaimInfo>();
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            if (claim.Id is null)
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
        if (!TryReadRequiredContractLiteral(payload, "verdict", "$", violations, out AssuranceVerdict actualVerdict)
            || claims.Any(static claim => !claim.Status.HasValue || !claim.Coverage.HasValue))
        {
            return;
        }

        var expectedVerdict = RecalculateVerdict(claims, payloadResidualRisks);
        if (actualVerdict != expectedVerdict)
        {
            AddViolation(
                violations,
                "$.verdict",
                $"Verdict must be '{TextVocabulary.GetText(expectedVerdict)}' when recalculated from claims and residual risks.");
        }
    }

    private void ValidateCatalogCode (
        UcliCode code,
        CodeCatalogKind expectedKind,
        string path,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!codeCatalog.TryFind(code, out var descriptor))
        {
            AddViolation(violations, path, $"Code '{code}' is not registered in the code catalog.");
            return;
        }

        if (descriptor.Kind != expectedKind)
        {
            AddViolation(violations, path, $"Code '{code}' must be registered as kind '{TextVocabulary.GetText(expectedKind)}'.");
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

    private void ValidateCommandSpecificPayload (
        JsonElement payload,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        for (var i = 0; i < payloadRules.Count; i++)
        {
            payloadRules[i].ValidatePayload(payload, violations);
        }
    }

    private void ValidateCommandSpecificClaims (
        JsonElement payload,
        JsonElement claimElement,
        string claimPath,
        UcliCode claimId,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        for (var i = 0; i < claimRules.Count; i++)
        {
            claimRules[i].ValidateClaim(payload, claimElement, claimPath, claimId, violations);
        }
    }

    private static IReadOnlyList<TRule> CopyRequiredRules<TRule> (
        IEnumerable<TRule> rules,
        string parameterName)
        where TRule : class
    {
        if (rules == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var copy = rules.ToArray();
        if (copy.Length == 0 || copy.Any(static rule => rule == null))
        {
            throw new ArgumentException("At least one non-null semantic invariant rule is required.", parameterName);
        }

        return copy;
    }

    private static AssuranceVerdict RecalculateVerdict (
        IReadOnlyList<ClaimInfo> claims,
        IReadOnlyList<ResidualRiskInfo> payloadResidualRisks)
    {
        var claimStates = new AssuranceVerdictClaimState[claims.Count];
        for (var i = 0; i < claims.Count; i++)
        {
            var claim = claims[i];
            claimStates[i] = new AssuranceVerdictClaimState(
                claim.Status!.Value,
                claim.Coverage!.Value,
                claim.Required,
                claim.ResidualRisks.Any(static risk => risk.Blocking));
        }

        var residualRiskStates = new AssuranceVerdictResidualRiskState[payloadResidualRisks.Count];
        for (var i = 0; i < payloadResidualRisks.Count; i++)
        {
            residualRiskStates[i] = new AssuranceVerdictResidualRiskState(payloadResidualRisks[i].Blocking);
        }

        return AssuranceVerdictCalculator.Calculate(claimStates, residualRiskStates);
    }

    private static bool TryReadRequiredContractLiteral<TEnum> (
        JsonElement owner,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations,
        out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;
        if (!TryReadRequiredString(owner, propertyName, ownerPath, violations, out var literal))
        {
            return false;
        }

        if (TextVocabulary.TryGetValue(literal, out value))
        {
            return true;
        }

        AddViolation(
            violations,
            BuildPropertyPath(ownerPath, propertyName),
            $"Value must be a supported {typeof(TEnum).Name} contract literal.");
        return false;
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

    private static void ValidateOptionalDigest (
        JsonElement owner,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!owner.TryGetProperty("digest", out var digestElement)
            || digestElement.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var digest = digestElement.ValueKind == JsonValueKind.String
            ? digestElement.GetString()
            : null;
        if (!Sha256Digest.TryParse(digest, out _))
        {
            AddViolation(
                violations,
                BuildPropertyPath(ownerPath, "digest"),
                "Report digest must be a canonical lowercase SHA-256 digest.");
        }
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

    private static IReadOnlyList<UcliCode?> ReadCodeArray (
        JsonElement owner,
        string propertyName,
        string ownerPath,
        List<AssuranceSemanticInvariantViolation> violations)
    {
        if (!owner.TryGetProperty(propertyName, out var arrayElement))
        {
            return Array.Empty<UcliCode?>();
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            AddViolation(violations, BuildPropertyPath(ownerPath, propertyName), "Property must be an array.");
            return Array.Empty<UcliCode?>();
        }

        var values = new List<UcliCode?>();
        var index = 0;
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                AddViolation(violations, $"{BuildPropertyPath(ownerPath, propertyName)}[{index}]", "Array item must be a string.");
                values.Add(null);
            }
            else
            {
                var rawValue = item.GetString();
                if (!UcliCode.TryCreate(rawValue, out var value))
                {
                    AddViolation(violations, $"{BuildPropertyPath(ownerPath, propertyName)}[{index}]", UcliCode.InvalidValueMessage);
                }

                values.Add(value);
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
        string? Path,
        string? Uri);

    private sealed record VerifierInfo (
        int Index,
        string Path,
        AssuranceVerifierId? Id,
        bool Required,
        IReadOnlyList<UcliCode?> PrimaryClaims);

    private sealed record ClaimInfo (
        int Index,
        string Path,
        UcliCode? Id,
        AssuranceVerifierId? VerifierRef,
        AssuranceClaimStatus? Status,
        AssuranceCoverage? Coverage,
        bool Required,
        IReadOnlyList<ResidualRiskInfo> ResidualRisks);

    private sealed record ResidualRiskInfo (bool Blocking);
}
