namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

internal static class UnityIpcRequestBuilderTestSupport
{
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
            AllowedEditorModes: [DaemonEditorMode.Batchmode],
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
