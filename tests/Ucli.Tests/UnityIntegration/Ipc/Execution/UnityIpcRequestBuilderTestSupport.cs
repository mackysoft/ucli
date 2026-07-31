using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

internal static class UnityIpcRequestBuilderTestSupport
{
    public static LifecycleExecutionRegistration CreateLifecycleRegistration (
        LifecycleExecutionKind kind,
        Guid? executionId = null,
        TimeProvider? timeProvider = null,
        TimeSpan? executionTimeout = null)
    {
        var startedAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow();
        return new LifecycleExecutionRegistration(
            new LifecycleExecutionDefinition(kind),
            executionId ?? Guid.Parse("9d0a8d2a-df80-4e43-a038-985132485483"),
            startedAtUtc + (executionTimeout ?? TimeSpan.FromMinutes(5)),
            startedAtUtc);
    }

    public static UnityRequestPayload.BuildRun CreateExplicitBuildRunPayload (
        IpcBuildOutputLayout? outputLayout,
        bool development = false)
    {
        return new UnityRequestPayload.BuildRun(new IpcBuildRunRequest(
            RunId: RunIdTestValues.Build,
            InputKind: BuildProfileInputsKind.Explicit,
            BuildTarget: BuildTargetStableName.StandaloneLinux64,
            SceneSource: BuildProfileSceneSource.Explicit,
            ScenePaths: [new SceneAssetPath("Assets/Scenes/Main.unity")],
            Development: development,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: outputLayout,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: [UnityEditorMode.Batchmode],
            ProjectMutationMode: BuildProfileProjectMutationMode.Forbid,
            RunnerKind: BuildRunnerKind.BuildPipeline,
            ProfileDigest: Sha256Digest.Parse(new string('a', 64)),
            UnityBuildProfile: null,
            ProfilePath: null,
            RunnerMethod: null,
            RunnerArguments: new Dictionary<string, string>(StringComparer.Ordinal),
            RunnerEnvironmentVariables: [],
            RunnerEnvironmentSecrets: [],
            RunnerEnvironmentVariableValues: new Dictionary<string, string>(StringComparer.Ordinal),
            RunnerEnvironmentSecretValues: new Dictionary<string, string>(StringComparer.Ordinal)));
    }
}
