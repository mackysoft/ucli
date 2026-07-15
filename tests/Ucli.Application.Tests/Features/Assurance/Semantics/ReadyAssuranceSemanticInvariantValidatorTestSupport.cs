using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

internal static class ReadyAssuranceSemanticInvariantValidatorTestSupport
{
    public const string ReadyClaim = "UNITY_READY_EXECUTION";
    public const string CompileClaim = "UNITY_COMPILE_NO_ERRORS";
    public const string LogUnavailableRisk = "UNITY_LOG_UNAVAILABLE";

    public static AssuranceSemanticInvariantValidationResult ValidateReadyPayload (string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return CreateValidator().Validate(document.RootElement);
    }

    public static string CreateReadyPayload (
        string verdict = "pass",
        IReadOnlyList<object>? verifiers = null,
        IReadOnlyList<object>? claims = null,
        IReadOnlyDictionary<string, object>? reports = null,
        IReadOnlyList<object>? residualRisks = null)
    {
        return JsonSerializer.Serialize(new
        {
            verdict,
            verifiers = verifiers ?? [CreateVerifier()],
            claims = claims ?? [CreateClaim()],
            reports = reports ?? CreateReports(),
            residualRisks = residualRisks ?? [],
        });
    }

    public static string CreateReadyExecutionPayload (
        IReadOnlyDictionary<string, object>? validity = null,
        bool includeValidity = true)
    {
        var claim = CreateClaim(
            verifierRef: "ready.lifecycle",
            evidence: [],
            includeValidity: includeValidity,
            validity: validity ?? new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["kind"] = "probeOnly",
                ["guaranteesReusableSession"] = false,
            });

        return JsonSerializer.Serialize(new
        {
            verdict = "pass",
            target = "execution",
            requestedMode = "auto",
            resolvedMode = "oneshot",
            sessionKind = "transientProbe",
            verifiers = new[]
            {
                CreateVerifier(
                    id: "ready.lifecycle",
                    kind: "ready",
                    deterministic: false,
                    reportRef: null,
                    effects: []),
            },
            claims = new[] { claim },
            reports = new Dictionary<string, object>(StringComparer.Ordinal),
            residualRisks = Array.Empty<object>(),
        });
    }

    public static object CreateVerifier (
        string id = "ready",
        string kind = "ready",
        bool deterministic = true,
        bool required = true,
        IReadOnlyList<string>? primaryClaims = null,
        string? reportRef = "ready-log",
        IReadOnlyList<object>? effects = null)
    {
        var verifier = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["kind"] = kind,
            ["deterministic"] = deterministic,
            ["required"] = required,
            ["primaryClaims"] = primaryClaims ?? [ReadyClaim],
        };

        if (reportRef != null)
        {
            verifier["reportRef"] = reportRef;
        }

        if (effects != null)
        {
            verifier["effects"] = effects;
        }

        return verifier;
    }

    public static object CreateClaim (
        string id = ReadyClaim,
        string status = "passed",
        string coverage = "full",
        bool required = true,
        string verifierRef = "ready",
        IReadOnlyList<object>? evidence = null,
        IReadOnlyList<object>? residualRisks = null,
        bool includeValidity = false,
        IReadOnlyDictionary<string, object>? validity = null)
    {
        var claim = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["status"] = status,
            ["coverage"] = coverage,
            ["required"] = required,
            ["verifierRef"] = verifierRef,
            ["evidence"] = evidence ?? [CreateLogEvidence()],
            ["residualRisks"] = residualRisks ?? [],
        };

        if (includeValidity)
        {
            claim["validity"] = validity;
        }

        return claim;
    }

    public static object CreateLogEvidence (string evidenceRef = "ready-log")
    {
        return new
        {
            kind = "log",
            evidenceRef,
        };
    }

    public static object CreateRisk (string code, bool blocking = true)
    {
        return new
        {
            code,
            blocking,
        };
    }

    public static Dictionary<string, object> CreateReports ()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ready-log"] = CreateReport(),
        };
    }

    public static object CreateReport (
        string kind = "log",
        string? path = "artifacts/ready.log",
        string? digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
    {
        var report = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = kind,
        };

        if (path != null)
        {
            report["path"] = path;
        }

        if (digest != null)
        {
            report["digest"] = digest;
        }

        return report;
    }

    private static AssuranceSemanticInvariantValidator CreateValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new StaticCodeCatalog(
            [
                CreateDescriptor(ReadyClaim, CodeCatalogKind.Claim),
                CreateDescriptor(CompileClaim, CodeCatalogKind.Claim),
                CreateDescriptor(LogUnavailableRisk, CodeCatalogKind.Risk),
            ]),
            [new BuildAssuranceSemanticInvariantRule()],
            [new ReadyAssuranceSemanticInvariantRule()]);
    }
}
