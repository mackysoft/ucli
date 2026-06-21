using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Catalog;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Execution;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Features.Assurance.Build.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Execution.Progress;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceTests
{
    private const string RunId = "build-run-1";
    private const string ProjectFingerprint = "project-fingerprint";
    private const string BuildMetadataDigest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string BuildReportArtifactDigest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string BuildOutputManifestArtifactDigest = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string BuildLogArtifactDigest = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string OutputManifestDigest = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";

    private const string ProfileJson = """
        {
          "schemaVersion": 1,
          "inputs": {
            "kind": "explicit",
            "buildTarget": "standaloneLinux64",
            "scenes": {
              "source": "explicit",
              "paths": [
                "Assets/Scenes/Main.unity"
              ]
            },
            "options": {
              "development": true
            }
          },
          "runner": {
            "kind": "buildPipeline"
          },
          "policy": {
            "runtime": {
              "allowedExecutionModes": [
                "daemon",
                "oneshot"
              ],
              "allowedEditorModes": [
                "batchmode",
                "gui"
              ]
            },
            "projectMutationMode": "forbid"
          }
        }
        """;

    private const string UnityBuildProfileJson = """
        {
          "schemaVersion": 1,
          "inputs": {
            "kind": "unityBuildProfile",
            "path": "Assets/BuildProfiles/Linux.asset"
          },
          "runner": {
            "kind": "buildPipeline"
          },
          "policy": {
            "runtime": {
              "allowedExecutionModes": [
                "daemon",
                "oneshot"
              ],
              "allowedEditorModes": [
                "batchmode",
                "gui"
              ]
            },
            "projectMutationMode": "forbid"
          }
        }
        """;

    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithSucceededBuildReport_ReturnsArtifactBackedPayload ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var progressSink = new CollectingProgressSink();
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        if (!result.IsSuccess)
        {
            Assert.Fail(string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        }
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), output.Verdict);
        Assert.Equal(RunId, output.Build.RunId);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded), output.Build.Summary.Result);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport), output.Build.RunnerResult.Source);
        Assert.Equal(output.Build.RunnerResult.Status, output.Build.Summary.Result);
        Assert.Equal(BuildReportRefs.BuildReport, output.Build.Summary.ReportRef);
        Assert.Equal(BuildReportRefs.BuildLog, output.Build.Logs.ReportRef);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed), output.Build.Logs.CompletionReason);
        Assert.Equal("asset-before", output.Build.Generations.Before.AssetRefreshGeneration);
        Assert.Equal("asset-after", output.Build.Generations.After.AssetRefreshGeneration);
        Assert.Equal("asset-after", output.Build.Generations.ValidFor.AssetRefreshGeneration);
        var expectedProfileDigest = BuildProfileResolver.ResolveJson(ProfileJson).Profile!.Digest;
        Assert.Equal(expectedProfileDigest, output.Build.Profile.Digest);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit), output.Build.Inputs.InputKind);
        Assert.Equal("standaloneLinux64", output.Build.Inputs.Target.StableName);
        Assert.Equal("StandaloneLinux64", output.Build.Inputs.Target.UnityBuildTarget);
        Assert.Equal("explicit", output.Build.Inputs.Scenes.Source);
        Assert.Equal(["Assets/Scenes/Main.unity"], output.Build.Inputs.Scenes.Paths);
        Assert.True(output.Build.Inputs.Options.Development);
        Assert.Null(output.Build.Inputs.UnityBuildProfile);
        Assert.Equal(BuildReportRefs.BuildOutputManifest, output.Build.Output.ManifestRef);
        Assert.Equal(OutputManifestDigest, output.Build.Output.ManifestDigest);
        Assert.Equal(1, output.Build.Output.EntryCount);
        Assert.Equal(1, output.Build.Output.FileCount);
        Assert.Equal(
            [BuildReportRefs.Build, BuildReportRefs.BuildLog, BuildReportRefs.BuildOutputManifest, BuildReportRefs.BuildReport],
            output.Reports.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(BuildMetadataDigest, output.Reports[BuildReportRefs.Build].Digest);
        Assert.Equal(BuildReportArtifactDigest, output.Reports[BuildReportRefs.BuildReport].Digest);
        Assert.Equal(BuildOutputManifestArtifactDigest, output.Reports[BuildReportRefs.BuildOutputManifest].Digest);
        Assert.Equal(BuildLogArtifactDigest, output.Reports[BuildReportRefs.BuildLog].Digest);
        Assert.Equal("build.json", output.Reports[BuildReportRefs.Build].Path);
        Assert.Equal("build-report.json", output.Reports[BuildReportRefs.BuildReport].Path);
        Assert.Equal("output-manifest.json", output.Reports[BuildReportRefs.BuildOutputManifest].Path);
        Assert.Equal("build.log", output.Reports[BuildReportRefs.BuildLog].Path);
        Assert.True(output.Reports.ContainsKey(output.Build.Output.ManifestRef));
        AssertEvidenceRefsResolveToReports(output);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodResolved.Value);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodInvoked.Value);
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildExecuteMethodCompleted.Value);
        Assert.All(output.Claims, claim => Assert.True(claim.Required));
        var verifier = Assert.Single(output.Verifiers);
        Assert.Equal("build", verifier.Id);
        Assert.Equal(output.Claims.Where(static claim => claim.Required).Select(static claim => claim.Id).ToArray(), verifier.PrimaryClaims);
        Assert.Equal(BuildPipelineEffectValues, verifier.Effects);
        var preparedPaths = artifactStore.PreparedPaths;
        Assert.NotNull(preparedPaths);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            artifactStore.WrittenMetadata!.Summary.GetProperty("result").GetString());
        Assert.Equal(
            output.Build.RunnerResult.Source,
            artifactStore.WrittenMetadata.RunnerResult.GetProperty("source").GetString());
        Assert.Equal(
            output.Build.RunnerResult.Status,
            artifactStore.WrittenMetadata.RunnerResult.GetProperty("status").GetString());
        Assert.Equal(output.Build.Profile.Path, artifactStore.WrittenMetadata.Profile.GetProperty("path").GetString());
        Assert.Equal(expectedProfileDigest, artifactStore.WrittenMetadata.Profile.GetProperty("digest").GetString());
        Assert.Equal(output.Build.Inputs.InputKind, artifactStore.WrittenMetadata.Inputs.GetProperty("inputKind").GetString());
        Assert.Equal(
            output.Build.Inputs.Target.StableName,
            artifactStore.WrittenMetadata.Inputs.GetProperty("target").GetProperty("stableName").GetString());
        Assert.Equal(
            output.Build.Inputs.Target.UnityBuildTarget,
            artifactStore.WrittenMetadata.Inputs.GetProperty("target").GetProperty("unityBuildTarget").GetString());
        Assert.Equal(
            output.Build.Inputs.Scenes.Source,
            artifactStore.WrittenMetadata.Inputs.GetProperty("scenes").GetProperty("source").GetString());
        Assert.Equal(
            output.Build.Inputs.Options.Development,
            artifactStore.WrittenMetadata.Inputs.GetProperty("options").GetProperty("development").GetBoolean());
        Assert.False(artifactStore.WrittenMetadata.Inputs.TryGetProperty("unityBuildProfile", out _));
        Assert.Equal("buildPipeline", artifactStore.WrittenMetadata.Runner.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, artifactStore.WrittenMetadata.Runner.GetProperty("method").ValueKind);
        Assert.Equal("{}", artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("arguments").GetRawText());
        var runnerEnvironment = artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment");
        Assert.Equal(0, runnerEnvironment.GetProperty("variables").GetArrayLength());
        Assert.Equal(0, runnerEnvironment.GetProperty("secrets").GetArrayLength());
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File), artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("shape").GetString());
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(preparedPaths.RunnerOutputDirectory),
            artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());
        Assert.Equal(output.Build.Summary.ReportRef, artifactStore.WrittenMetadata.Summary.GetProperty("reportRef").GetString());
        Assert.Equal(output.Build.Logs.ReportRef, artifactStore.WrittenMetadata.Logs.GetProperty("reportRef").GetString());
        Assert.False(artifactStore.WrittenMetadata.ProjectMutation.GetProperty("mutated").GetBoolean());
        Assert.Equal("full", artifactStore.WrittenMetadata.ProjectMutation.GetProperty("coverage").GetString());
        Assert.Equal(output.Build.Generations.Before.CompileGeneration, artifactStore.WrittenMetadata.Generations.GetProperty("before").GetProperty("compileGeneration").GetString());
        Assert.Equal(output.Build.Generations.After.DomainReloadGeneration, artifactStore.WrittenMetadata.Generations.GetProperty("after").GetProperty("domainReloadGeneration").GetString());
        Assert.Equal(output.Build.Generations.ValidFor.AssetRefreshGeneration, artifactStore.WrittenMetadata.Generations.GetProperty("validFor").GetProperty("assetRefreshGeneration").GetString());
        Assert.Equal("ready", artifactStore.WrittenMetadata.Lifecycle.GetProperty("before").GetProperty("lifecycleState").GetString());
        Assert.Equal("ready", artifactStore.WrittenMetadata.Lifecycle.GetProperty("after").GetProperty("lifecycleState").GetString());
        AssertProgressEvents(
            progressSink,
            BuildRunProgressEventNames.Started,
            BuildRunProgressEventNames.ReadinessCompleted,
            BuildRunProgressEventNames.RunnerResolved,
            BuildRunProgressEventNames.RunnerStarted,
            BuildRunProgressEventNames.RunnerCompleted,
            BuildRunProgressEventNames.RunnerResultCompleted,
            BuildRunProgressEventNames.ArtifactsCompleted,
            BuildRunProgressEventNames.Completed);
        var startedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(RunId, startedEntry.RunId);
        Assert.Equal(expectedProfileDigest, startedEntry.ProfileDigest);
        Assert.Equal("started", startedEntry.Phase);
        Assert.Null(startedEntry.RunnerKind);
        Assert.Empty(startedEntry.ReportRefs);

        var runnerCompletedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[4].Payload);
        Assert.Equal("runnerResult", runnerCompletedEntry.Phase);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileRunnerKind.BuildPipeline), runnerCompletedEntry.RunnerKind);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded), runnerCompletedEntry.RunnerStatus);

        var completedEntry = Assert.IsType<BuildProgressEntry>(progressSink.Entries[7].Payload);
        Assert.Equal(RunId, completedEntry.RunId);
        Assert.Equal("completed", completedEntry.Phase);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), completedEntry.Verdict);
        Assert.Equal(
            [
                BuildReportRefs.Build,
                BuildReportRefs.BuildReport,
                BuildReportRefs.BuildOutputManifest,
                BuildReportRefs.BuildLog,
            ],
            completedEntry.ReportRefs);

        var validator = CreateBuildSemanticInvariantValidator();
        var semanticPayload = JsonSerializer.SerializeToElement(output, PayloadSerializerOptions);
        var semanticResult = validator.Validate(semanticPayload);
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));

        var requestPayload = Assert.IsType<UnityRequestPayload.BuildRun>(requestExecutor.CapturedPayload);
        Assert.Equal(RunId, requestPayload.RunId);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit), requestPayload.InputKind);
        Assert.Equal("standaloneLinux64", requestPayload.BuildTarget);
        Assert.Equal("StandaloneLinux64", requestPayload.UnityBuildTarget);
        Assert.Equal("explicit", requestPayload.SceneSource);
        Assert.Equal(["Assets/Scenes/Main.unity"], requestPayload.ScenePaths);
        Assert.True(requestPayload.Development);
        Assert.Equal(preparedPaths.RunnerOutputDirectory, requestPayload.OutputPath);
        Assert.NotNull(requestPayload.OutputLayout);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File), requestPayload.OutputLayout!.Shape);
        Assert.Equal(CreateExpectedPlayerLocationPathName(preparedPaths.RunnerOutputDirectory), requestPayload.OutputLayout.LocationPathName);
        Assert.Equal(preparedPaths.BuildReportJsonPath, requestPayload.BuildReportPath);
        Assert.Equal(preparedPaths.BuildLogPath, requestPayload.BuildLogPath);
        Assert.Equal(["batchmode", "gui"], requestPayload.AllowedEditorModes);
        Assert.Equal("forbid", requestPayload.ProjectMutationMode);
        Assert.Equal("buildPipeline", requestPayload.RunnerKind);
        Assert.Null(requestPayload.ProfilePath);
        Assert.Null(requestPayload.RunnerMethod);
        Assert.NotEqual(preparedPaths.RunnerOutputDirectory, preparedPaths.ArtifactOutputDirectory);
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        var outputSource = Assert.Single(accountingRequest.OutputSources);
        Assert.False(outputSource.IsRunnerOutputRelative);
        Assert.Equal(requestPayload.OutputLayout.LocationPathName, outputSource.Path);
        Assert.Equal("standaloneLinux64", accountingRequest.BuildTarget);
        Assert.Equal("StandaloneLinux64", accountingRequest.UnityBuildTarget);
        Assert.False(accountingRequest.AllowEmptyOutputManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecuteMethodRunner_ResolvesInvocationIntoInternalIpcRequest ()
    {
        const string EnvironmentValue = "release";
        const string SecretValue = "secret-value";
        const string profilePath = "/workspace/build.ucli.json";
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: """
                      "run": "${ucli.build.runId}",
                      "output": "${ucli.build.outputDir}",
                      "profile": "${ucli.build.profilePath}",
                      "digest": "${ucli.build.profileDigest}",
                      "project": "${project.path}",
                      "fingerprint": "${project.fingerprint}",
                      "target": "${build.target}"
                """,
            environmentVariables: """
                    "UCLI_MODE"
                """,
            environment: """
                    "UCLI_SECRET"
                """);
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0,
            runnerResult: CreateExecuteMethodRunnerResult(),
            omitReport: true);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, profilePath)),
            environmentVariableReader: new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["UCLI_MODE"] = EnvironmentValue,
                ["UCLI_SECRET"] = SecretValue,
            }),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);
        var progressSink = new CollectingProgressSink();

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        if (!result.IsSuccess)
        {
            Assert.Fail(string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        }

        var payload = Assert.IsType<UnityRequestPayload.BuildRun>(requestExecutor.CapturedPayload);
        var outputDirectory = artifactStore.PreparedPaths!.RunnerOutputDirectory;
        var profileDigest = BuildProfileResolver.ResolveJson(profileJson).Profile!.Digest;
        Assert.Equal("executeMethod", payload.RunnerKind);
        Assert.Null(payload.OutputLayout);
        Assert.Equal(profilePath, payload.ProfilePath);
        Assert.Equal(profileDigest, payload.ProfileDigest);
        Assert.Equal("Build.Entry.Run", payload.RunnerMethod);
        Assert.Equal(RunId, payload.RunnerArguments["run"]);
        Assert.Equal(outputDirectory, payload.RunnerArguments["output"]);
        Assert.Equal(profilePath, payload.RunnerArguments["profile"]);
        Assert.Equal(profileDigest, payload.RunnerArguments["digest"]);
        Assert.Equal("/workspace/UnityProject", payload.RunnerArguments["project"]);
        Assert.Equal(ProjectFingerprint, payload.RunnerArguments["fingerprint"]);
        Assert.Equal("standaloneLinux64", payload.RunnerArguments["target"]);
        Assert.Equal(["UCLI_MODE"], payload.RunnerEnvironmentVariables);
        Assert.Equal(["UCLI_SECRET"], payload.RunnerEnvironmentSecrets);
        Assert.Equal(EnvironmentValue, payload.RunnerEnvironmentVariableValues["UCLI_MODE"]);
        Assert.Equal(SecretValue, payload.RunnerEnvironmentSecretValues["UCLI_SECRET"]);
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        Assert.Null(accountingRequest.BuildReport);
        var outputSource = Assert.Single(accountingRequest.OutputSources);
        Assert.True(outputSource.IsRunnerOutputRelative);
        Assert.Equal("player.txt", outputSource.Path);
        Assert.False(accountingRequest.AllowEmptyOutputManifest);

        var output = result.Output!;
        Assert.Equal("executeMethod", output.Build.Runner.Kind);
        Assert.Equal("Build.Entry.Run", output.Build.Runner.Method);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult), output.Build.RunnerResult.Source);
        Assert.Equal(output.Build.RunnerResult.Status, output.Build.Summary.Result);
        Assert.Equal(["UCLI_MODE"], output.Build.Runner.Invocation.Environment.Variables);
        Assert.Equal(["UCLI_SECRET"], output.Build.Runner.Invocation.Environment.Secrets);
        Assert.Null(output.Build.Summary.ReportRef);
        Assert.False(output.Reports.ContainsKey(BuildReportRefs.BuildReport));
        Assert.DoesNotContain(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildReportAccounted.Value);
        Assert.DoesNotContain(EnvironmentValue, JsonSerializer.Serialize(output, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(output, PayloadSerializerOptions));
        var semanticResult = CreateBuildSemanticInvariantValidator().Validate(JsonSerializer.SerializeToElement(output, PayloadSerializerOptions));
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.DoesNotContain(EnvironmentValue, JsonSerializer.Serialize(artifactStore.WrittenMetadata!, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(artifactStore.WrittenMetadata!, PayloadSerializerOptions));
        Assert.DoesNotContain(EnvironmentValue, JsonSerializer.Serialize(progressSink.Entries, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(progressSink.Entries, PayloadSerializerOptions));
        AssertProgressEvents(
            progressSink,
            BuildRunProgressEventNames.Started,
            BuildRunProgressEventNames.ReadinessCompleted,
            BuildRunProgressEventNames.RunnerResolved,
            BuildRunProgressEventNames.RunnerStarted,
            BuildRunProgressEventNames.RunnerCompleted,
            BuildRunProgressEventNames.RunnerResultCompleted,
            BuildRunProgressEventNames.ArtifactsCompleted,
            BuildRunProgressEventNames.Completed);
        var executeMethodRunnerResolved = Assert.IsType<BuildProgressEntry>(progressSink.Entries[2].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileRunnerKind.ExecuteMethod), executeMethodRunnerResolved.RunnerKind);
        var executeMethodRunnerCompleted = Assert.IsType<BuildProgressEntry>(progressSink.Entries[4].Payload);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileRunnerKind.ExecuteMethod), executeMethodRunnerCompleted.RunnerKind);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded), executeMethodRunnerCompleted.RunnerStatus);
        Assert.Equal("executeMethod", artifactStore.WrittenMetadata!.Runner.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").ValueKind);
        Assert.Equal(output.Build.RunnerResult.Source, artifactStore.WrittenMetadata.RunnerResult.GetProperty("source").GetString());
        Assert.Equal(output.Build.RunnerResult.Status, artifactStore.WrittenMetadata.RunnerResult.GetProperty("status").GetString());
        Assert.Equal(
            ["UCLI_MODE"],
            artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment").GetProperty("variables").EnumerateArray().Select(static item => item.GetString()!).ToArray());
        Assert.Equal(
            ["UCLI_SECRET"],
            artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment").GetProperty("secrets").EnumerateArray().Select(static item => item.GetString()!).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidUnityProgressFrame_ReturnsRunnerInvocationFailedAndDiagnostic ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var invalidProgressFrame = new UnityRequestProgressFrame(
            BuildRunProgressEventNames.ReadinessCompleted,
            IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
                RunId: RunId,
                ProfileDigest: new string('a', 64),
                Phase: "invalidPhase",
                RunnerKind: null,
                RunnerStatus: null,
                Verdict: null,
                ReportRefs: [],
                ErrorCode: null)));
        var requestExecutor = new StubUnityRequestExecutor(
            _ =>
                CreateBuildResponseResult(
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                    errorCount: 0),
            [invalidProgressFrame]);
        var progressSink = new CollectingProgressSink();
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput(), progressSink);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerInvocationFailed, error.Code);
        AssertProgressEvents(
            progressSink,
            BuildRunProgressEventNames.Started,
            BuildRunProgressEventNames.Diagnostic);
        var diagnostic = Assert.IsType<BuildDiagnosticEntry>(progressSink.Entries[1].Payload);
        Assert.Equal(RunId, diagnostic.RunId);
        Assert.Equal(BuildErrorCodes.BuildRunnerInvocationFailed.Value, diagnostic.Code);
        Assert.Equal(IpcExecuteDiagnosticSeverityNames.Error, diagnostic.Severity);
        Assert.Equal("runnerInvocation", diagnostic.Phase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecuteMethodRunnerBuildReportPath_AccountsOptionalBuildReport ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("reports/build-report.json")),
                omitReport: true),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        Assert.NotNull(accountingRequest.BuildReport);
        Assert.Equal("reports/build-report.json", accountingRequest.BuildReport.RunnerOutputRelativePath);
        var output = result.Output!;
        Assert.Equal(BuildReportRefs.BuildReport, output.Build.Summary.ReportRef);
        Assert.True(output.Reports.ContainsKey(BuildReportRefs.BuildReport));
        var reportClaim = Assert.Single(output.Claims, static claim => claim.Id == BuildClaimCodes.UnityBuildReportAccounted.Value);
        Assert.False(reportClaim.Required);
        var semanticResult = CreateBuildSemanticInvariantValidator().Validate(JsonSerializer.SerializeToElement(output, PayloadSerializerOptions));
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMissingExecuteMethodEnvironment_ReturnsBuildRunnerEnvironmentMissingBeforeDispatch ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: """
                      "output": "${ucli.build.outputDir}"
                """,
            environment: """
                    "UCLI_SECRET"
                """);
        using var tempDirectory = TemporaryDirectory.Create();
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            environmentVariableReader: new StubEnvironmentVariableReader(),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerEnvironmentMissing, error.Code);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("\"bad\": \"${ucli.build.unknown}\"")]
    [InlineData("\"bad\": \"${ucli.build.outputDir\"")]
    public async Task Execute_WithInvalidExecuteMethodArgumentVariable_ReturnsBuildProfileInvalidBeforeDispatch (
        string arguments)
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: arguments,
            environment: string.Empty);
        using var tempDirectory = TemporaryDirectory.Create();
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, error.Code);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithEmptyExecuteMethodProfilePathVariable_ReturnsBuildProfileInvalidBeforeDispatch ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: """
                      "profile": "${ucli.build.profilePath}"
                """,
            environment: string.Empty);
        using var tempDirectory = TemporaryDirectory.Create();
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(new BuildProfileFileReadResult(profileJson, string.Empty, null)),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProfileInvalid, error.Code);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithVariableLikeExecuteMethodEnvironmentName_DoesNotSubstituteBeforeLookup ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: """
                    "${UCLI_SECRET}"
                """);
        using var tempDirectory = TemporaryDirectory.Create();
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            environmentVariableReader: new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["UCLI_SECRET"] = "secret-value",
            }),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerEnvironmentMissing, error.Code);
        Assert.Equal(0, requestExecutor.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecuteMethodRunnerResultStatusMismatch_ReturnsCommandFailure ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: CreateExecuteMethodRunnerResult(
                    status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
                    outputs: [],
                    errorCount: 1,
                    warningCount: 0),
                omitReport: true),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMissingExecuteMethodRunnerResult_ReturnsBuildRunnerResultMissing ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                omitReport: true),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultMissing, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecuteMethodSucceededAndEmptyOutputs_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(outputs: []));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecuteMethodRunnerResultOutputsNull_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(
            static payload => payload["runnerResult"]!.AsObject()["outputs"] = null);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExecuteMethodRunnerResultOutputsMissing_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(
            static payload => payload["runnerResult"]!.AsObject().Remove("outputs"));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(GetInvalidExecuteMethodRunnerResultShapeCases))]
    public async Task Execute_WithInvalidExecuteMethodRunnerResultPayloadShape_ReturnsBuildRunnerResultInvalid (
        Action<JsonObject> mutatePayload)
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(mutatePayload);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(GetDuplicateExecuteMethodRunnerResultPropertyCases))]
    public async Task Execute_WithDuplicateExecuteMethodRunnerResultProperty_ReturnsBuildRunnerResultInvalid (
        Func<string, string> mutatePayloadJson)
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultRawPayloadAsync(mutatePayloadJson);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
    [InlineData(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
    public async Task Execute_WithUnsuccessfulExecuteMethodAndEmptyOutputs_AllowsEmptyOutputManifest (
        IpcBuildReportResult runnerStatus,
        IpcBuildLogCompletionReason completionReason)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(
                status: ContractLiteralCodec.ToValue(runnerStatus),
                outputs: [],
                errorCount: runnerStatus == IpcBuildReportResult.Failed ? 1 : 0,
                warningCount: 0),
            ContractLiteralCodec.ToValue(completionReason),
            artifactStore);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        Assert.Empty(accountingRequest.OutputSources);
        Assert.True(accountingRequest.AllowEmptyOutputManifest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidExecuteMethodOutputPath_ReturnsBuildOutputPathInvalid ()
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(outputs: ["../player"]),
            writeRunnerResultOutputs: false);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildOutputPathInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMissingDeclaredExecuteMethodOutput_ReturnsBuildRunnerResultInvalid ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.Path,
            accountArtifactsOverride: static (_, _) => ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Failure(
                ExecutionError.InvalidArgument(
                    "Build runner result declared an output source that does not exist.",
                    BuildErrorCodes.BuildRunnerResultInvalid))));
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(outputs: ["missing-player"]),
            "completed",
            artifactStore);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidExecuteMethodBuildReportPath_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("../build-report.json")),
            writeRunnerBuildReportSource: false);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMissingExecuteMethodBuildReportSource_ReturnsBuildReportMissing ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.Path,
            accountArtifactsOverride: static (_, _) => ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Failure(
                ExecutionError.InternalError(
                    "BuildReport source file was not found.",
                    BuildErrorCodes.BuildReportMissing))));
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("reports/build-report.json")),
            "completed",
            artifactStore);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidExecuteMethodBuildReportSource_ReturnsBuildReportMissing ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.Path,
            accountArtifactsOverride: static (_, _) => ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Failure(
                ExecutionError.InternalError(
                    "BuildReport source is not a valid uCLI BuildReport JSON artifact.",
                    BuildErrorCodes.BuildReportMissing))));
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(buildReport: new IpcBuildRunnerResultBuildReport("reports/build-report.json")),
            "completed",
            artifactStore);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildReportMissing, error.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unsupported", 2500, 0, 1)]
    [InlineData("succeeded", -1, 0, 1)]
    public async Task Execute_WithInvalidExecuteMethodRunnerResultShape_ReturnsBuildRunnerResultInvalid (
        string status,
        long durationMilliseconds,
        int errorCount,
        int warningCount)
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(
                status: status,
                durationMilliseconds: durationMilliseconds,
                errorCount: errorCount,
                warningCount: warningCount));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidExecuteMethodDiagnostics_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(diagnostics:
            [
                new IpcBuildRunnerDiagnostic(
                    Severity: "verbose",
                    Code: "diagnostic",
                    Message: "Unsupported severity"),
            ]));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithBuildPipelineRunnerResultSourceMismatch_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                    Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    DurationMilliseconds: 2500,
                    ErrorCount: 0,
                    WarningCount: 0,
                    Diagnostics: [])),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithBuildPipelineRunnerResultSummaryMismatch_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.BuildPipelineBuildReport),
                    Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    DurationMilliseconds: 9999,
                    ErrorCount: 0,
                    WarningCount: 0,
                    Diagnostics: [])),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnknownBuildPipelineReportResult_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Unknown),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                errorCount: 0),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnityBuildProfileInput_DelegatesInputResolutionToUnityAndProjectsResponse ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        const string unityBuildProfilePath = "Assets/BuildProfiles/Linux.asset";
        var unityBuildProfileDigest = new string('f', 64);
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "StandaloneLinux64",
                buildOptions: "None",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(unityBuildProfilePath, unityBuildProfileDigest));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile), output.Build.Inputs.InputKind);
        Assert.Equal("standaloneLinux64", output.Build.Inputs.Target.StableName);
        Assert.Equal("StandaloneLinux64", output.Build.Inputs.Target.UnityBuildTarget);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile), output.Build.Inputs.Scenes.Source);
        Assert.Equal(["Assets/Scenes/ProfileMain.unity"], output.Build.Inputs.Scenes.Paths);
        Assert.False(output.Build.Inputs.Options.Development);
        var outputUnityBuildProfile = Assert.IsType<BuildUnityBuildProfileOutput>(output.Build.Inputs.UnityBuildProfile);
        Assert.Equal(unityBuildProfilePath, outputUnityBuildProfile.Path);
        Assert.Equal(unityBuildProfileDigest, outputUnityBuildProfile.Digest);

        var requestPayload = Assert.IsType<UnityRequestPayload.BuildRun>(requestExecutor.CapturedPayload);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile), requestPayload.InputKind);
        Assert.Null(requestPayload.BuildTarget);
        Assert.Null(requestPayload.UnityBuildTarget);
        Assert.Null(requestPayload.SceneSource);
        Assert.Empty(requestPayload.ScenePaths);
        Assert.False(requestPayload.Development);
        Assert.Null(requestPayload.OutputLayout);
        Assert.Equal(unityBuildProfilePath, requestPayload.UnityBuildProfile!.Path);
        Assert.Null(requestPayload.UnityBuildProfile.Digest);
        Assert.Null(requestPayload.UnityBuildProfile.ApplyAudit);
        Assert.Null(artifactStore.PreparedOutputLayout);
        Assert.Equal("standaloneLinux64", artifactStore.AccountingRequest!.BuildTarget);

        Assert.NotNull(artifactStore.WrittenMetadata);
        var metadataInput = artifactStore.WrittenMetadata!.Inputs;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile), metadataInput.GetProperty("inputKind").GetString());
        var metadataUnityBuildProfile = metadataInput.GetProperty("unityBuildProfile");
        Assert.Equal(unityBuildProfilePath, metadataUnityBuildProfile.GetProperty("path").GetString());
        Assert.Equal(unityBuildProfileDigest, metadataUnityBuildProfile.GetProperty("digest").GetString());
        var metadataApplyAudit = metadataUnityBuildProfile.GetProperty("applyAudit");
        Assert.True(metadataApplyAudit.GetProperty("applied").GetBoolean());
        Assert.Equal("ready", metadataApplyAudit.GetProperty("lifecycleBefore").GetProperty("lifecycleState").GetString());
        Assert.Equal("asset-profile-after", metadataApplyAudit.GetProperty("generationsAfter").GetProperty("assetRefreshGeneration").GetString());
        Assert.False(metadataApplyAudit.GetProperty("dirtyStateAfter").GetProperty("dirty").GetBoolean());
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(artifactStore.PreparedPaths!.RunnerOutputDirectory),
            artifactStore.WrittenMetadata.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());

        var validator = CreateBuildSemanticInvariantValidator();
        var semanticPayload = JsonSerializer.SerializeToElement(output, PayloadSerializerOptions);
        var semanticResult = validator.Validate(semanticPayload);
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnityBuildProfileAndroidAppBundleResponse_AcceptsResolvedAabOutputLayout ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        const string unityBuildProfilePath = "Assets/BuildProfiles/Linux.asset";
        var unityBuildProfileDigest = new string('f', 64);
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath, "Player.aab"));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "android",
                unityBuildTarget: "Android",
                buildOptions: "None",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(unityBuildProfilePath, unityBuildProfileDigest));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.Equal("android", artifactStore.AccountingRequest!.BuildTarget);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal(
            CreateExpectedPlayerLocationPathName(artifactStore.PreparedPaths!.RunnerOutputDirectory, "Player.aab"),
            artifactStore.WrittenMetadata!.Runner.GetProperty("outputLayout").GetProperty("locationPathName").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExplicitResponseContainingUnityBuildProfile_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                reportOutputPath: buildRunPayload.OutputLayout!.LocationPathName,
                outputLayout: buildRunPayload.OutputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset", new string('f', 64)));
        });
        var service = CreateService(
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnityBuildProfileResponseMismatchedUnityBuildTarget_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "Android",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput("Assets/BuildProfiles/Linux.asset", new string('f', 64)));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnityBuildProfileResponseMissingApplyAuditDirtyState_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "StandaloneLinux64",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(
                    "Assets/BuildProfiles/Linux.asset",
                    new string('f', 64),
                    static audit => audit with
                    {
                        DirtyStateAfter = null!,
                    }));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnityBuildProfileResponseMismatchedApplyAuditGeneration_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            var outputLayout = new IpcBuildOutputLayout(
                Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                LocationPathName: CreateExpectedPlayerLocationPathName(buildRunPayload.OutputPath));
            return CreateBuildResponseResult(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                inputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.UnityBuildProfile),
                sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.UnityBuildProfile),
                scenes: ["Assets/Scenes/ProfileMain.unity"],
                buildTarget: "standaloneLinux64",
                unityBuildTarget: "StandaloneLinux64",
                reportOutputPath: outputLayout.LocationPathName,
                outputLayout: outputLayout,
                unityBuildProfile: CreateUnityBuildProfileInput(
                    "Assets/BuildProfiles/Linux.asset",
                    new string('f', 64),
                    static audit => audit with
                    {
                        GenerationsAfter = audit.GenerationsAfter with
                        {
                            CompileGeneration = "different-compile-generation",
                        },
                    }));
        });
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(UnityBuildProfileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithoutTimeoutOption_UsesBuildRunConfigOverride ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.BuildRun.Name] = 432100,
        };
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            projectContextResolver: new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext(config))),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path),
            timeProvider: new ManualTimeProvider());

        var result = await service.ExecuteAsync(CreateInput(timeoutMilliseconds: null));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.Equal(TimeSpan.FromMilliseconds(432100), requestExecutor.CapturedTimeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithCompositeUnityDevelopmentBuildOptions_ReturnsSuccess ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                buildOptions: "ForceOptimizeScriptCompilation, Il2CPP, Development"),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithEditorBuildSettings_UsesUnityResolvedScenesInPayload ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        const string profileJson = """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "editorBuildSettings"
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """;
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0,
            sceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.EditorBuildSettings),
            scenes: ["Assets/Scenes/FromSettings.unity"]);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.Equal("editorBuildSettings", result.Output!.Build.Inputs.Scenes.Source);
        Assert.Equal(["Assets/Scenes/FromSettings.unity"], result.Output.Build.Inputs.Scenes.Paths);
        var metadataScenePaths = artifactStore.WrittenMetadata!.Inputs
            .GetProperty("scenes")
            .GetProperty("paths")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();
        Assert.Equal(["Assets/Scenes/FromSettings.unity"], metadataScenePaths);
        var payload = Assert.IsType<UnityRequestPayload.BuildRun>(requestExecutor.CapturedPayload);
        Assert.Equal("editorBuildSettings", payload.SceneSource);
        Assert.Empty(payload.ScenePaths);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMissingLifecycleGeneration_ReturnsIncompleteGenerationClaim ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                lifecycleAfter: CreateLifecycleSnapshot("after", canAcceptExecutionRequests: true, omitAssetRefreshGeneration: true)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Incomplete), result.Output!.Verdict);
        var claim = FindClaim(result.Output, BuildClaimCodes.UnityBuildValidForGeneration);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Indeterminate), claim.Status);
        Assert.Equal("unknown", result.Output.Build.Generations.ValidFor.AssetRefreshGeneration);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
    [InlineData(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
    public async Task Execute_WithUnsuccessfulBuildReport_ReturnsCompletedFailVerdict (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason)
    {
        var reportResultLiteral = ContractLiteralCodec.ToValue(reportResult);
        var completionReasonLiteral = ContractLiteralCodec.ToValue(completionReason);
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                reportResultLiteral,
                completionReasonLiteral,
                errorCount: 1),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        var output = result.Output!;
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Fail), output.Verdict);
        Assert.Equal(reportResultLiteral, output.Build.Summary.Result);
        Assert.Equal(completionReasonLiteral, output.Build.Logs.CompletionReason);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Passed), FindClaim(output, BuildClaimCodes.UnityBuildCompleted).Status);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Failed), FindClaim(output, BuildClaimCodes.UnityBuildSucceeded).Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAutoResolvedDaemonRejectedByRuntimePolicy_DoesNotBypassDaemon ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["oneshot"], ["batchmode", "gui"], "forbid"),
                "/workspace/build.ucli.json")),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: true,
                UnityExecutionTarget.Daemon,
                TimeSpan.FromSeconds(10)))),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRuntimePolicyViolation, error.Code);
        Assert.Null(requestExecutor.CapturedPayload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithOneshotBatchmodeRejectedByRuntimePolicy_ReturnsRuntimePolicyViolation ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["oneshot"], ["gui"], "forbid"),
                "/workspace/build.ucli.json")),
            modeDecisionService: new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Oneshot,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRuntimePolicyViolation, error.Code);
        Assert.Null(requestExecutor.CapturedPayload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDirtySceneResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
        var input = CreateInputProbe();
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", ProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
            DirtyState: dirtyState,
            Input: input);
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStatePresent, "Dirty scene state is present.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent, error.Code);
        Assert.NotNull(result.DirtyState);
        Assert.True(result.DirtyState!.Checked);
        Assert.True(result.DirtyState.Dirty);
        var item = Assert.Single(result.DirtyState.Items);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene), item.Kind);
        Assert.Equal("Assets/Scenes/Main.unity", item.Path);
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithDirtyStateIndeterminateResponse_ReturnsCommandFailureWithProbePayload ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: false,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Partial),
            Items: []);
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", ProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
            DirtyState: dirtyState,
            Input: CreateInputProbe());
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildDirtyStateIndeterminate, "Dirty state coverage is partial.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildDirtyStateIndeterminate, error.Code);
        Assert.NotNull(result.DirtyState);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Partial), result.DirtyState!.Coverage);
        Assert.Empty(result.DirtyState.Items);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithNonDirtyFailurePayload_DoesNotReturnDirtyState ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var dirtyState = new IpcBuildDirtyState(
            Checked: true,
            Dirty: true,
            Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
            Items:
            [
                new IpcBuildDirtyStateItem(
                    ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                    "Assets/Scenes/Main.unity"),
            ]);
        var errorPayload = new IpcBuildRunErrorPayload(
            Project: new IpcProjectIdentity("/workspace/UnityProject", ProjectFingerprint, "6000.1.4f1"),
            LifecycleBefore: CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
            DirtyState: dirtyState,
            Input: CreateInputProbe());
        var response = new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(errorPayload),
            [new OperationExecutionError(BuildErrorCodes.BuildArtifactWriteFailed, "Artifact write failed.", null)],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
        var service = CreateService(
            requestExecutor: new StubUnityRequestExecutor(UnityRequestExecutionResult.Success(response)),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildArtifactWriteFailed, error.Code);
        Assert.Null(result.DirtyState);
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenArtifactAccountingTimesOut_ReturnsIpcTimeoutFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.Path,
            accountArtifactsOverride: async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            });
        var service = CreateService(artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput(timeoutMilliseconds: 50));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCallerCancelsArtifactAccounting_PropagatesCancellation ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        using var cancellationTokenSource = new CancellationTokenSource();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.Path,
            accountArtifactsOverride: async (_, cancellationToken) =>
            {
                cancellationTokenSource.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("Unreachable.");
            });
        var service = CreateService(artifactStore: artifactStore);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(CreateInput(), cancellationToken: cancellationTokenSource.Token).AsTask());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unsupported", "Assets/Scenes/Main.unity")]
    [InlineData("explicit", " Assets/Scenes/Main.unity")]
    public async Task Execute_WithInvalidResolvedInputResponse_ReturnsCommandFailure (
        string sceneSource,
        string scenePath)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                sceneSource: sceneSource,
                scenes: [scenePath]),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("otherTarget", "StandaloneLinux64", "Development")]
    [InlineData("standaloneLinux64", "StandaloneOSX", "Development")]
    [InlineData("standaloneLinux64", "StandaloneLinux64", "None")]
    public async Task Execute_WithMismatchedResolvedInputResponse_ReturnsCommandFailure (
        string buildTarget,
        string unityBuildTarget,
        string buildOptions)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                buildTarget: buildTarget,
                unityBuildTarget: unityBuildTarget,
                buildOptions: buildOptions),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMismatchedExplicitScenesResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                scenes: ["Assets/Scenes/Other.unity"]),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMismatchedOutputLayoutResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: new StubUnityRequestExecutor(payload =>
            {
                var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
                var mismatchedLayout = new IpcBuildOutputLayout(
                    Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                    LocationPathName: Path.Combine(tempDirectory.Path, "other-output", "player", "Player"));
                return CreateBuildResponseResult(
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                    errorCount: 0,
                    reportOutputPath: buildRunPayload.OutputLayout!.LocationPathName,
                    outputLayout: mismatchedLayout);
            }),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMismatchedCompletionReasonResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 1),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithUnknownBuildReportResult_ReturnsCommandFailureBeforeArtifactAccounting ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Unknown),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                errorCount: 1),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.AccountingRequest);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
    [InlineData(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
    public async Task Execute_WithUnsuccessfulBuildResponse_AllowsEmptyOutputManifest (
        IpcBuildReportResult reportResult,
        IpcBuildLogCompletionReason completionReason)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(
            tempDirectory.Path,
            (request, _) =>
            {
                Assert.True(request.AllowEmptyOutputManifest);
                return ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Success(new BuildRunArtifactAccountingResult(
                    BuildReport: new BuildArtifactRef(BuildArtifactKind.BuildReport, "build-report.json", BuildReportArtifactDigest),
                    BuildOutputManifest: new BuildArtifactRef(BuildArtifactKind.BuildOutputManifest, "output-manifest.json", BuildOutputManifestArtifactDigest),
                    BuildLog: new BuildArtifactRef(BuildArtifactKind.BuildLog, "build.log", BuildLogArtifactDigest),
                    OutputManifest: new BuildOutputManifestSummary(
                        ManifestDigest: OutputManifestDigest,
                        EntryCount: 0,
                        FileCount: 0,
                        TotalBytes: 0))));
            });
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(reportResult),
                ContractLiteralCodec.ToValue(completionReason),
                errorCount: 1),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Output!.Build.Output.EntryCount);
        Assert.Equal(0, result.Output.Build.Output.FileCount);
        Assert.Equal(0, result.Output.Build.Output.TotalBytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithBuildTargetWithoutDeterministicBuildPipelineOutputLayout_ReturnsBuildInputsInvalid ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        const string profileJson = """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "switch",
                "scenes": {
                  "source": "explicit",
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ]
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """;
        var requestExecutor = CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildInputsInvalid, error.Code);
        Assert.Null(requestExecutor.CapturedPayload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithForbidProjectMutation_ReturnsCommandFailureAfterWritingMetadata ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: true,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProjectMutationForbidden, error.Code);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.True(artifactStore.WrittenMetadata!.ProjectMutation.GetProperty("mutated").GetBoolean());
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithForbidProjectMutationPartialCoverage_ReturnsCommandFailureAfterWritingMetadata ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid),
                    coverage: ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Partial))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildProjectMutationForbidden, error.Code);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.False(artifactStore.WrittenMetadata!.ProjectMutation.GetProperty("mutated").GetBoolean());
        Assert.Equal("partial", artifactStore.WrittenMetadata.ProjectMutation.GetProperty("coverage").GetString());
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAuditProjectMutation_ReturnsNonBlockingResidualRisk ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["daemon", "oneshot"], ["batchmode", "gui"], "audit"),
                "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: true,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Audit))),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), result.Output!.Verdict);
        var risk = Assert.Single(result.Output.ResidualRisks);
        Assert.Equal(BuildRiskCodes.ProjectMutationDetected.Value, risk.Code);
        Assert.False(risk.Blocking);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Passed), FindClaim(result.Output, BuildClaimCodes.UnityBuildProjectMutationAccounted).Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAllowWithAuditFullCoverage_ReturnsSuccessWithoutResidualRisk ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["daemon", "oneshot"], ["batchmode", "gui"], "allowWithAudit"),
                "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: true,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.AllowWithAudit))),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), result.Output!.Verdict);
        Assert.Empty(result.Output.ResidualRisks);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithAllowWithAuditPartialCoverage_ReturnsNonBlockingResidualRisk ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(
                CreateProfileJson(["daemon", "oneshot"], ["batchmode", "gui"], "allowWithAudit"),
                "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.AllowWithAudit),
                    coverage: ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Partial))),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Incomplete), result.Output!.Verdict);
        var risk = Assert.Single(result.Output.ResidualRisks);
        Assert.Equal(BuildRiskCodes.ProjectMutationDetected.Value, risk.Code);
        Assert.False(risk.Blocking);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildClaimStatus.Indeterminate), FindClaim(result.Output, BuildClaimCodes.UnityBuildProjectMutationAccounted).Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithMismatchedProjectMutationModeResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Audit))),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInvalidProjectMutationCoverageResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: CreateProjectMutation(
                    mutated: false,
                    mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid),
                    coverage: "legacy")),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithInconsistentProjectMutationItemsResponse_ReturnsCommandFailure ()
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.Path);
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0,
                projectMutation: new IpcBuildProjectMutationAudit(
                    Mode: ContractLiteralCodec.ToValue(BuildProfileProjectMutationMode.Forbid),
                    Coverage: ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Full),
                    Mutated: false,
                    BeforeDigest: new string('1', 64),
                    AfterDigest: new string('2', 64),
                    Items:
                    [
                        new IpcBuildProjectMutationAuditItem(
                            Path: "Assets/Generated.asset",
                            ChangeKind: ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added),
                            BeforeSha256: null,
                            AfterSha256: new string('2', 64)),
                    ])),
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        Assert.Null(artifactStore.WrittenMetadata);
    }

    private static BuildService CreateService (
        IBuildRunArtifactStore artifactStore,
        IProjectContextResolver? projectContextResolver = null,
        IBuildProfileFileReader? profileFileReader = null,
        IEnvironmentVariableReader? environmentVariableReader = null,
        IUnityExecutionModeDecisionService? modeDecisionService = null,
        IUnityRequestExecutor? requestExecutor = null,
        IUnityStreamingRequestExecutor? streamingRequestExecutor = null,
        IBuildRunIdFactory? runIdFactory = null,
        TimeProvider? timeProvider = null)
    {
        var resolvedRequestExecutor = requestExecutor ?? CreateBuildResponseExecutor(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
            errorCount: 0);
        var resolvedStreamingRequestExecutor = streamingRequestExecutor
            ?? resolvedRequestExecutor as IUnityStreamingRequestExecutor
            ?? throw new InvalidOperationException("BuildService tests require a streaming request executor.");
        return new BuildService(
            projectContextResolver ?? new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            profileFileReader ?? new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(ProfileJson, "/workspace/build.ucli.json")),
            environmentVariableReader ?? new StubEnvironmentVariableReader(),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            resolvedRequestExecutor,
            resolvedStreamingRequestExecutor,
            runIdFactory ?? new StubBuildRunIdFactory(RunId),
            artifactStore,
            timeProvider);
    }

    private static BuildCommandInput CreateInput (
        int? timeoutMilliseconds = 10000)
    {
        return new BuildCommandInput(
            ProfilePath: "/workspace/build.ucli.json",
            ProjectPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: timeoutMilliseconds);
    }

    private static string CreateProfileJson (
        IReadOnlyList<string> allowedExecutionModes,
        IReadOnlyList<string> allowedEditorModes,
        string projectMutationMode)
    {
        var executionModesJson = string.Join(",\n                  ", allowedExecutionModes.Select(static mode => $"\"{mode}\""));
        var editorModesJson = string.Join(",\n                  ", allowedEditorModes.Select(static mode => $"\"{mode}\""));
        return $$"""
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "explicit",
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ]
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                  {{executionModesJson}}
                  ],
                  "allowedEditorModes": [
                  {{editorModesJson}}
                  ]
                },
                "projectMutationMode": "{{projectMutationMode}}"
              }
            }
            """;
    }

    private static string CreateExecuteMethodProfileJson (
        string method,
        string arguments,
        string environment,
        string environmentVariables = "")
    {
        return $$"""
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "explicit",
                  "paths": [
                    "Assets/Scenes/Main.unity"
                  ]
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "executeMethod",
                "method": "{{method}}",
                "invocation": {
                  "arguments": {
                {{arguments}}
                  },
                  "environment": {
                    "variables": [
                {{environmentVariables}}
                    ],
                    "secrets": [
                {{environment}}
                    ]
                  }
                }
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """;
    }

    private static ProjectContext CreateProjectContext (UcliConfig? config = null)
    {
        var unityProject = new ResolvedUnityProjectContext(
            UnityProjectRoot: "/workspace/UnityProject",
            RepositoryRoot: "/workspace",
            ProjectFingerprint: ProjectFingerprint,
            PathSource: UnityProjectPathSource.CommandOption,
            PathSourceLabel: "--projectPath",
            UnityVersion: "6000.1.4f1");
        return new ProjectContext(
            unityProject,
            config ?? UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static StubUnityRequestExecutor CreateBuildResponseExecutor (
        string reportResult,
        string completionReason,
        int errorCount,
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null,
        IpcBuildLifecycleSnapshot? lifecycleBefore = null,
        IpcBuildLifecycleSnapshot? lifecycleAfter = null,
        string? reportOutputPath = null,
        IpcBuildProjectMutationAudit? projectMutation = null,
        IpcBuildRunnerResultArtifact? runnerResult = null,
        bool omitReport = false,
        bool writeRunnerResultOutputs = true,
        bool writeRunnerBuildReportSource = true,
        string? runnerBuildReportSourceJson = null)
    {
        return new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            if (runnerResult != null)
            {
                WriteRunnerResultFiles(
                    buildRunPayload,
                    runnerResult,
                    reportResult,
                    errorCount,
                    writeRunnerResultOutputs,
                    writeRunnerBuildReportSource,
                    runnerBuildReportSourceJson);
            }

            return CreateBuildResponseResult(
                reportResult,
                completionReason,
                errorCount,
                sceneSource: sceneSource,
                scenes: scenes,
                buildTarget: buildTarget,
                unityBuildTarget: unityBuildTarget,
                buildOptions: buildOptions,
                lifecycleBefore: lifecycleBefore,
                lifecycleAfter: lifecycleAfter,
                reportOutputPath: reportOutputPath ?? buildRunPayload.OutputLayout?.LocationPathName ?? buildRunPayload.OutputPath,
                outputLayout: buildRunPayload.OutputLayout,
                useDefaultOutputLayout: buildRunPayload.OutputLayout != null,
                projectMutation: projectMutation,
                runnerResult: runnerResult,
                omitReport: omitReport);
        });
    }

    private static async Task<BuildExecutionResult> ExecuteWithExecuteMethodRunnerResultAsync (
        IpcBuildRunnerResultArtifact runnerResult,
        string completionReason,
        StubBuildRunArtifactStore artifactStore,
        bool writeRunnerResultOutputs = true,
        bool writeRunnerBuildReportSource = true,
        string? runnerBuildReportSourceJson = null)
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                runnerResult.Status,
                completionReason,
                runnerResult.ErrorCount,
                runnerResult: runnerResult,
                omitReport: true,
                writeRunnerResultOutputs: writeRunnerResultOutputs,
                writeRunnerBuildReportSource: writeRunnerBuildReportSource,
                runnerBuildReportSourceJson: runnerBuildReportSourceJson),
            artifactStore: artifactStore);

        return await service.ExecuteAsync(CreateInput());
    }

    private static async Task<BuildExecutionResult> ExecuteWithExecuteMethodRunnerResultAsync (
        IpcBuildRunnerResultArtifact runnerResult,
        string completionReason = "completed",
        bool writeRunnerResultOutputs = true,
        bool writeRunnerBuildReportSource = true,
        string? runnerBuildReportSourceJson = null)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        return await ExecuteWithExecuteMethodRunnerResultAsync(
            runnerResult,
            completionReason,
            new StubBuildRunArtifactStore(tempDirectory.Path),
            writeRunnerResultOutputs,
            writeRunnerBuildReportSource,
            runnerBuildReportSourceJson);
    }

    public static IEnumerable<object[]> GetInvalidExecuteMethodRunnerResultShapeCases ()
    {
        yield return
        [
            (Action<JsonObject>)(static payload => payload["runnerResult"] = new JsonArray()),
        ];
        yield return
        [
            (Action<JsonObject>)(static payload => payload["runnerResult"]!.AsObject()["extra"] = true),
        ];
        yield return
        [
            (Action<JsonObject>)(static payload => payload["runnerResult"]!.AsObject()["outputs"] = "player.txt"),
        ];
        yield return
        [
            (Action<JsonObject>)(static payload => payload["runnerResult"]!.AsObject()["outputs"] = new JsonArray(1)),
        ];
        yield return
        [
            (Action<JsonObject>)(static payload => payload["runnerResult"]!.AsObject()["buildReport"] = new JsonObject
            {
                ["path"] = 1,
            }),
        ];
    }

    public static IEnumerable<object[]> GetDuplicateExecuteMethodRunnerResultPropertyCases ()
    {
        yield return
        [
            (Func<string, string>)(static json => json.Replace(
                "\"outputs\":[]",
                "\"outputs\":[],\"Outputs\":[]",
                StringComparison.Ordinal)),
        ];
        yield return
        [
            (Func<string, string>)(static json => json.Replace(
                "\"diagnostics\":[]",
                "\"diagnostics\":[{\"code\":\"D\",\"Code\":\"E\",\"severity\":\"error\",\"message\":\"m\"}]",
                StringComparison.Ordinal)),
        ];
        yield return
        [
            (Func<string, string>)(static json => json.Replace(
                "\"buildReport\":null",
                "\"buildReport\":{\"path\":\"reports/a.json\",\"Path\":\"reports/b.json\"}",
                StringComparison.Ordinal)),
        ];
    }

    private static Task<BuildExecutionResult> ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync (
        Action<JsonObject> mutatePayload)
    {
        return ExecuteWithMalformedExecuteMethodRunnerResultRawPayloadAsync(payloadJson =>
        {
            var payloadObject = JsonNode.Parse(payloadJson)!.AsObject();
            mutatePayload(payloadObject);
            return payloadObject.ToJsonString();
        });
    }

    private static async Task<BuildExecutionResult> ExecuteWithMalformedExecuteMethodRunnerResultRawPayloadAsync (
        Func<string, string> mutatePayloadJson)
    {
        using var tempDirectory = TemporaryDirectory.Create();
        var runnerResult = CreateExecuteMethodRunnerResult(
            status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
            outputs: [],
            errorCount: 1,
            warningCount: 0);
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: new StubUnityRequestExecutor(payload =>
            {
                var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
                WriteRunnerResultFiles(
                    buildRunPayload,
                    runnerResult,
                    runnerResult.Status,
                    runnerResult.ErrorCount,
                    writeOutputs: true,
                    writeBuildReportSource: true,
                    buildReportSourceJson: null);
                var result = CreateBuildResponseResult(
                    runnerResult.Status,
                    ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                    runnerResult.ErrorCount,
                    reportOutputPath: buildRunPayload.OutputPath,
                    runnerResult: runnerResult,
                    omitReport: true);
                var response = result.Response!;
                using var document = JsonDocument.Parse(mutatePayloadJson(response.Payload.GetRawText()));
                return UnityRequestExecutionResult.Success(response with
                {
                    Payload = document.RootElement.Clone(),
                });
            }),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.Path));

        return await service.ExecuteAsync(CreateInput());
    }

    private static UnityRequestExecutionResult CreateBuildResponseResult (
        string reportResult,
        string completionReason,
        int errorCount,
        string? inputKind = null,
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null,
        IpcBuildLifecycleSnapshot? lifecycleBefore = null,
        IpcBuildLifecycleSnapshot? lifecycleAfter = null,
        string? reportOutputPath = null,
        IpcBuildOutputLayout? outputLayout = null,
        bool useDefaultOutputLayout = true,
        IpcBuildProjectMutationAudit? projectMutation = null,
        IpcUnityBuildProfileInput? unityBuildProfile = null,
        IpcBuildRunnerResultArtifact? runnerResult = null,
        bool omitReport = false)
    {
        return UnityRequestExecutionResult.Success(new UnityRequestResponse(
            IpcPayloadCodec.SerializeToElement(new IpcBuildRunResponse(
                RunId: RunId,
                ProjectFingerprint: ProjectFingerprint,
                LifecycleBefore: lifecycleBefore ?? CreateLifecycleSnapshot("before", canAcceptExecutionRequests: true),
                LifecycleAfter: lifecycleAfter ?? CreateLifecycleSnapshot("after", canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(
                    Checked: true,
                    Dirty: false,
                    Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                    Items: []),
                Input: CreateInputProbe(inputKind, sceneSource, scenes, buildTarget, unityBuildTarget, buildOptions),
                OutputLayout: outputLayout ?? (useDefaultOutputLayout
                    ? new IpcBuildOutputLayout(
                        Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                        LocationPathName: "/workspace/.ucli/output/player/Player")
                    : null),
                UnityBuildProfile: unityBuildProfile,
                Report: omitReport
                    ? null
                    : CreateBuildReportArtifact(
                        reportResult,
                        unityBuildTarget ?? "StandaloneLinux64",
                        reportOutputPath ?? "/workspace/.ucli/output/player/Player",
                        errorCount),
                Logs: new IpcBuildLogSummary(
                    EntryCount: errorCount == 0 ? 3 : 4,
                    ErrorCount: errorCount,
                    WarningCount: 1,
                    CompletionReason: completionReason,
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"))),
                ProjectMutation: projectMutation ?? CreateProjectMutation(mutated: false))
            {
                RunnerResult = runnerResult,
            }),
            [],
            HasFailureStatus: false));
    }

    private static IpcUnityBuildProfileInput CreateUnityBuildProfileInput (
        string path,
        string digest,
        Func<IpcUnityBuildProfileApplyAudit, IpcUnityBuildProfileApplyAudit>? configureApplyAudit = null)
    {
        var lifecycleBefore = CreateLifecycleSnapshot("profile-before", canAcceptExecutionRequests: true);
        var lifecycleAfter = CreateLifecycleSnapshot("profile-after", canAcceptExecutionRequests: true);
        var applyAudit = new IpcUnityBuildProfileApplyAudit(
            Applied: true,
            LifecycleBefore: lifecycleBefore,
            LifecycleAfter: lifecycleAfter,
            GenerationsBefore: new IpcBuildGenerationSnapshot(
                lifecycleBefore.CompileGeneration,
                lifecycleBefore.DomainReloadGeneration,
                lifecycleBefore.AssetRefreshGeneration),
            GenerationsAfter: new IpcBuildGenerationSnapshot(
                lifecycleAfter.CompileGeneration,
                lifecycleAfter.DomainReloadGeneration,
                lifecycleAfter.AssetRefreshGeneration),
            DirtyStateAfter: new IpcBuildDirtyState(
                Checked: true,
                Dirty: false,
                Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                Items: []));
        return new IpcUnityBuildProfileInput(
            Path: path,
            Digest: digest,
            ApplyAudit: configureApplyAudit == null ? applyAudit : configureApplyAudit(applyAudit));
    }

    private static void WriteRunnerResultFiles (
        UnityRequestPayload.BuildRun buildRunPayload,
        IpcBuildRunnerResultArtifact runnerResult,
        string reportResult,
        int errorCount,
        bool writeOutputs,
        bool writeBuildReportSource,
        string? buildReportSourceJson)
    {
        if (writeOutputs)
        {
            foreach (var output in runnerResult.Outputs)
            {
                var outputPath = Path.Combine(buildRunPayload.OutputPath, output);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllText(outputPath, "runner output");
            }
        }

        if (writeBuildReportSource && runnerResult.BuildReport != null)
        {
            var buildReportSourcePath = Path.Combine(buildRunPayload.OutputPath, runnerResult.BuildReport.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(buildReportSourcePath)!);
            if (buildReportSourceJson != null)
            {
                File.WriteAllText(buildReportSourcePath, buildReportSourceJson);
            }
            else
            {
                var unityBuildTarget = buildRunPayload.UnityBuildTarget
                    ?? throw new InvalidOperationException("Test build report source requires a Unity build target.");
                var buildReport = CreateBuildReportArtifact(
                    reportResult,
                    unityBuildTarget,
                    buildRunPayload.OutputPath,
                    errorCount);
                File.WriteAllText(buildReportSourcePath, IpcPayloadCodec.SerializeToElement(buildReport).GetRawText());
            }
        }
    }

    private static IpcBuildRunnerResultArtifact CreateExecuteMethodRunnerResult (
        string? status = null,
        IReadOnlyList<string>? outputs = null,
        IpcBuildRunnerResultBuildReport? buildReport = null,
        long durationMilliseconds = 2500,
        int errorCount = 0,
        int warningCount = 1,
        IReadOnlyList<IpcBuildRunnerDiagnostic>? diagnostics = null)
    {
        return new IpcBuildRunnerResultArtifact(
            Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
            Status: status ?? ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            DurationMilliseconds: durationMilliseconds,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Diagnostics: diagnostics ?? [])
        {
            Outputs = outputs ?? ["player.txt"],
            BuildReport = buildReport,
        };
    }

    private static IpcBuildReportArtifact CreateBuildReportArtifact (
        string reportResult,
        string unityBuildTarget,
        string outputPath,
        int errorCount)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: reportResult,
            UnityBuildTarget: unityBuildTarget,
            OutputPath: outputPath,
            DurationMilliseconds: 2500,
            TotalSizeBytes: 4096,
            ErrorCount: errorCount,
            WarningCount: 1,
            Steps:
            [
                new IpcBuildReportStep(
                    Name: "Build player",
                    DurationMilliseconds: 2500,
                    Depth: 0,
                    MessageCount: 1),
            ],
            Messages:
            [
                new IpcBuildReportMessage(
                    Type: errorCount == 0 ? "warning" : "error",
                    Content: errorCount == 0 ? "Sample warning" : "Sample error"),
            ]);
    }

    private static IpcBuildLifecycleSnapshot CreateLifecycleSnapshot (
        string generationSuffix,
        bool canAcceptExecutionRequests,
        bool omitAssetRefreshGeneration = false)
    {
        return new IpcBuildLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: ProjectFingerprint,
            LifecycleState: "ready",
            BlockingReason: null,
            CompileState: "idle",
            CompileGeneration: $"compile-{generationSuffix}",
            DomainReloadGeneration: $"domain-{generationSuffix}",
            CanAcceptExecutionRequests: canAcceptExecutionRequests,
            ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new IpcPlayModeSnapshot(
                State: "stopped",
                Transition: "none",
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false,
                Generation: $"play-{generationSuffix}"),
            AssetRefreshGeneration: omitAssetRefreshGeneration ? null : $"asset-{generationSuffix}");
    }

    private static IpcBuildProjectMutationAudit CreateProjectMutation (
        bool mutated,
        string mode = "forbid",
        string? coverage = null)
    {
        var resolvedCoverage = coverage ?? ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Full);
        var beforeDigest = new string('1', 64);
        var afterDigest = mutated ? new string('2', 64) : beforeDigest;
        return new IpcBuildProjectMutationAudit(
            Mode: mode,
            Coverage: resolvedCoverage,
            Mutated: mutated,
            BeforeDigest: beforeDigest,
            AfterDigest: afterDigest,
            Items: mutated
                ?
                [
                    new IpcBuildProjectMutationAuditItem(
                        Path: "Assets/Generated.asset",
                        ChangeKind: ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added),
                        BeforeSha256: null,
                        AfterSha256: new string('2', 64)),
                ]
                : []);
    }

    private static IpcBuildInputProbe CreateInputProbe (
        string? inputKind = null,
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null)
    {
        return new IpcBuildInputProbe(
            InputKind: inputKind ?? ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
            BuildTarget: buildTarget ?? "standaloneLinux64",
            UnityBuildTarget: unityBuildTarget ?? "StandaloneLinux64",
            UnityBuildTargetGroup: "Standalone",
            SceneSource: sceneSource ?? ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
            Scenes: scenes ?? ["Assets/Scenes/Main.unity"],
            BuildOptions: buildOptions ?? "Development");
    }

    private static BuildClaimOutput FindClaim (
        BuildExecutionOutput output,
        UcliCode code)
    {
        return output.Claims.Single(claim => string.Equals(claim.Id, code.Value, StringComparison.Ordinal));
    }

    private static void AssertEvidenceRefsResolveToReports (BuildExecutionOutput output)
    {
        foreach (var claim in output.Claims)
        {
            foreach (var evidence in claim.Evidence)
            {
                if (evidence.EvidenceRef == null)
                {
                    continue;
                }

                Assert.True(
                    output.Reports.ContainsKey(evidence.EvidenceRef),
                    $"Claim {claim.Id} references missing report '{evidence.EvidenceRef}'.");
            }
        }
    }

    private static AssuranceSemanticInvariantValidator CreateBuildSemanticInvariantValidator ()
    {
        return new AssuranceSemanticInvariantValidator(
            new CodeCatalogModel([new BuildCodeCatalogContributor()]),
            [new BuildAssuranceSemanticInvariantRule()]);
    }

    private static string CreateExpectedPlayerLocationPathName (
        string outputDirectory,
        string fileName = "Player")
    {
        return string.Concat(
            outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            "/player/",
            fileName);
    }

    private static void AssertProgressEvents (
        CollectingProgressSink progressSink,
        params string[] eventNames)
    {
        Assert.Equal(eventNames.Length, progressSink.Entries.Count);
        for (var i = 0; i < eventNames.Length; i++)
        {
            Assert.Equal(eventNames[i], progressSink.Entries[i].EventName);
        }
    }

    private sealed class CollectingProgressSink : ICommandProgressSink
    {
        private readonly List<ProgressEntry> entries = [];

        public IReadOnlyList<ProgressEntry> Entries => entries;

        public ValueTask OnEntryAsync<TPayload> (
            string eventName,
            TPayload payload,
            CancellationToken cancellationToken = default)
            where TPayload : notnull
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new ProgressEntry(eventName, payload));
            return ValueTask.CompletedTask;
        }
    }

    private sealed record ProgressEntry (
        string EventName,
        object Payload);

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result;
        }

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubBuildProfileFileReader : IBuildProfileFileReader
    {
        private readonly BuildProfileFileReadResult result;

        public StubBuildProfileFileReader (BuildProfileFileReadResult result)
        {
            this.result = result;
        }

        public ValueTask<BuildProfileFileReadResult> ReadAsync (
            string profilePath,
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubEnvironmentVariableReader : IEnvironmentVariableReader
    {
        private readonly IReadOnlyDictionary<string, string?> values;

        public StubEnvironmentVariableReader ()
            : this(new Dictionary<string, string?>(StringComparer.Ordinal))
        {
        }

        public StubEnvironmentVariableReader (IReadOnlyDictionary<string, string?> values)
        {
            this.values = values;
        }

        public string? Get (string variableName)
        {
            return values.TryGetValue(variableName, out var value)
                ? value
                : null;
        }
    }

    private sealed class StubModeDecisionService : IUnityExecutionModeDecisionService
    {
        private readonly UnityExecutionModeDecisionResult result;

        public StubModeDecisionService (UnityExecutionModeDecisionResult result)
        {
            this.result = result;
        }

        public ValueTask<UnityExecutionModeDecisionResult> DecideAsync (
            UnityExecutionMode mode,
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor, IUnityStreamingRequestExecutor
    {
        private readonly Func<UnityRequestPayload, UnityRequestExecutionResult> resultFactory;
        private readonly IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames;

        public StubUnityRequestExecutor (UnityRequestExecutionResult result)
            : this(_ => result)
        {
        }

        public StubUnityRequestExecutor (
            Func<UnityRequestPayload, UnityRequestExecutionResult> resultFactory,
            IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames = null)
        {
            this.resultFactory = resultFactory;
            this.streamingProgressFrames = streamingProgressFrames;
        }

        public UnityRequestPayload? CapturedPayload { get; private set; }

        public TimeSpan? CapturedTimeout { get; private set; }

        public int CallCount { get; private set; }

        public int StreamingCallCount { get; private set; }

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            CapturedPayload = payload;
            CapturedTimeout = timeout;
            return ValueTask.FromResult(resultFactory(payload));
        }

        public async ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            Func<UnityRequestProgressFrame, CancellationToken, ValueTask> onProgressFrame,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StreamingCallCount++;
            CapturedPayload = payload;
            CapturedTimeout = timeout;

            var progressFrames = streamingProgressFrames ?? CreateDefaultProgressFrames((UnityRequestPayload.BuildRun)payload);
            for (var i = 0; i < progressFrames.Count; i++)
            {
                await onProgressFrame(progressFrames[i], cancellationToken).ConfigureAwait(false);
            }

            return resultFactory(payload);
        }

        private static IReadOnlyList<UnityRequestProgressFrame> CreateDefaultProgressFrames (UnityRequestPayload.BuildRun request)
        {
            var runnerKind = request.RunnerKind ?? ContractLiteralCodec.ToValue(BuildProfileRunnerKind.BuildPipeline);
            return
            [
                CreateProgressFrame(
                    BuildRunProgressEventNames.ReadinessCompleted,
                    request,
                    "readiness",
                    runnerKind: null,
                    runnerStatus: null),
                CreateProgressFrame(
                    BuildRunProgressEventNames.RunnerResolved,
                    request,
                    "runnerResolution",
                    runnerKind,
                    runnerStatus: null),
                CreateProgressFrame(
                    BuildRunProgressEventNames.RunnerStarted,
                    request,
                    "runnerInvocation",
                    runnerKind,
                    runnerStatus: null),
                CreateProgressFrame(
                    BuildRunProgressEventNames.RunnerCompleted,
                    request,
                    "runnerResult",
                    runnerKind,
                    ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded)),
            ];
        }

        private static UnityRequestProgressFrame CreateProgressFrame (
            string eventName,
            UnityRequestPayload.BuildRun request,
            string phase,
            string? runnerKind,
            string? runnerStatus)
        {
            return new UnityRequestProgressFrame(
                eventName,
                IpcPayloadCodec.SerializeToElement(new BuildProgressEntry(
                    RunId: request.RunId,
                    ProfileDigest: request.ProfileDigest!,
                    Phase: phase,
                    RunnerKind: runnerKind,
                    RunnerStatus: runnerStatus,
                    Verdict: null,
                    ReportRefs: [],
                    ErrorCode: null)));
        }
    }

    private sealed class StubBuildRunIdFactory : IBuildRunIdFactory
    {
        private readonly string runId;

        public StubBuildRunIdFactory (string runId)
        {
            this.runId = runId;
        }

        public string Create ()
        {
            return runId;
        }
    }

    private sealed class StubBuildRunArtifactStore : IBuildRunArtifactStore
    {
        private readonly string rootPath;

        private readonly Func<BuildRunArtifactAccountingRequest, CancellationToken, ValueTask<BuildRunArtifactAccountingOperationResult>>? accountArtifactsOverride;

        public StubBuildRunArtifactStore (
            string rootPath,
            Func<BuildRunArtifactAccountingRequest, CancellationToken, ValueTask<BuildRunArtifactAccountingOperationResult>>? accountArtifactsOverride = null)
        {
            this.rootPath = rootPath;
            this.accountArtifactsOverride = accountArtifactsOverride;
        }

        public BuildRunArtifactPaths? PreparedPaths { get; private set; }

        public BuildRunMetadataDocument? WrittenMetadata { get; private set; }

        public BuildRunArtifactAccountingRequest? AccountingRequest { get; private set; }

        public IpcBuildOutputLayout? PreparedOutputLayout { get; private set; }

        public BuildRunArtifactPreparationResult Prepare (
            ResolvedUnityProjectContext unityProject,
            string runId)
        {
            var runDirectory = Path.Combine(rootPath, runId);
            var runnerOutputDirectory = Path.Combine(rootPath, "work", runId, "output");
            var artifactOutputDirectory = Path.Combine(runDirectory, "output");
            Directory.CreateDirectory(runnerOutputDirectory);
            PreparedPaths = new BuildRunArtifactPaths(
                RepositoryRoot: rootPath,
                RunId: runId,
                ArtifactsDirectory: runDirectory,
                BuildJsonPath: Path.Combine(runDirectory, "build.json"),
                BuildReportJsonPath: Path.Combine(runDirectory, "build-report.json"),
                BuildLogPath: Path.Combine(runDirectory, "build.log"),
                OutputManifestJsonPath: Path.Combine(runDirectory, "output-manifest.json"),
                RunnerOutputDirectory: runnerOutputDirectory,
                ArtifactOutputDirectory: artifactOutputDirectory);
            return BuildRunArtifactPreparationResult.Success(PreparedPaths);
        }

        public BuildRunArtifactPreparationResult PrepareBuildPipelineOutputLayout (
            BuildRunArtifactPaths paths,
            string buildTarget,
            IpcBuildOutputLayout outputLayout)
        {
            PreparedOutputLayout = outputLayout;
            return BuildRunArtifactPreparationResult.Success(paths);
        }

        public ValueTask<BuildRunArtifactAccountingOperationResult> AccountArtifactsAsync (
            BuildRunArtifactAccountingRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AccountingRequest = request;
            if (accountArtifactsOverride != null)
            {
                return accountArtifactsOverride(request, cancellationToken);
            }

            var buildReport = request.BuildReport == null
                ? null
                : new BuildArtifactRef(BuildArtifactKind.BuildReport, "build-report.json", BuildReportArtifactDigest);
            return ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Success(new BuildRunArtifactAccountingResult(
                BuildReport: buildReport,
                BuildOutputManifest: new BuildArtifactRef(BuildArtifactKind.BuildOutputManifest, "output-manifest.json", BuildOutputManifestArtifactDigest),
                BuildLog: new BuildArtifactRef(BuildArtifactKind.BuildLog, "build.log", BuildLogArtifactDigest),
                OutputManifest: new BuildOutputManifestSummary(
                    ManifestDigest: OutputManifestDigest,
                    EntryCount: request.OutputSources.Count,
                    FileCount: request.OutputSources.Count,
                    TotalBytes: request.OutputSources.Count == 0 ? 0 : 12))));
        }

        public ValueTask<BuildArtifactRefWriteResult> WriteMetadataAsync (
            BuildRunMetadataWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WrittenMetadata = request.Metadata;
            return ValueTask.FromResult(BuildArtifactRefWriteResult.Success(new BuildArtifactRef(
                BuildArtifactKind.Build,
                "build.json",
                BuildMetadataDigest)));
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory (string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create ()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ucli-build-service-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose ()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
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
