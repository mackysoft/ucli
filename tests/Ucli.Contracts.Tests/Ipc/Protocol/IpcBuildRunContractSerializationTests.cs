using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Common.IpcBuildContractSerializationTestSupport;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBuildRunContractSerializationTests
{
    private const string RunIdText = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private static readonly Guid RunId = Guid.Parse(RunIdText);

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunContracts_SerializeWithCamelCaseFields ()
    {
        var request = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunRequest(
                RunId: RunId,
                InputKind: BuildProfileInputsKind.Explicit,
                BuildTarget: BuildTargetStableName.StandaloneLinux64,
                SceneSource: BuildProfileSceneSource.Explicit,
                ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
                Development: true,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: null,
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: [DaemonEditorMode.Batchmode],
                ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
                RunnerKind: BuildRunnerKind.ExecuteMethod,
                ProfileDigest: Sha256Digest.Parse(new string('c', 64)),
                UnityBuildProfile: null,
                ProfilePath: "/workspace/UnityProject/.ucli/build/player.json",
                RunnerMethod: "Build.Entry.Run",
                RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["output"] = "/tmp/ucli/output",
                },
                RunnerEnvironmentVariables: ["BUILD_MODE"],
                RunnerEnvironmentSecrets: ["UNITY_LICENSE"],
                RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["BUILD_MODE"] = "release",
                },
                RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UNITY_LICENSE"] = "license-value",
                }));
        var response = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunResponse(
                RunId: RunId,
                ProjectFingerprint: TestProjectFingerprint,
                LifecycleBefore: CreateBuildLifecycleSnapshot(10, canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot(11, canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(
                    Dirty: false,
                    Coverage: IpcBuildDirtyStateCoverage.Full,
                    Items: []),
                Input: new IpcBuildInputProbe(
                    InputKind: BuildProfileInputsKind.Explicit,
                    BuildTarget: BuildTargetStableName.StandaloneLinux64,
                    UnityBuildTarget: "StandaloneLinux64",
                    UnityBuildTargetGroup: "Standalone",
                    SceneSource: BuildProfileSceneSource.Explicit,
                    Scenes: [new SceneAssetPath("Assets/Scenes/Main.unity")],
                    BuildOptions: "Development"),
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: IpcBuildOutputLayoutShape.File,
                    LocationPathName: "/tmp/ucli/output/player/Player"),
                UnityBuildProfile: null,
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: IpcBuildReportResult.Succeeded,
                    UnityBuildTarget: "StandaloneLinux64",
                    OutputPath: "/tmp/ucli/output/build",
                    DurationMilliseconds: 2500,
                    TotalSizeBytes: 4096,
                    ErrorCount: 0,
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
                            Type: "warning",
                            Content: "Sample warning"),
                    ]),
                Logs: new IpcBuildLogSummary(
                    EntryCount: 3,
                    ErrorCount: 0,
                    WarningCount: 1,
                    CompletionReason: IpcBuildLogCompletionReason.Completed,
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"),
                        CursorStart: new IpcLogCursor("abcdef0123456789abcdef0123456789:10"),
                        CursorEnd: new IpcLogCursor("abcdef0123456789abcdef0123456789:20"))),
                ProjectMutation: CreateProjectMutationAudit(),
                RunnerResult: new IpcBuildRunnerResultArtifact(
                    Source: IpcBuildRunnerResultSource.UcliBuildRunnerResult,
                    Status: IpcBuildReportResult.Succeeded,
                    DurationMilliseconds: 2500,
                    ErrorCount: 0,
                    WarningCount: 1,
                    Diagnostics:
                    [
                        new IpcBuildRunnerDiagnostic(
                            Severity: UcliDiagnosticSeverity.Warning,
                            Code: "sample-warning",
                            Message: "Sample warning"),
                    ],
                    Outputs: [new BuildRunnerOutputPath("player/Player")],
                    BuildReport: new IpcBuildRunnerResultBuildReport(
                        new BuildRunnerOutputPath("reports/build-report.json")))));

        JsonAssert.For(request)
            .HasString("runId", RunIdText)
            .HasString("inputKind", ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit))
            .HasString("buildTarget", "standaloneLinux64")
            .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit))
            .HasArrayLength("scenePaths", 1)
            .HasProperty("scenePaths", 0, scene => scene
                .HasString("Assets/Scenes/Main.unity"))
            .HasBoolean("development", true)
            .HasString("outputPath", "/tmp/ucli/output")
            .HasString("buildReportPath", "/tmp/ucli/build-report.json")
            .HasString("buildLogPath", "/tmp/ucli/build.log")
            .HasArrayLength("allowedEditorModes", 1)
            .HasProperty("allowedEditorModes", 0, mode => mode
                .HasString("batchmode"))
            .HasString("projectMutationMode", "forbid")
            .HasString("runnerKind", "executeMethod")
            .HasString("profilePath", "/workspace/UnityProject/.ucli/build/player.json")
            .HasString("profileDigest", new string('c', 64))
            .HasString("runnerMethod", "Build.Entry.Run")
            .HasProperty("runnerArguments", arguments => arguments
                .HasString("output", "/tmp/ucli/output"))
            .HasArrayLength("runnerEnvironmentVariables", 1)
            .HasProperty("runnerEnvironmentVariables", 0, environment => environment
                .HasString("BUILD_MODE"))
            .HasArrayLength("runnerEnvironmentSecrets", 1)
            .HasProperty("runnerEnvironmentSecrets", 0, environment => environment
                .HasString("UNITY_LICENSE"))
            .HasProperty("runnerEnvironmentVariableValues", environment => environment
                .HasString("BUILD_MODE", "release"))
            .HasProperty("runnerEnvironmentSecretValues", environment => environment
                .HasString("UNITY_LICENSE", "license-value"));
        Assert.False(request.TryGetProperty("timeoutMilliseconds", out _));
        Assert.False(request.TryGetProperty("unityBuildTarget", out _));
        Assert.False(request.TryGetProperty("outputLayout", out _));
        JsonAssert.For(response)
            .HasString("runId", RunIdText)
            .HasString("projectFingerprint", TestProjectFingerprint.ToString())
            .HasProperty("lifecycleBefore", lifecycle => lifecycle
                .HasProperty("state", state => state
                    .HasProperty("generations", generations => generations
                        .HasInt32("compileGeneration", 10)
                        .HasInt32("domainReloadGeneration", 10)
                        .HasInt32("assetRefreshGeneration", 10)
                        .HasInt32("playModeGeneration", 10))))
            .HasProperty("lifecycleAfter", lifecycle => lifecycle
                .HasProperty("state", state => state
                    .HasProperty("generations", generations => generations
                        .HasInt32("compileGeneration", 11)
                        .HasInt32("domainReloadGeneration", 11)
                        .HasInt32("assetRefreshGeneration", 11)
                        .HasInt32("playModeGeneration", 11))))
            .HasProperty("dirtyState", dirty => dirty
                .HasBoolean("dirty", false)
                .HasString("coverage", ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full))
                .HasArrayLength("items", 0))
            .HasProperty("input", input => input
                .HasString("inputKind", ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit))
                .HasString("buildTarget", "standaloneLinux64")
                .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit)))
            .HasProperty("outputLayout", outputLayout => outputLayout
                .HasString("shape", ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File))
                .HasString("locationPathName", "/tmp/ucli/output/player/Player"))
            .HasProperty("report", report => report
                .HasInt32("schemaVersion", 1)
                .HasString("result", ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded))
                .HasString("unityBuildTarget", "StandaloneLinux64")
                .HasString("outputPath", "/tmp/ucli/output/build")
                .HasInt32("durationMilliseconds", 2500)
                .HasInt32("totalSizeBytes", 4096)
                .HasInt32("errorCount", 0)
                .HasInt32("warningCount", 1)
                .HasArrayLength("steps", 1)
                .HasProperty("steps", 0, step => step
                    .HasString("name", "Build player")
                    .HasInt32("durationMilliseconds", 2500)
                    .HasInt32("depth", 0)
                    .HasInt32("messageCount", 1))
                .HasArrayLength("messages", 1)
                .HasProperty("messages", 0, message => message
                    .HasString("type", "warning")
                    .HasString("content", "Sample warning")))
            .HasProperty("logs", logs => logs
                .HasInt32("entryCount", 3)
                .HasInt32("errorCount", 0)
                .HasInt32("warningCount", 1)
                .HasString("completionReason", ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed))
                .HasProperty("window", window => window
                    .HasString("startedAtUtc", "2026-06-12T00:00:00+00:00")
                    .HasString("completedAtUtc", "2026-06-12T00:00:03+00:00")
                    .HasString("cursorStart", "abcdef0123456789abcdef0123456789:10")
                    .HasString("cursorEnd", "abcdef0123456789abcdef0123456789:20")))
            .HasProperty("runnerResult", runnerResult => runnerResult
                .HasString("source", ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult))
                .HasString("status", ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded))
                .HasInt32("durationMilliseconds", 2500)
                .HasInt32("errorCount", 0)
                .HasInt32("warningCount", 1)
                .HasArrayLength("outputs", 1)
                .HasProperty("outputs", 0, output => output
                    .HasString("player/Player"))
                .HasProperty("buildReport", buildReport => buildReport
                    .HasString("path", "reports/build-report.json"))
                .HasArrayLength("diagnostics", 1)
                .HasProperty("diagnostics", 0, diagnostic => diagnostic
                    .HasString("severity", "warning")
                    .HasString("code", "sample-warning")
                    .HasString("message", "Sample warning")))
            .HasProperty("projectMutation", mutation => mutation
                .HasString("mode", "forbid")
                .HasString("coverage", ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Full))
                .HasBoolean("mutated", true)
                .HasString("beforeDigest", new string('a', 64))
                .HasString("afterDigest", new string('b', 64))
                .HasArrayLength("items", 1)
                .HasProperty("items", 0, item => item
                    .HasString("path", "Assets/Generated.asset")
                    .HasString("changeKind", ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added))
                    .HasValueKind("beforeSha256", JsonValueKind.Null)
                    .HasString("afterSha256", new string('b', 64))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void NullableBuildInputAndMutationDigestWireFields_RoundTripNull ()
    {
        var request = new IpcBuildRunRequest(
            RunId: RunId,
            InputKind: BuildProfileInputsKind.UnityBuildProfile,
            BuildTarget: null,
            SceneSource: null,
            ScenePaths: [],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: null,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            ProfileDigest: Sha256Digest.Parse(new string('c', 64)),
            UnityBuildProfile: new IpcUnityBuildProfileInput(
                Path: new UnityBuildProfileAssetPath("Assets/BuildProfiles/Linux.asset"),
                Digest: null,
                ApplyAudit: null),
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>());
        var requestJson = IpcPayloadCodec.SerializeToElement(request);

        Assert.True(IpcPayloadCodec.TryDeserialize(requestJson, out IpcBuildRunRequest requestRoundTrip, out _));
        Assert.Equal(Sha256Digest.Parse(new string('c', 64)), requestRoundTrip.ProfileDigest);
        Assert.NotNull(requestRoundTrip.UnityBuildProfile);
        Assert.Equal(
            "Assets/BuildProfiles/Linux.asset",
            requestRoundTrip.UnityBuildProfile!.Path.Value);
        Assert.Null(requestRoundTrip.UnityBuildProfile!.Digest);

        var itemJson = IpcPayloadCodec.SerializeToElement(new IpcBuildProjectMutationAuditItem(
            Path: new ProjectMutationAuditPath("Assets/Generated.asset"),
            ChangeKind: IpcBuildProjectMutationChangeKind.Added,
            BeforeSha256: null,
            AfterSha256: Sha256Digest.Parse(new string('b', 64))));

        Assert.True(IpcPayloadCodec.TryDeserialize(itemJson, out IpcBuildProjectMutationAuditItem itemRoundTrip, out _));
        Assert.Null(itemRoundTrip.BeforeSha256);
        Assert.Equal(Sha256Digest.Parse(new string('b', 64)), itemRoundTrip.AfterSha256);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WithNullProfileDigest_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new IpcBuildRunRequest(
            RunId: RunId,
            InputKind: BuildProfileInputsKind.Explicit,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            SceneSource: BuildProfileSceneSource.Explicit,
            ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: new IpcBuildOutputLayout(
                IpcBuildOutputLayoutShape.File,
                "/tmp/ucli/output/player/Player"),
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            ProfileDigest: null!,
            UnityBuildProfile: null,
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>()));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("inputKind")]
    [InlineData("projectMutationMode")]
    [InlineData("runnerKind")]
    [InlineData("profileDigest")]
    public void IpcBuildRunRequest_WithMissingRequiredTypedProperty_FailsDeserialization (string propertyName)
    {
        var jsonObject = JsonNode.Parse(IpcPayloadCodec.SerializeToElement(CreateMinimalRequest()).GetRawText())!.AsObject();
        Assert.True(jsonObject.Remove(propertyName));
        using var document = JsonDocument.Parse(jsonObject.ToJsonString());

        var result = IpcPayloadCodec.TryDeserialize(
            document.RootElement,
            out IpcBuildRunRequest _,
            out var error);

        Assert.False(result);
        Assert.Equal(IpcPayloadReadErrorKind.DeserializeFailed, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunRequest_WithUnspecifiedContractEnum_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMinimalRequest(inputKind: (BuildProfileInputsKind)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMinimalRequest(projectMutationMode: (BuildProfileProjectMutationMode)0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateMinimalRequest(runnerKind: (BuildRunnerKind)0));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcBuildProjectMutationChangeKind.Added, true, true)]
    [InlineData(IpcBuildProjectMutationChangeKind.Added, false, false)]
    [InlineData(IpcBuildProjectMutationChangeKind.Modified, false, true)]
    [InlineData(IpcBuildProjectMutationChangeKind.Modified, true, false)]
    [InlineData(IpcBuildProjectMutationChangeKind.Deleted, false, false)]
    [InlineData(IpcBuildProjectMutationChangeKind.Deleted, true, true)]
    public void IpcBuildProjectMutationAuditItem_WithInconsistentDigestShape_ThrowsArgumentException (
        IpcBuildProjectMutationChangeKind changeKind,
        bool hasBefore,
        bool hasAfter)
    {
        var before = hasBefore ? Sha256Digest.Parse(new string('a', 64)) : null;
        var after = hasAfter ? Sha256Digest.Parse(new string('b', 64)) : null;

        Assert.Throws<ArgumentException>(() => new IpcBuildProjectMutationAuditItem(
            Path: new ProjectMutationAuditPath("Assets/Generated.asset"),
            ChangeKind: changeKind,
            BeforeSha256: before,
            AfterSha256: after));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildProjectMutationAudit_WithInconsistentAggregate_ThrowsArgumentException ()
    {
        var before = Sha256Digest.Parse(new string('a', 64));
        var after = Sha256Digest.Parse(new string('b', 64));

        Assert.Throws<ArgumentException>(() => new IpcBuildProjectMutationAudit(
            Mode: BuildProfileProjectMutationMode.Forbid,
            Coverage: IpcBuildProjectMutationAuditCoverage.Full,
            Mutated: false,
            BeforeDigest: before,
            AfterDigest: after,
            Items: []));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildProjectMutationAudit_WithNullRequiredDigest_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new IpcBuildProjectMutationAudit(
            Mode: BuildProfileProjectMutationMode.Forbid,
            Coverage: IpcBuildProjectMutationAuditCoverage.Full,
            Mutated: false,
            BeforeDigest: null!,
            AfterDigest: Sha256Digest.Parse(new string('a', 64)),
            Items: []));
    }

    private static IpcBuildRunRequest CreateMinimalRequest (
        BuildProfileInputsKind inputKind = BuildProfileInputsKind.Explicit,
        BuildProfileProjectMutationMode projectMutationMode = BuildProfileProjectMutationMode.Forbid,
        BuildRunnerKind runnerKind = BuildRunnerKind.BuildPipeline)
    {
        return new IpcBuildRunRequest(
            RunId: RunId,
            InputKind: inputKind,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            SceneSource: BuildProfileSceneSource.Explicit,
            ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
            Development: false,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: new IpcBuildOutputLayout(
                Shape: IpcBuildOutputLayoutShape.File,
                LocationPathName: "/tmp/ucli/output/player/Player"),
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
            ProjectMutationMode: projectMutationMode,
            RunnerKind: runnerKind,
            ProfileDigest: Sha256Digest.Parse(new string('c', 64)),
            UnityBuildProfile: null,
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>());
    }
}
