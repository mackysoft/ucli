using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCliOutputContractTests
{
    private const string BuildRunGoldenDirectory = "build-run";
    private const string BuildArtifactFixtureRoot = "tests/Ucli.Tests/GoldenFiles/Json/BuildRunArtifacts";
    private const string SuccessManifestDigest = "59a71d71a244ad7a978be0bbfe8f87328c036091bb4b5aefef9e89b855d82b9b";
    private const string FailedManifestDigest = "04d7d7e1eb32bc4521986964ba5e86b772fe46a3b50a73e4dd3783d4c4577d21";

    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string BuildDigest = new('a', 64);
    private static readonly string BuildReportDigest = new('b', 64);
    private static readonly string BuildOutputManifestArtifactDigest = new('c', 64);
    private static readonly string BuildLogDigest = new('d', 64);
    private static readonly string ProfileDigest = new('e', 64);
    private static readonly DateTimeOffset BuildStartedAtUtc = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BuildCompletedAtUtc = new(2026, 6, 1, 0, 0, 2, 500, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(GetGoldenCases))]
    [Trait("Size", "Small")]
    public void Create_WithBuildRunGoldenCase_MatchesGolden (
        string fileName,
        string caseName)
    {
        var result = CreateCommandResult(caseName);

        var json = new CommandResultJsonContractWriter().Write(result);

        JsonGoldenFileAssert.Matches(CliOutputGoldenFiles.GetPath(BuildRunGoldenDirectory, fileName), json);
    }

    [Theory]
    [InlineData("success.json")]
    [InlineData("build-report-failed.json")]
    [Trait("Size", "Small")]
    public void OkGoldenPayloads_SatisfyBuildSemanticInvariants (string fileName)
    {
        var payload = ReadGoldenPayload(fileName);

        var result = CreateBuildValidator().Validate(payload);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildReportFailureGolden_FixesVerifierFailureContract ()
    {
        using var document = ReadGoldenDocument("build-report-failed.json");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        Assert.Equal(IpcProtocol.StatusOk, root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("fail", payload.GetProperty("verdict").GetString());
        Assert.Equal("failed", payload.GetProperty("build").GetProperty("summary").GetProperty("result").GetString());
        Assert.Equal("failed", payload.GetProperty("build").GetProperty("logs").GetProperty("completionReason").GetString());
        Assert.Equal("passed", GetClaimStatus(payload, BuildClaimCodes.UnityBuildCompleted.Value));
        Assert.Equal("failed", GetClaimStatus(payload, BuildClaimCodes.UnityBuildSucceeded.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DirtySceneFailureGolden_FixesDirtyStateContract ()
    {
        using var document = ReadGoldenDocument("dirty-scene.json");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");
        var dirtyState = payload.GetProperty("dirtyState");
        var dirtyItem = dirtyState.GetProperty("items")[0];

        Assert.Equal(IpcProtocol.StatusError, root.GetProperty("status").GetString());
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent.Value, root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(dirtyState.GetProperty("checked").GetBoolean());
        Assert.True(dirtyState.GetProperty("dirty").GetBoolean());
        Assert.Equal("full", dirtyState.GetProperty("coverage").GetString());
        Assert.Equal("scene", dirtyItem.GetProperty("kind").GetString());
        Assert.Equal("Assets/Scenes/Main.unity", dirtyItem.GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("success.json", "success")]
    [InlineData("build-report-failed.json", "failed")]
    [Trait("Size", "Small")]
    public void BuildRunPayload_ProjectsBuildJsonFixtureFields (
        string fileName,
        string fixtureName)
    {
        var payloadBuild = ReadGoldenPayload(fileName).GetProperty("build");
        using var fixture = ReadBuildArtifactFixture(fixtureName, "build.json");
        var buildRoot = fixture.RootElement;

        AssertEquivalentJson(buildRoot.GetProperty("profile"), payloadBuild.GetProperty("profile"));
        AssertEquivalentJson(buildRoot.GetProperty("inputs"), payloadBuild.GetProperty("inputs"));
        AssertEquivalentJson(buildRoot.GetProperty("summary"), payloadBuild.GetProperty("summary"));
        AssertEquivalentJson(buildRoot.GetProperty("logs"), payloadBuild.GetProperty("logs"));
        AssertEquivalentJson(buildRoot.GetProperty("generations"), payloadBuild.GetProperty("generations"));
        Assert.Equal(
            buildRoot.GetProperty("runner").GetProperty("kind").GetString(),
            payloadBuild.GetProperty("runner").GetProperty("kind").GetString());
        Assert.Equal(
            buildRoot.GetProperty("runner").GetProperty("method").ValueKind,
            payloadBuild.GetProperty("runner").GetProperty("method").ValueKind);
        Assert.Equal(
            buildRoot.GetProperty("runnerResult").GetProperty("source").GetString(),
            payloadBuild.GetProperty("runnerResult").GetProperty("source").GetString());
        Assert.Equal(
            buildRoot.GetProperty("runnerResult").GetProperty("status").GetString(),
            payloadBuild.GetProperty("runnerResult").GetProperty("status").GetString());
        Assert.False(payloadBuild.TryGetProperty("buildTarget", out _));
        Assert.False(payloadBuild.TryGetProperty("scenes", out _));
        Assert.False(payloadBuild.TryGetProperty("options", out _));
    }

    [Theory]
    [InlineData("success.json")]
    [InlineData("build-report-failed.json")]
    [Trait("Size", "Small")]
    public void BuildRunPayloadGoldens_UseArtifactRelativeReportsWithoutLegacyKeys (string fileName)
    {
        var payload = ReadGoldenPayload(fileName);
        var reports = payload.GetProperty("reports");

        Assert.False(reports.TryGetProperty("buildResult", out _));
        foreach (var report in reports.EnumerateObject())
        {
            Assert.False(report.Value.TryGetProperty("kind", out _));
            Assert.False(report.Value.TryGetProperty("category", out _));
            var path = report.Value.GetProperty("path").GetString()!;
            Assert.False(IsAbsoluteLikePath(path), path);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRunPayloadSchema_UsesInputsAndReportRefs ()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "schemas",
            "v1",
            "cli-output",
            "payload",
            "build.run.schema.json")));
        var successSchema = document.RootElement.GetProperty("oneOf")[0];
        var buildProperties = successSchema.GetProperty("properties").GetProperty("build").GetProperty("properties");
        var reportProperties = successSchema.GetProperty("properties").GetProperty("reports").GetProperty("properties");

        Assert.True(buildProperties.TryGetProperty("inputs", out _));
        Assert.False(buildProperties.TryGetProperty("buildTarget", out _));
        Assert.False(buildProperties.TryGetProperty("scenes", out _));
        Assert.False(buildProperties.TryGetProperty("options", out _));
        Assert.Equal(
            [BuildReportRefs.Build, BuildReportRefs.BuildReport, BuildReportRefs.BuildOutputManifest, BuildReportRefs.BuildLog],
            reportProperties.EnumerateObject().Select(static property => property.Name).ToArray());
        foreach (var reportSchema in reportProperties.EnumerateObject())
        {
            var properties = reportSchema.Value.GetProperty("properties");
            Assert.True(properties.TryGetProperty("path", out _));
            Assert.True(properties.TryGetProperty("digest", out _));
            Assert.False(properties.TryGetProperty("kind", out _));
            Assert.False(properties.TryGetProperty("category", out _));
            Assert.False(properties.TryGetProperty("buildResult", out _));
        }
    }

    [Theory]
    [InlineData("success.json", "success")]
    [InlineData("build-report-failed.json", "failed")]
    [Trait("Size", "Small")]
    public void OutputManifestFixture_StoresCanonicalManifestDigest (
        string fileName,
        string fixtureName)
    {
        using var manifest = ReadBuildArtifactFixture(fixtureName, "output-manifest.json");
        var manifestRoot = manifest.RootElement;
        var payloadManifestDigest = ReadGoldenPayload(fileName)
            .GetProperty("build")
            .GetProperty("output")
            .GetProperty("manifestDigest")
            .GetString();

        var recalculatedDigest = new BuildOutputManifestJsonContractWriter()
            .CalculateManifestDigest(ReadOutputManifestContent(manifestRoot));

        Assert.Equal(recalculatedDigest, manifestRoot.GetProperty("manifestDigest").GetString());
        Assert.Equal(recalculatedDigest, payloadManifestDigest);
    }

    [Theory]
    [InlineData("missing-report-ref", "$.reports")]
    [InlineData("digest-only-entry", "$.reports.buildLog.path")]
    [InlineData("invalid-digest", "$.reports.buildLog.digest")]
    [InlineData("manifest-ref-mismatch", "$.build.output.manifestRef")]
    [Trait("Size", "Small")]
    public void BuildSemanticInvariant_RejectsInvalidReportContracts (
        string caseName,
        string expectedViolationPath)
    {
        var payload = CreateMutatedSuccessPayload(caseName);

        var result = CreateBuildValidator().Validate(payload);

        Assert.Contains(result.Violations, violation => string.Equals(violation.Path, expectedViolationPath, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRunArtifactFixtures_DoNotExposeRemovedArtifactsOrSha256Prefixes ()
    {
        string[] removedNames =
        [
            "build-summary.json",
            "profile-snapshot.json",
            "lifecycle.json",
            "manifest.json",
        ];
        var fixtureRoot = Path.Combine(RepositoryRoot, BuildArtifactFixtureRoot);
        var fixtureFiles = Directory.EnumerateFiles(fixtureRoot, "*.json", SearchOption.AllDirectories).ToArray();
        var cliOutputFiles = Directory.EnumerateFiles(
            Path.Combine(RepositoryRoot, "tests", "Ucli.Tests", "GoldenFiles", "Json", "CliOutput", BuildRunGoldenDirectory),
            "*.json",
            SearchOption.AllDirectories);

        foreach (var filePath in fixtureFiles)
        {
            Assert.DoesNotContain(Path.GetFileName(filePath), removedNames);
            var rawText = File.ReadAllText(filePath);
            Assert.DoesNotContain("sha256:", rawText, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(rawText);
            AssertNoRemovedPathSegment(document.RootElement, removedNames);
        }

        foreach (var filePath in cliOutputFiles)
        {
            Assert.DoesNotContain("sha256:", File.ReadAllText(filePath), StringComparison.Ordinal);
        }
    }

    public static IEnumerable<object[]> GetGoldenCases ()
    {
        yield return ["success.json", "success"];
        yield return ["build-report-failed.json", "build-report-failed"];
        yield return ["invalid-profile.json", "invalid-profile"];
        yield return ["unsupported-buildTarget.json", "unsupported-buildTarget"];
        yield return ["dirty-scene.json", "dirty-scene"];
        yield return ["buildTarget-module-missing.json", "buildTarget-module-missing"];
        yield return ["artifact-write-failed.json", "artifact-write-failed"];
        yield return ["output-manifest-failed.json", "output-manifest-failed"];
    }

    private static CommandResult CreateCommandResult (string caseName)
    {
        return caseName switch
        {
            "success" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(CreateOutput(succeeded: true))),
            "build-report-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Success(CreateOutput(succeeded: false))),
            "invalid-profile" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InvalidArgument("Build profile is invalid: /workspace/UnityProject/.ucli/build/player.json.", BuildErrorCodes.BuildProfileInvalid),
                CreateProject())),
            "unsupported-buildTarget" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InvalidArgument("Build profile inputs.buildTarget is unsupported: unknownTarget.", BuildErrorCodes.BuildTargetUnsupported),
                CreateProject())),
            "dirty-scene" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present."),
                CreateProject(),
                CreateDirtyState())),
            "buildTarget-module-missing" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ApplicationFailure.FromCode(BuildErrorCodes.BuildTargetModuleMissing, "buildTarget module is missing: standaloneLinux64."),
                CreateProject())),
            "artifact-write-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InternalError("Build artifacts could not be written.", BuildErrorCodes.BuildArtifactWriteFailed),
                CreateProject())),
            "output-manifest-failed" => BuildRunCommandResultFactory.Create(BuildExecutionResult.Failure(
                ExecutionError.InternalError("Build output manifest could not be generated.", BuildErrorCodes.BuildOutputManifestFailed),
                CreateProject())),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown build.run Golden case."),
        };
    }

    private static BuildExecutionOutput CreateOutput (bool succeeded)
    {
        var reportResult = succeeded
            ? ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded)
            : ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed);
        var manifestDigest = succeeded ? SuccessManifestDigest : FailedManifestDigest;
        var errorCount = succeeded ? 0 : 1;
        var warningCount = succeeded ? 1 : 0;
        var entryCount = succeeded ? 1 : 0;
        var fileCount = succeeded ? 2 : 0;
        var totalBytes = succeeded ? 33 : 0;
        var build = new BuildOutput(
            RunId: "build-run-1",
            Profile: new BuildProfileOutput("/workspace/UnityProject/.ucli/build/player.json", ProfileDigest),
            Inputs: new BuildInputsOutput(
                InputKind: "explicit",
                Target: new BuildTargetOutput("standaloneLinux64", "StandaloneLinux64"),
                Scenes: new BuildScenesOutput("explicit", ["Assets/Scenes/Main.unity"]),
                Options: new BuildOptionsOutput(Development: true)),
            Runner: new BuildRunnerOutput(
                Kind: "buildPipeline",
                Method: null,
                Invocation: new BuildRunnerInvocationOutput(
                    Arguments: new Dictionary<string, string>(StringComparer.Ordinal),
                    Environment: new BuildRunnerInvocationEnvironmentOutput(
                        Variables: [],
                        Secrets: []))),
            RunnerResult: new BuildRunnerResultOutput(
                Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                Status: reportResult),
            Output: new BuildArtifactOutput(
                ManifestRef: BuildReportRefs.BuildOutputManifest,
                ManifestDigest: manifestDigest,
                EntryCount: entryCount,
                FileCount: fileCount,
                TotalBytes: totalBytes),
            Generations: CreateGenerations(),
            Summary: new BuildSummaryOutput(
                Result: reportResult,
                DurationMilliseconds: succeeded ? 2500 : 2400,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                ReportRef: BuildReportRefs.BuildReport),
            Logs: new BuildLogsOutput(
                ReportRef: BuildReportRefs.BuildLog,
                EntryCount: succeeded ? 3 : 2,
                ErrorCount: errorCount,
                WarningCount: warningCount,
                CompletionReason: succeeded
                    ? ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed)
                    : ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                Window: new BuildLogWindowOutput(BuildStartedAtUtc, BuildCompletedAtUtc)));

        return new BuildExecutionOutput(
            Verdict: succeeded
                ? ContractLiteralCodec.ToValue(BuildVerdict.Pass)
                : ContractLiteralCodec.ToValue(BuildVerdict.Fail),
            Project: CreateProject(),
            Build: build,
            Verifiers:
            [
                new BuildVerifierOutput(
                    Id: BuildReportRefs.Build,
                    Kind: BuildReportRefs.Build,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: BuildClaimCodes.All.Select(static code => code.Value).ToArray(),
                    Effects: BuildPipelineEffectValues,
                    ReportRef: BuildReportRefs.Build),
            ],
            Claims: CreateClaims(build, succeeded),
            Reports: CreateReports(),
            ResidualRisks: []);
    }

    private static IReadOnlyList<BuildClaimOutput> CreateClaims (
        BuildOutput build,
        bool succeeded)
    {
        return BuildClaimCodes.All
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

    private static IReadOnlyDictionary<string, BuildReportOutput> CreateReports ()
    {
        return new Dictionary<string, BuildReportOutput>(StringComparer.Ordinal)
        {
            [BuildReportRefs.Build] = new("build.json", BuildDigest),
            [BuildReportRefs.BuildReport] = new("build-report.json", BuildReportDigest),
            [BuildReportRefs.BuildOutputManifest] = new("output-manifest.json", BuildOutputManifestArtifactDigest),
            [BuildReportRefs.BuildLog] = new("build.log", BuildLogDigest),
        };
    }

    private static BuildGenerationsOutput CreateGenerations ()
    {
        return new BuildGenerationsOutput(
            Before: new BuildGenerationSnapshotOutput("compile-before", "domain-before", "asset-before"),
            After: new BuildGenerationSnapshotOutput("compile-after", "domain-after", "asset-after"),
            ValidFor: new BuildGenerationSnapshotOutput("compile-after", "domain-after", "asset-after"));
    }

    private static ProjectIdentityInfo CreateProject ()
    {
        return new ProjectIdentityInfo(
            ProjectPath: "/workspace/UnityProject",
            ProjectFingerprint: "project-fingerprint",
            UnityVersion: "6000.1.4f1");
    }

    private static IpcBuildDirtyState CreateDirtyState ()
    {
        return new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
    }

    private static AssuranceSemanticInvariantValidator CreateBuildValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalog([new BuildCodeCatalogContributor()]),
            [new BuildAssuranceSemanticInvariantRule()]);
    }

    private static JsonElement CreateMutatedSuccessPayload (string caseName)
    {
        var payloadNode = JsonNode.Parse(ReadGoldenPayload("success.json").GetRawText())!.AsObject();
        switch (caseName)
        {
            case "missing-report-ref":
                payloadNode["reports"]!.AsObject().Remove(BuildReportRefs.BuildReport);
                break;
            case "digest-only-entry":
                payloadNode["reports"]![BuildReportRefs.BuildLog]!.AsObject().Remove("path");
                break;
            case "invalid-digest":
                payloadNode["reports"]![BuildReportRefs.BuildLog]!["digest"] = "sha256:dddd";
                break;
            case "manifest-ref-mismatch":
                payloadNode["build"]!["output"]!["manifestRef"] = BuildReportRefs.Build;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown invalid build invariant case.");
        }

        using var document = JsonDocument.Parse(payloadNode.ToJsonString());
        return document.RootElement.Clone();
    }

    private static string GetClaimStatus (
        JsonElement payload,
        string claimId)
    {
        foreach (var claim in payload.GetProperty("claims").EnumerateArray())
        {
            if (string.Equals(claim.GetProperty("id").GetString(), claimId, StringComparison.Ordinal))
            {
                return claim.GetProperty("status").GetString()!;
            }
        }

        throw new InvalidOperationException($"Claim was not found: {claimId}");
    }

    private static JsonDocument ReadGoldenDocument (string fileName)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot,
            CliOutputGoldenFiles.GetPath(BuildRunGoldenDirectory, fileName))));
    }

    private static JsonElement ReadGoldenPayload (string fileName)
    {
        using var document = ReadGoldenDocument(fileName);
        return document.RootElement.GetProperty("payload").Clone();
    }

    private static JsonDocument ReadBuildArtifactFixture (
        string fixtureName,
        string fileName)
    {
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(
            RepositoryRoot,
            BuildArtifactFixtureRoot,
            fixtureName,
            fileName)));
    }

    private static BuildOutputManifestContentJsonContract ReadOutputManifestContent (JsonElement root)
    {
        var targetElement = root.GetProperty("target");
        var entryElements = root.GetProperty("entries");
        var entries = new List<BuildOutputManifestEntryJsonContract>(entryElements.GetArrayLength());
        foreach (var entry in entryElements.EnumerateArray())
        {
            entries.Add(new BuildOutputManifestEntryJsonContract(
                entry.GetProperty("id").GetString()!,
                entry.GetProperty("kind").GetString()!,
                entry.GetProperty("sourcePath").GetString()!));
        }

        var fileElements = root.GetProperty("files");
        var files = new List<BuildOutputManifestFileJsonContract>(fileElements.GetArrayLength());
        foreach (var file in fileElements.EnumerateArray())
        {
            files.Add(new BuildOutputManifestFileJsonContract(
                file.GetProperty("entryId").GetString()!,
                file.GetProperty("logicalPath").GetString()!,
                file.GetProperty("sourcePath").GetString()!,
                file.GetProperty("artifactPath").GetString()!,
                file.GetProperty("sizeBytes").GetInt64(),
                file.GetProperty("sha256").GetString()!));
        }

        return new BuildOutputManifestContentJsonContract(
            root.GetProperty("schemaVersion").GetInt32(),
            new BuildOutputManifestTargetJsonContract(
                targetElement.GetProperty("stableName").GetString()!,
                targetElement.GetProperty("unityBuildTarget").GetString()!),
            entries,
            root.GetProperty("entryCount").GetInt32(),
            root.GetProperty("fileCount").GetInt32(),
            root.GetProperty("totalBytes").GetInt64(),
            files);
    }

    private static void AssertEquivalentJson (
        JsonElement expected,
        JsonElement actual)
    {
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private static bool IsAbsoluteLikePath (string path)
    {
        return Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.StartsWith("\\", StringComparison.Ordinal)
            || (path.Length >= 3 && IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'));
    }

    private static bool IsAsciiLetter (char value)
    {
        return value is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');
    }

    private static void AssertNoRemovedPathSegment (
        JsonElement element,
        IReadOnlyCollection<string> removedNames)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var removedName in removedNames)
                {
                    Assert.DoesNotContain(removedName, segments);
                }
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                AssertNoRemovedPathSegment(property.Value, removedNames);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoRemovedPathSegment(item, removedNames);
            }
        }
    }

    private static string FindRepositoryRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Ucli.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test base directory.");
    }

    private static readonly string[] BuildPipelineEffectValues =
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
}
