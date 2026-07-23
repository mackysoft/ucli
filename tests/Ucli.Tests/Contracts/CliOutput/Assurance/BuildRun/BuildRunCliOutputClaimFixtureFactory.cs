using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Tests;

internal static class BuildRunCliOutputClaimFixtureFactory
{
    private static readonly AssuranceVerifierId BuildVerifierId = new("build");

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
            Id: code,
            Status: status,
            Coverage: AssuranceCoverage.Full,
            Required: true,
            VerifierRef: BuildVerifierId,
            Statement: ResolveClaimStatement(code),
            Subject: CreateClaimSubject(code, build),
            Evidence: CreateClaimEvidence(code, build),
            ResidualRisks: []);
    }

    private static AssuranceClaimStatus ResolveClaimStatus (
        UcliCode code,
        bool succeeded)
    {
        if (BuildClaimCodes.UnityBuildSucceeded == code)
        {
            return succeeded ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed;
        }

        return AssuranceClaimStatus.Passed;
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
                ["reportRef"] = TextVocabulary.GetText(BuildArtifactKind.BuildReport),
            };
        }

        if (BuildClaimCodes.UnityBuildArtifactsAccounted == code)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["manifestRef"] = TextVocabulary.GetText(BuildArtifactKind.BuildOutputManifest),
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
                ["reportRef"] = TextVocabulary.GetText(BuildArtifactKind.BuildLog),
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

        var validFor = build.Generations.ValidFor
            ?? throw new InvalidOperationException("Build generation validity must be present in the CLI output fixture.");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["compileGeneration"] = validFor.CompileGeneration,
            ["domainReloadGeneration"] = validFor.DomainReloadGeneration,
            ["assetRefreshGeneration"] = validFor.AssetRefreshGeneration,
            ["playModeGeneration"] = validFor.PlayModeGeneration,
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
                    TextVocabulary.GetText(BuildEvidenceKind.BuildProfile),
                    BuildArtifactKind.Build,
                    build.Profile),
            ];
        }

        if (BuildClaimCodes.UnityReadyForBuild == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    TextVocabulary.GetText(AssuranceEffect.UnityLifecycleRead),
                    EvidenceRef: null,
                    Data: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["lifecycleState"] = "ready",
                        ["compileGeneration"] = 1L,
                    }),
            ];
        }

        if (BuildClaimCodes.UnityBuildInputsResolved == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    TextVocabulary.GetText(BuildEvidenceKind.BuildInput),
                    BuildArtifactKind.Build,
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
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.UnityBuildPipeline), BuildArtifactKind.Build, Data: null)];
        }

        if (BuildClaimCodes.UnityBuildCompleted == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.UnityBuildPipeline), BuildArtifactKind.BuildReport, build.Summary)];
        }

        if (BuildClaimCodes.UnityBuildSucceeded == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.UnityBuildReportRead), BuildArtifactKind.BuildReport, build.Summary)];
        }

        if (BuildClaimCodes.UnityBuildResultAccounted == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.UnityBuildReportRead), BuildArtifactKind.Build, build.RunnerResult)];
        }

        if (BuildClaimCodes.UnityBuildReportAccounted == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.UnityBuildReportRead), BuildArtifactKind.BuildReport, Data: null)];
        }

        if (BuildClaimCodes.UnityBuildArtifactsAccounted == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.OutputManifestWrite), BuildArtifactKind.Build, build.Output)];
        }

        if (BuildClaimCodes.UnityBuildOutputDigested == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.OutputManifestWrite), BuildArtifactKind.BuildOutputManifest, Data: null)];
        }

        if (BuildClaimCodes.UnityBuildLogsAccounted == code)
        {
            return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.UnityLogWindowRead), BuildArtifactKind.BuildLog, build.Logs)];
        }

        if (BuildClaimCodes.UnityBuildProjectMutationAccounted == code)
        {
            return
            [
                new BuildEvidenceOutput(
                    TextVocabulary.GetText(AssuranceEffect.ProjectMutationAudit),
                    BuildArtifactKind.Build,
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

        return [new BuildEvidenceOutput(TextVocabulary.GetText(AssuranceEffect.GenerationSnapshot), BuildArtifactKind.Build, build.Generations)];
    }
}
