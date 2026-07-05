namespace MackySoft.Ucli.Tests.Ipc;

using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

internal static class UnityIpcRequestBuilderTestSupport
{
    public static UnityRequestPayload.BuildRun CreateExplicitBuildRunPayload (
        IpcBuildOutputLayout? outputLayout,
        bool development = false,
        string runnerKind = "buildPipeline")
    {
        return new UnityRequestPayload.BuildRun(
            RunId: "build-run-1",
            InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
            BuildTarget: "standaloneLinux64",
            UnityBuildTarget: "StandaloneLinux64",
            SceneSource: "explicit",
            ScenePaths: ["Assets/Scenes/Main.unity"],
            Development: development,
            OutputPath: "/tmp/ucli/output",
            OutputLayout: outputLayout,
            BuildReportPath: "/tmp/ucli/build-report.json",
            BuildLogPath: "/tmp/ucli/build.log",
            AllowedEditorModes: ["batchmode"],
            ProjectMutationMode: "forbid",
            RunnerKind: runnerKind);
    }
}
