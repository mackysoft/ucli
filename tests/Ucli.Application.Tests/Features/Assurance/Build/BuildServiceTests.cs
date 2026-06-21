using System.Text.Json;
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
        Assert.Equal(BuildReportRefs.BuildReport, output.Build.Summary.ReportRef);
        Assert.Equal(BuildReportRefs.BuildLog, output.Build.Logs.ReportRef);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed), output.Build.Logs.CompletionReason);
        Assert.Equal("asset-before", output.Build.Generations.Before.AssetRefreshGeneration);
        Assert.Equal("asset-after", output.Build.Generations.After.AssetRefreshGeneration);
        Assert.Equal("asset-after", output.Build.Generations.ValidFor.AssetRefreshGeneration);
        var expectedProfileDigest = BuildProfileResolver.ResolveJson(ProfileJson).Profile!.Digest;
        Assert.Equal(expectedProfileDigest, output.Build.Profile.Digest);
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
        Assert.True(output.Reports.ContainsKey(output.Build.Output.ManifestRef));
        AssertEvidenceRefsResolveToReports(output);
        Assert.Equal(BuildClaimCodes.All.Count, output.Claims.Count);
        Assert.All(output.Claims, claim => Assert.True(claim.Required));
        var verifier = Assert.Single(output.Verifiers);
        Assert.Equal("build", verifier.Id);
        Assert.Equal(BuildClaimCodes.All.Select(static code => code.Value).ToArray(), verifier.PrimaryClaims);
        Assert.Equal(BuildPipelineEffectValues, verifier.Effects);
        var preparedPaths = artifactStore.PreparedPaths;
        Assert.NotNull(preparedPaths);
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.Equal(
            ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            artifactStore.WrittenMetadata!.Summary.GetProperty("result").GetString());
        Assert.Equal(output.Build.Profile.Path, artifactStore.WrittenMetadata.Profile.GetProperty("path").GetString());
        Assert.Equal(expectedProfileDigest, artifactStore.WrittenMetadata.Profile.GetProperty("digest").GetString());
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
        Assert.Equal(output.Build.Output.ManifestRef, artifactStore.WrittenMetadata.Output.GetProperty("manifestRef").GetString());
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
            BuildRunProgressEventNames.Completed);
        var startedEntry = Assert.IsType<BuildRunStartedEntry>(progressSink.Entries[0].Payload);
        Assert.Equal(RunId, startedEntry.RunId);
        Assert.Equal(ProjectFingerprint, startedEntry.ProjectFingerprint);
        Assert.Equal("auto", startedEntry.RequestedMode);
        Assert.Equal("oneshot", startedEntry.ResolvedMode);
        Assert.Equal("transientProbe", startedEntry.SessionKind);
        Assert.Equal(10000, startedEntry.TimeoutMilliseconds);
        Assert.Equal("standaloneLinux64", startedEntry.BuildTarget);
        Assert.Equal(preparedPaths.RunnerOutputDirectory, startedEntry.OutputPath);
        var completedEntry = Assert.IsType<BuildRunCompletedEntry>(progressSink.Entries[1].Payload);
        Assert.Equal(RunId, completedEntry.RunId);
        Assert.Equal(ContractLiteralCodec.ToValue(BuildVerdict.Pass), completedEntry.Verdict);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded), completedEntry.Result);
        Assert.Equal(ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed), completedEntry.CompletionReason);
        Assert.Equal(0, completedEntry.ErrorCount);
        Assert.Equal(1, completedEntry.WarningCount);
        Assert.Equal(preparedPaths.BuildJsonPath, completedEntry.BuildJsonPath);
        Assert.Equal(preparedPaths.BuildReportJsonPath, completedEntry.BuildReportPath);
        Assert.Equal(preparedPaths.BuildLogPath, completedEntry.BuildLogPath);
        Assert.Equal(preparedPaths.OutputManifestJsonPath, completedEntry.OutputManifestPath);

        var validator = CreateBuildSemanticInvariantValidator();
        var semanticPayload = JsonSerializer.SerializeToElement(output, PayloadSerializerOptions);
        var semanticResult = validator.Validate(semanticPayload);
        Assert.True(semanticResult.IsValid, string.Join(Environment.NewLine, semanticResult.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));

        var requestPayload = Assert.IsType<UnityRequestPayload.BuildRun>(requestExecutor.CapturedPayload);
        Assert.Equal(RunId, requestPayload.RunId);
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
        Assert.Equal(requestPayload.OutputLayout.LocationPathName, outputSource.SourcePath);
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
            runnerResult: new IpcBuildRunnerResultArtifact(
                Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                DurationMilliseconds: 2500,
                ErrorCount: 0,
                WarningCount: 1,
                Diagnostics: []));
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
        Assert.Empty(accountingRequest.OutputSources);
        Assert.True(accountingRequest.AllowEmptyOutputManifest);

        var output = result.Output!;
        Assert.Equal("executeMethod", output.Build.Runner.Kind);
        Assert.Equal("Build.Entry.Run", output.Build.Runner.Method);
        Assert.Equal(["UCLI_MODE"], output.Build.Runner.Invocation.Environment.Variables);
        Assert.Equal(["UCLI_SECRET"], output.Build.Runner.Invocation.Environment.Secrets);
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(output, PayloadSerializerOptions));
        Assert.NotNull(artifactStore.WrittenMetadata);
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(artifactStore.WrittenMetadata!, PayloadSerializerOptions));
        Assert.DoesNotContain(SecretValue, JsonSerializer.Serialize(progressSink.Entries, PayloadSerializerOptions));
        Assert.Equal("executeMethod", artifactStore.WrittenMetadata!.Runner.GetProperty("kind").GetString());
        Assert.Equal(
            ["UCLI_MODE"],
            artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment").GetProperty("variables").EnumerateArray().Select(static item => item.GetString()!).ToArray());
        Assert.Equal(
            ["UCLI_SECRET"],
            artifactStore.WrittenMetadata.Runner.GetProperty("invocation").GetProperty("environment").GetProperty("secrets").EnumerateArray().Select(static item => item.GetString()!).ToArray());
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
                runnerResult: new IpcBuildRunnerResultArtifact(
                    Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                    Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
                    DurationMilliseconds: 2500,
                    ErrorCount: 1,
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
        Assert.Equal("editorBuildSettings", result.Output!.Build.Scenes.Source);
        Assert.Equal(["Assets/Scenes/FromSettings.unity"], result.Output.Build.Scenes.Paths);
        var metadataScenePaths = artifactStore.WrittenMetadata!.Input
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
        Assert.Equal(BuildRiskCodes.ProjectMutationAuditCoverageIncomplete.Value, risk.Code);
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
        IBuildRunIdFactory? runIdFactory = null,
        TimeProvider? timeProvider = null)
    {
        return new BuildService(
            projectContextResolver ?? new StubProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())),
            profileFileReader ?? new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(ProfileJson, "/workspace/build.ucli.json")),
            environmentVariableReader ?? new StubEnvironmentVariableReader(),
            modeDecisionService ?? new StubModeDecisionService(UnityExecutionModeDecisionResult.Success(new UnityExecutionModeDecision(
                UnityExecutionMode.Auto,
                DaemonRunning: false,
                UnityExecutionTarget.Oneshot,
                TimeSpan.FromSeconds(10)))),
            requestExecutor ?? CreateBuildResponseExecutor(
                ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                errorCount: 0),
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
        IpcBuildRunnerResultArtifact? runnerResult = null)
    {
        return new StubUnityRequestExecutor(payload =>
        {
            var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
            return CreateBuildResponseResult(
                reportResult,
                completionReason,
                errorCount,
                sceneSource,
                scenes,
                buildTarget,
                unityBuildTarget,
                buildOptions,
                lifecycleBefore,
                lifecycleAfter,
                reportOutputPath: reportOutputPath ?? buildRunPayload.OutputLayout?.LocationPathName ?? buildRunPayload.OutputPath,
                projectMutation: projectMutation,
                runnerResult: runnerResult);
        });
    }

    private static UnityRequestExecutionResult CreateBuildResponseResult (
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
        IpcBuildRunnerResultArtifact? runnerResult = null)
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
                Input: CreateInputProbe(sceneSource, scenes, buildTarget, unityBuildTarget, buildOptions),
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: reportResult,
                    UnityBuildTarget: unityBuildTarget ?? "StandaloneLinux64",
                    OutputPath: reportOutputPath ?? "/workspace/.ucli/output/player/Player",
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
                    ]),
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
        string? sceneSource = null,
        IReadOnlyList<string>? scenes = null,
        string? buildTarget = null,
        string? unityBuildTarget = null,
        string? buildOptions = null)
    {
        return new IpcBuildInputProbe(
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

    private static string CreateExpectedPlayerLocationPathName (string outputDirectory)
    {
        return string.Concat(
            outputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            "/player/Player");
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

    private sealed class StubUnityRequestExecutor : IUnityRequestExecutor
    {
        private readonly Func<UnityRequestPayload, UnityRequestExecutionResult> resultFactory;

        public StubUnityRequestExecutor (UnityRequestExecutionResult result)
            : this(_ => result)
        {
        }

        public StubUnityRequestExecutor (Func<UnityRequestPayload, UnityRequestExecutionResult> resultFactory)
        {
            this.resultFactory = resultFactory;
        }

        public UnityRequestPayload? CapturedPayload { get; private set; }

        public TimeSpan? CapturedTimeout { get; private set; }

        public int CallCount { get; private set; }

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

            return ValueTask.FromResult(BuildRunArtifactAccountingOperationResult.Success(new BuildRunArtifactAccountingResult(
                BuildReport: new BuildArtifactRef(BuildArtifactKind.BuildReport, "build-report.json", BuildReportArtifactDigest),
                BuildOutputManifest: new BuildArtifactRef(BuildArtifactKind.BuildOutputManifest, "output-manifest.json", BuildOutputManifestArtifactDigest),
                BuildLog: new BuildArtifactRef(BuildArtifactKind.BuildLog, "build.log", BuildLogArtifactDigest),
                OutputManifest: new BuildOutputManifestSummary(
                    ManifestDigest: OutputManifestDigest,
                    EntryCount: 1,
                    FileCount: 1,
                    TotalBytes: 12))));
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
