using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunCliOutputClaimFixtureFactory
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

    public static IReadOnlyList<BuildClaimOutput> CreateClaims (
        BuildOutput build,
        bool succeeded)
    {
        return BuildPipelineClaimCodes
            .Select(code => CreateClaim(code, build, succeeded))
            .ToArray();
    }

    private static BuildClaimOutput CreateClaim (
        UcliCode code,
        BuildOutput build,
        bool succeeded)
    {
        var status = ResolveClaimStatus(code, succeeded);
        return new BuildClaimOutput(
            Id: code.Value,
            Status: status,
            Coverage: ContractLiteralCodec.ToValue(BuildCoverage.Full),
            Required: true,
            VerifierRef: BuildReportRefs.Build,
            Statement: ResolveClaimStatement(code),
            Subject: CreateClaimSubject(code, build),
            Evidence: CreateClaimEvidence(code, build),
            ResidualRisks: []);
    }

    private static string ResolveClaimStatus (
        UcliCode code,
        bool succeeded)
    {
        if (BuildClaimCodes.UnityBuildSucceeded == code)
        {
            return ContractLiteralCodec.ToValue(succeeded ? BuildClaimStatus.Passed : BuildClaimStatus.Failed);
        }

        return ContractLiteralCodec.ToValue(BuildClaimStatus.Passed);
    }

    private static string ResolveClaimStatement (UcliCode code)
    {
        if (BuildClaimCodes.UnityBuildProfileResolved == code)
        {
            return "Build profile resolved to a deterministic input digest.";
        }

        if (BuildClaimCodes.UnityReadyForBuild == code)
        {
            return "Unity lifecycle was ready before BuildPipeline execution.";
        }

        if (BuildClaimCodes.UnityBuildInputsResolved == code)
        {
            return "Unity resolved BuildPipeline BuildTarget and scenes.";
        }

        if (BuildClaimCodes.UnityBuildRunnerResolved == code)
        {
            return "Build runner was resolved before invocation.";
        }

        if (BuildClaimCodes.UnityBuildCompleted == code)
        {
            return "Unity BuildPipeline reached a terminal BuildReport result.";
        }

        if (BuildClaimCodes.UnityBuildSucceeded == code)
        {
            return "Unity BuildPipeline reported a successful build result.";
        }

        if (BuildClaimCodes.UnityBuildResultAccounted == code)
        {
            return "Build runner terminal result was persisted in build metadata.";
        }

        if (BuildClaimCodes.UnityBuildReportAccounted == code)
        {
            return "BuildReport artifact was written and digested.";
        }

        if (BuildClaimCodes.UnityBuildArtifactsAccounted == code)
        {
            return "Build output artifacts were counted in the output manifest.";
        }

        if (BuildClaimCodes.UnityBuildOutputDigested == code)
        {
            return "Build output manifest digest was verified against the written artifact.";
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted == code)
        {
            return "Build log byte range was written and summarized.";
        }

        if (BuildClaimCodes.UnityBuildProjectMutationAccounted == code)
        {
            return "Project mutation audit was recorded according to build policy.";
        }

        return "Build artifacts declare the Unity lifecycle generations they are valid for.";
    }

    private static IReadOnlyDictionary<string, object?> CreateClaimSubject (
        UcliCode code,
        BuildOutput build)
    {
        if (BuildClaimCodes.UnityBuildProfileResolved == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["path"] = build.Profile.Path,
                ["digest"] = build.Profile.Digest,
            };
        }

        if (BuildClaimCodes.UnityReadyForBuild == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["lifecycleState"] = "ready",
            };
        }

        if (BuildClaimCodes.UnityBuildInputsResolved == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["buildTarget"] = build.Inputs.Target.StableName,
                ["sceneCount"] = build.Inputs.Scenes.Paths.Count,
            };
        }

        if (BuildClaimCodes.UnityBuildRunnerResolved == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "buildPipeline",
            };
        }

        if (BuildClaimCodes.UnityBuildCompleted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = build.Summary.Result,
            };
        }

        if (BuildClaimCodes.UnityBuildSucceeded == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["result"] = build.Summary.Result,
                ["errorCount"] = build.Summary.ErrorCount,
            };
        }

        if (BuildClaimCodes.UnityBuildResultAccounted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["source"] = build.RunnerResult.Source,
                ["status"] = build.RunnerResult.Status,
            };
        }

        if (BuildClaimCodes.UnityBuildReportAccounted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["reportRef"] = BuildReportRefs.BuildReport,
            };
        }

        if (BuildClaimCodes.UnityBuildArtifactsAccounted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manifestRef"] = BuildReportRefs.BuildOutputManifest,
                ["entryCount"] = build.Output.EntryCount,
                ["fileCount"] = build.Output.FileCount,
            };
        }

        if (BuildClaimCodes.UnityBuildOutputDigested == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manifestDigest"] = build.Output.ManifestDigest,
            };
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["reportRef"] = BuildReportRefs.BuildLog,
                ["entryCount"] = build.Logs.EntryCount,
                ["completionReason"] = build.Logs.CompletionReason,
            };
        }

        if (BuildClaimCodes.UnityBuildProjectMutationAccounted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = "forbid",
                ["coverage"] = "full",
                ["mutated"] = false,
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["compileGeneration"] = build.Generations.ValidFor.CompileGeneration,
            ["domainReloadGeneration"] = build.Generations.ValidFor.DomainReloadGeneration,
            ["assetRefreshGeneration"] = build.Generations.ValidFor.AssetRefreshGeneration,
        };
    }

    private static IReadOnlyList<BuildEvidenceOutput> CreateClaimEvidence (
        UcliCode code,
        BuildOutput build)
    {
        if (BuildClaimCodes.UnityBuildProfileResolved == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildProfile),
                    BuildReportRefs.Build,
                    build.Profile),
            ];
        }

        if (BuildClaimCodes.UnityReadyForBuild == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    ContractLiteralCodec.ToValue(BuildEffect.UnityLifecycleRead),
                    Data: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["lifecycleState"] = "ready",
                        ["compileGeneration"] = "compile-before",
                    }),
            ];
        }

        if (BuildClaimCodes.UnityBuildInputsResolved == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    ContractLiteralCodec.ToValue(BuildEvidenceKind.BuildInput),
                    BuildReportRefs.Build,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["buildTarget"] = build.Inputs.Target.StableName,
                        ["unityBuildTarget"] = build.Inputs.Target.UnityBuildTarget,
                        ["sceneSource"] = build.Inputs.Scenes.Source,
                        ["scenes"] = build.Inputs.Scenes.Paths,
                        ["buildOptions"] = "Development",
                    }),
            ];
        }

        if (BuildClaimCodes.UnityBuildRunnerResolved == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline), BuildReportRefs.Build)];
        }

        if (BuildClaimCodes.UnityBuildCompleted == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.UnityBuildPipeline), BuildReportRefs.BuildReport, build.Summary)];
        }

        if (BuildClaimCodes.UnityBuildSucceeded == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), BuildReportRefs.BuildReport, build.Summary)];
        }

        if (BuildClaimCodes.UnityBuildResultAccounted == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), BuildReportRefs.Build, build.RunnerResult)];
        }

        if (BuildClaimCodes.UnityBuildReportAccounted == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.UnityBuildReportRead), BuildReportRefs.BuildReport)];
        }

        if (BuildClaimCodes.UnityBuildArtifactsAccounted == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite), BuildReportRefs.Build, build.Output)];
        }

        if (BuildClaimCodes.UnityBuildOutputDigested == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.OutputManifestWrite), BuildReportRefs.BuildOutputManifest)];
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted == code)
        {
            return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.UnityLogWindowRead), BuildReportRefs.BuildLog, build.Logs)];
        }

        if (BuildClaimCodes.UnityBuildProjectMutationAccounted == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    ContractLiteralCodec.ToValue(BuildEffect.ProjectMutationAudit),
                    BuildReportRefs.Build,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["mode"] = "forbid",
                        ["coverage"] = "full",
                        ["mutated"] = false,
                        ["beforeDigest"] = new string('1', 64),
                        ["afterDigest"] = new string('1', 64),
                        ["items"] = Array.Empty<object>(),
                    }),
            ];
        }

        return [new BuildEvidenceOutput(ContractLiteralCodec.ToValue(BuildEffect.GenerationSnapshot), BuildReportRefs.Build, build.Generations)];
    }
}
