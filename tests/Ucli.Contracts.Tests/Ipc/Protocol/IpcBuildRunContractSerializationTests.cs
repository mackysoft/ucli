using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Common.IpcBuildContractSerializationTestSupport;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBuildRunContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildRunContracts_SerializeWithCamelCaseFields ()
    {
        var request = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunRequest(
                RunId: "build-run-1",
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
                ScenePaths: ["Assets/Scenes/Main.unity"],
                Development: true,
                OutputPath: "/tmp/ucli/output",
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                    LocationPathName: "/tmp/ucli/output/player/Player"),
                BuildReportPath: "/tmp/ucli/build-report.json",
                BuildLogPath: "/tmp/ucli/build.log",
                AllowedEditorModes: ["batchmode"],
                ProjectMutationMode: "forbid",
                RunnerKind: ContractLiteralCodec.ToValue(IpcBuildRunnerKind.ExecuteMethod))
            {
                TimeoutMilliseconds = 1234,
                ProfilePath = "/workspace/UnityProject/.ucli/build/player.json",
                ProfileDigest = new string('c', 64),
                RunnerMethod = "Build.Entry.Run",
                RunnerArguments = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["output"] = "/tmp/ucli/output",
                },
                RunnerEnvironmentVariables = ["BUILD_MODE"],
                RunnerEnvironmentSecrets = ["UNITY_LICENSE"],
                RunnerEnvironmentVariableValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["BUILD_MODE"] = "release",
                },
                RunnerEnvironmentSecretValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["UNITY_LICENSE"] = "license-value",
                },
            });
        var response = IpcPayloadCodec.SerializeToElement(
            new IpcBuildRunResponse(
                RunId: "build-run-1",
                ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
                LifecycleBefore: CreateBuildLifecycleSnapshot("before", canAcceptExecutionRequests: true),
                LifecycleAfter: CreateBuildLifecycleSnapshot("after", canAcceptExecutionRequests: true),
                DirtyState: new IpcBuildDirtyState(
                    Checked: true,
                    Dirty: false,
                    Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                    Items: []),
                Input: new IpcBuildInputProbe(
                    InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
                    BuildTarget: "standaloneLinux64",
                    UnityBuildTarget: "StandaloneLinux64",
                    UnityBuildTargetGroup: "Standalone",
                    SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
                    Scenes: ["Assets/Scenes/Main.unity"],
                    BuildOptions: "Development"),
                OutputLayout: new IpcBuildOutputLayout(
                    Shape: ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File),
                    LocationPathName: "/tmp/ucli/output/player/Player"),
                UnityBuildProfile: null,
                Report: new IpcBuildReportArtifact(
                    SchemaVersion: 1,
                    Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
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
                    CompletionReason: ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Completed),
                    Window: new IpcBuildLogWindow(
                        StartedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                        CompletedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:03+00:00"),
                        CursorStart: "stream-1:10",
                        CursorEnd: "stream-1:20")),
                ProjectMutation: CreateProjectMutationAudit())
            {
                RunnerResult = new IpcBuildRunnerResultArtifact(
                    Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
                    Status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
                    DurationMilliseconds: 2500,
                    ErrorCount: 0,
                    WarningCount: 1,
                    Diagnostics:
                    [
                        new IpcBuildRunnerDiagnostic(
                            Severity: "warning",
                            Code: "sample-warning",
                            Message: "Sample warning"),
                    ])
                {
                    Outputs = ["player/Player"],
                    BuildReport = new IpcBuildRunnerResultBuildReport("reports/build-report.json"),
                },
            });

        JsonAssert.For(request)
            .HasString("runId", "build-run-1")
            .HasString("inputKind", ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit))
            .HasString("buildTarget", "standaloneLinux64")
            .HasString("unityBuildTarget", "StandaloneLinux64")
            .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit))
            .HasArrayLength("scenePaths", 1)
            .HasProperty("scenePaths", 0, scene => scene
                .HasString("Assets/Scenes/Main.unity"))
            .HasBoolean("development", true)
            .HasString("outputPath", "/tmp/ucli/output")
            .HasProperty("outputLayout", outputLayout => outputLayout
                .HasString("shape", ContractLiteralCodec.ToValue(IpcBuildOutputLayoutShape.File))
                .HasString("locationPathName", "/tmp/ucli/output/player/Player"))
            .HasString("buildReportPath", "/tmp/ucli/build-report.json")
            .HasString("buildLogPath", "/tmp/ucli/build.log")
            .HasArrayLength("allowedEditorModes", 1)
            .HasProperty("allowedEditorModes", 0, mode => mode
                .HasString("batchmode"))
            .HasString("projectMutationMode", "forbid")
            .HasInt32("timeoutMilliseconds", 1234)
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
        JsonAssert.For(response)
            .HasString("runId", "build-run-1")
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasProperty("lifecycleBefore", lifecycle => lifecycle
                .HasString("compileGeneration", "compile-before")
                .HasString("domainReloadGeneration", "domain-before")
                .HasString("assetRefreshGeneration", "asset-before")
                .HasBoolean("canAcceptExecutionRequests", true))
            .HasProperty("lifecycleAfter", lifecycle => lifecycle
                .HasString("compileGeneration", "compile-after")
                .HasString("domainReloadGeneration", "domain-after")
                .HasString("assetRefreshGeneration", "asset-after"))
            .HasProperty("dirtyState", dirty => dirty
                .HasBoolean("checked", true)
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
                    .HasString("cursorStart", "stream-1:10")
                    .HasString("cursorEnd", "stream-1:20")))
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
}
