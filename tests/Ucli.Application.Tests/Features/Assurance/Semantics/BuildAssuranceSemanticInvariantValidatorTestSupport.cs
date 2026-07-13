using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

internal static class BuildAssuranceSemanticInvariantValidatorTestSupport
{
    private static readonly UcliCode[] BuildPipelineClaimCodes =
    [
        BuildClaimCodes.UnityBuildProfileResolved,
        BuildClaimCodes.UnityReadyForBuild,
        BuildClaimCodes.UnityBuildInputsResolved,
        BuildClaimCodes.UnityBuildRunnerResolved,
        BuildClaimCodes.UnityBuildCompleted,
        BuildClaimCodes.UnityBuildSucceeded,
        BuildClaimCodes.UnityBuildResultAccounted,
        BuildClaimCodes.UnityBuildReportAccounted,
        BuildClaimCodes.UnityBuildArtifactsAccounted,
        BuildClaimCodes.UnityBuildOutputDigested,
        BuildClaimCodes.UnityBuildLogsAccounted,
        BuildClaimCodes.UnityBuildProjectMutationAccounted,
        BuildClaimCodes.UnityBuildValidForGeneration,
    ];

    private static readonly IReadOnlyList<object> BuildPipelineVerifierEffects =
    [
        ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead),
        ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline),
        ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead),
        ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead),
        ContractLiteralCodec.ToValue(BuildEffect.UcliArtifactWrite),
        ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite),
        ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot),
        ContractLiteralCodec.ToValue(BuildEffect.ProjectMutationAudit),
    ];

    public static AssuranceSemanticInvariantValidationResult ValidateBuildPayload (string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return CreateBuildValidator().Validate(document.RootElement);
    }

    public static string CreateBuildPayload (
        string verdict = "pass",
        string buildResult = "succeeded",
        string buildCompletionReason = "completed",
        string buildCompletedClaimStatus = "passed",
        string buildSucceededClaimStatus = "passed",
        string buildGenerationClaimStatus = "passed",
        bool includeBuildLogReport = true,
        bool includeBuildLogDigest = true,
        string? buildSucceededEvidenceRef = null,
        bool buildGenerationEvidenceDataOnly = false,
        long? buildGenerationEvidenceDataValidForAssetRefreshGeneration = null,
        bool includeBuildProfile = true,
        string buildManifestRef = "buildOutputManifest",
        string summaryReportRef = "buildReport",
        string? summaryResult = null,
        string logsReportRef = "buildLog",
        IReadOnlyList<object>? verifierEffects = null,
        bool includeBuildGenerations = true,
        long? validForAssetRefreshGeneration = 2,
        bool includeBuildLogPath = true,
        bool includeBuildClaims = true,
        string? buildLogPath = null,
        bool includeBuildLogKind = false,
        string buildLogDigest = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
        bool useUnityBuildProfileInput = false,
        string unityBuildProfilePath = "Assets/BuildProfiles/Linux.asset")
    {
        var reports = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["build"] = new
            {
                path = "build.json",
                digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            },
            ["buildReport"] = new
            {
                path = "build-report.json",
                digest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            },
            ["buildOutputManifest"] = new
            {
                path = "output-manifest.json",
                digest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
            },
        };
        if (includeBuildLogReport)
        {
            var buildLogReport = includeBuildLogDigest
                ? new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = includeBuildLogPath ? buildLogPath ?? "build.log" : null,
                    ["digest"] = buildLogDigest,
                }
                : new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["path"] = buildLogPath ?? "build.log",
                };
            if (includeBuildLogKind)
            {
                buildLogReport["kind"] = "buildLog";
            }

            reports["buildLog"] = buildLogReport;
        }

        object? profile = includeBuildProfile
            ? new
            {
                path = ".ucli/build/player.json",
                digest = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
            }
            : null;
        object? generations = includeBuildGenerations
            ? CreateBuildGenerations(validForAssetRefreshGeneration)
            : null;
        var generationEvidenceData = buildGenerationEvidenceDataValidForAssetRefreshGeneration == null
            ? generations
            : CreateBuildGenerations(buildGenerationEvidenceDataValidForAssetRefreshGeneration);
        var inputScenes = new
        {
            source = useUnityBuildProfileInput ? "unityBuildProfile" : "explicit",
            paths = new[] { "Assets/Scenes/Main.unity" },
        };
        var inputOptions = new
        {
            development = true,
        };
        var inputs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["inputKind"] = useUnityBuildProfileInput ? "unityBuildProfile" : "explicit",
            ["target"] = new
            {
                stableName = "standaloneLinux64",
                unityBuildTarget = "StandaloneLinux64",
            },
            ["scenes"] = inputScenes,
            ["options"] = inputOptions,
        };
        if (useUnityBuildProfileInput)
        {
            inputs["unityBuildProfile"] = new
            {
                path = unityBuildProfilePath,
                digest = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            };
        }

        var claims = includeBuildClaims
            ? BuildPipelineClaimCodes
                .Select(code =>
                {
                    var status = ResolveBuildClaimStatus(
                        code,
                        buildCompletedClaimStatus,
                        buildSucceededClaimStatus,
                        buildGenerationClaimStatus);
                    return (object)new
                    {
                        id = code.Value,
                        status,
                        coverage = ResolveBuildClaimCoverage(status),
                        required = true,
                        verifierRef = "build",
                        evidence = CreateBuildEvidence(
                            code.Value,
                            buildResult,
                            buildSucceededEvidenceRef,
                            buildGenerationEvidenceDataOnly,
                            generationEvidenceData),
                        residualRisks = Array.Empty<object>(),
                    };
                })
                .ToArray()
            : Array.Empty<object>();
        return JsonSerializer.Serialize(new
        {
            verdict,
            build = new
            {
                profile,
                inputs,
                runner = new
                {
                    kind = "buildPipeline",
                    method = (string?)null,
                    invocation = new
                    {
                        arguments = new Dictionary<string, string>(StringComparer.Ordinal),
                        environment = new
                        {
                            variables = Array.Empty<string>(),
                            secrets = Array.Empty<string>(),
                        },
                    },
                },
                runnerResult = new
                {
                    source = "buildPipelineBuildReport",
                    status = buildResult,
                },
                output = new
                {
                    manifestRef = buildManifestRef,
                    manifestDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                },
                generations,
                summary = new
                {
                    result = summaryResult ?? buildResult,
                    reportRef = summaryReportRef,
                },
                logs = new
                {
                    reportRef = logsReportRef,
                    entryCount = 1,
                    errorCount = 0,
                    warningCount = 0,
                    completionReason = buildCompletionReason,
                    window = new
                    {
                        startedAtUtc = "2026-06-12T00:00:00+00:00",
                        completedAtUtc = "2026-06-12T00:00:03+00:00",
                        cursorStart = (string?)null,
                        cursorEnd = (string?)null,
                    },
                },
            },
            verifiers = new[]
            {
                new
                {
                    id = "build",
                    kind = "build",
                    deterministic = false,
                    required = true,
                    primaryClaims = BuildPipelineClaimCodes.Select(static code => code.Value).ToArray(),
                    effects = verifierEffects ?? BuildPipelineVerifierEffects,
                    reportRef = "build",
                },
            },
            claims,
            reports,
            residualRisks = Array.Empty<object>(),
        });
    }

    public static string BuildSucceededClaimPath (string propertyName)
    {
        for (var i = 0; i < BuildPipelineClaimCodes.Length; i++)
        {
            if (BuildPipelineClaimCodes[i].Equals(BuildClaimCodes.UnityBuildSucceeded))
            {
                return $"$.claims[{i}].{propertyName}";
            }
        }

        throw new InvalidOperationException("Build claim catalog must contain UNITY_BUILD_SUCCEEDED.");
    }

    public static string BuildCompletedClaimPath (string propertyName)
    {
        for (var i = 0; i < BuildPipelineClaimCodes.Length; i++)
        {
            if (BuildPipelineClaimCodes[i].Equals(BuildClaimCodes.UnityBuildCompleted))
            {
                return $"$.claims[{i}].{propertyName}";
            }
        }

        throw new InvalidOperationException("Build claim catalog must contain UNITY_BUILD_COMPLETED.");
    }

    public static string BuildGenerationClaimPath (string propertyName)
    {
        for (var i = 0; i < BuildPipelineClaimCodes.Length; i++)
        {
            if (BuildPipelineClaimCodes[i].Equals(BuildClaimCodes.UnityBuildValidForGeneration))
            {
                return $"$.claims[{i}].{propertyName}";
            }
        }

        throw new InvalidOperationException("Build claim catalog must contain UNITY_BUILD_VALID_FOR_GENERATION.");
    }

    private static AssuranceSemanticInvariantValidator CreateBuildValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new StaticCodeCatalog(BuildClaimCodes.All.Select(static code => CreateDescriptor(code.Value, CodeCatalogKindValues.Claim))),
            [new BuildAssuranceSemanticInvariantRule()]);
    }

    private static object CreateBuildGenerations (long? validForAssetRefreshGeneration)
    {
        object? validFor = validForAssetRefreshGeneration.HasValue
            ? new
            {
                compileGeneration = 2L,
                domainReloadGeneration = 1L,
                assetRefreshGeneration = validForAssetRefreshGeneration.Value,
                playModeGeneration = 1L,
            }
            : null;
        return new
        {
            before = new
            {
                compileGeneration = 1L,
                domainReloadGeneration = 1L,
                assetRefreshGeneration = 1L,
                playModeGeneration = 1L,
            },
            after = new
            {
                compileGeneration = 2L,
                domainReloadGeneration = 1L,
                assetRefreshGeneration = 2L,
                playModeGeneration = 1L,
            },
            validFor,
        };
    }

    private static string ResolveBuildClaimStatus (
        UcliCode code,
        string buildCompletedClaimStatus,
        string buildSucceededClaimStatus,
        string buildGenerationClaimStatus)
    {
        if (code.Equals(BuildClaimCodes.UnityBuildCompleted))
        {
            return buildCompletedClaimStatus;
        }

        if (code.Equals(BuildClaimCodes.UnityBuildSucceeded))
        {
            return buildSucceededClaimStatus;
        }

        if (code.Equals(BuildClaimCodes.UnityBuildValidForGeneration))
        {
            return buildGenerationClaimStatus;
        }

        return "passed";
    }

    private static string ResolveBuildClaimCoverage (string status)
    {
        return string.Equals(status, "indeterminate", StringComparison.Ordinal)
            || string.Equals(status, "unverified", StringComparison.Ordinal)
            ? "none"
            : "full";
    }

    private static object[] CreateBuildEvidence (
        string claimId,
        string buildResult,
        string? buildSucceededEvidenceRef,
        bool buildGenerationEvidenceDataOnly,
        object? buildGenerationEvidenceData)
    {
        if (BuildClaimCodes.UnityBuildValidForGeneration.EqualsValue(claimId) && buildGenerationEvidenceDataOnly)
        {
            return
            [
                new
                {
                    kind = "evidence",
                    data = buildGenerationEvidenceData,
                },
            ];
        }

        var evidenceRef = ResolveBuildEvidenceRef(claimId);
        if (BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId) && buildSucceededEvidenceRef != null)
        {
            evidenceRef = buildSucceededEvidenceRef;
        }

        if (BuildClaimCodes.UnityBuildResultAccounted.EqualsValue(claimId))
        {
            return
            [
                new
                {
                    kind = "evidence",
                    evidenceRef,
                    data = new
                    {
                        source = "buildPipelineBuildReport",
                        status = buildResult,
                    },
                },
            ];
        }

        return
        [
            new
            {
                kind = "evidence",
                evidenceRef,
            },
        ];
    }

    private static string ResolveBuildEvidenceRef (string claimId)
    {
        if (BuildClaimCodes.UnityBuildCompleted.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildSucceeded.EqualsValue(claimId)
            || BuildClaimCodes.UnityBuildReportAccounted.EqualsValue(claimId))
        {
            return "buildReport";
        }

        if (BuildClaimCodes.UnityBuildOutputDigested.EqualsValue(claimId))
        {
            return "buildOutputManifest";
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted.EqualsValue(claimId))
        {
            return "buildLog";
        }

        return "build";
    }
}
