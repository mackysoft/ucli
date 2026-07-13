using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Common.IpcBuildContractSerializationTestSupport;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcBuildPreconditionContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcBuildPreconditionContracts_SerializeWithCamelCaseFields ()
    {
        var dirtyState = IpcPayloadCodec.SerializeToElement(
            new IpcBuildDirtyState(
                Checked: true,
                Dirty: true,
                Coverage: ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full),
                Items:
                [
                    new IpcBuildDirtyStateItem(
                        ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene),
                        "Assets/Scenes/Main.unity"),
                ]));
        var inputProbe = IpcPayloadCodec.SerializeToElement(
            new IpcBuildInputProbe(
                InputKind: ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit),
                BuildTarget: "standaloneLinux64",
                UnityBuildTarget: "StandaloneLinux64",
                UnityBuildTargetGroup: "Standalone",
                SceneSource: ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit),
                Scenes: ["Assets/Scenes/Main.unity"],
                BuildOptions: "Development"));
        var lifecycle = IpcPayloadCodec.SerializeToElement(
            new IpcBuildLifecycleSnapshot(
                ServerVersion: "1.2.3",
                EditorMode: "batchmode",
                UnityVersion: "6000.0.0f1",
                ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
                LifecycleState: IpcEditorLifecycleStateCodec.CompileFailed,
                BlockingReason: IpcEditorBlockingReasonCodec.CompileFailed,
                CompileState: IpcCompileStateCodec.Failed,
                CompileGeneration: "compile-1",
                DomainReloadGeneration: "domain-1",
                CanAcceptExecutionRequests: false,
                ObservedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                ActionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
                PrimaryDiagnostic: new IpcPrimaryDiagnostic(
                    Kind: "compiler",
                    Code: "CS1002",
                    File: "Assets/Broken.cs",
                    Line: 4,
                    Column: 16,
                    Message: "; expected"),
                PlayMode: new IpcPlayModeSnapshot(
                    State: "stopped",
                    Transition: "none",
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false,
                    Generation: "play-1"),
                AssetRefreshGeneration: "asset-1"));

        JsonAssert.For(dirtyState)
            .HasBoolean("checked", true)
            .HasBoolean("dirty", true)
            .HasString("coverage", ContractLiteralCodec.ToValue(IpcBuildDirtyStateCoverage.Full))
            .HasArrayLength("items", 1)
            .HasProperty("items", 0, item => item
                .HasString("kind", ContractLiteralCodec.ToValue(IpcBuildDirtyStateItemKind.Scene))
                .HasString("path", "Assets/Scenes/Main.unity"));
        JsonAssert.For(inputProbe)
            .HasString("inputKind", ContractLiteralCodec.ToValue(BuildProfileInputsKind.Explicit))
            .HasString("buildTarget", "standaloneLinux64")
            .HasString("unityBuildTarget", "StandaloneLinux64")
            .HasString("unityBuildTargetGroup", "Standalone")
            .HasString("sceneSource", ContractLiteralCodec.ToValue(BuildProfileSceneSource.Explicit))
            .HasArrayLength("scenes", 1)
            .HasProperty("scenes", 0, scene => scene
                .HasString("Assets/Scenes/Main.unity"))
            .HasString("buildOptions", "Development");
        JsonAssert.For(lifecycle)
            .HasString("serverVersion", "1.2.3")
            .HasString("editorMode", "batchmode")
            .HasString("unityVersion", "6000.0.0f1")
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasString("lifecycleState", IpcEditorLifecycleStateCodec.CompileFailed)
            .HasString("blockingReason", IpcEditorBlockingReasonCodec.CompileFailed)
            .HasString("compileState", IpcCompileStateCodec.Failed)
            .HasString("compileGeneration", "compile-1")
            .HasString("domainReloadGeneration", "domain-1")
            .HasString("assetRefreshGeneration", "asset-1")
            .HasBoolean("canAcceptExecutionRequests", false)
            .HasString("observedAtUtc", "2026-06-12T00:00:00+00:00")
            .HasString("actionRequired", DaemonDiagnosisActionRequiredValues.FixCompileErrors)
            .HasProperty("primaryDiagnostic", diagnostic => diagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002")
                .HasString("file", "Assets/Broken.cs")
                .HasInt32("line", 4)
                .HasInt32("column", 16)
                .HasString("message", "; expected"))
            .HasProperty("playMode", playMode => playMode
                .HasString("state", "stopped")
                .HasString("transition", "none")
                .HasBoolean("isPlaying", false)
                .HasBoolean("isPlayingOrWillChangePlaymode", false)
                .HasString("generation", "play-1"));
    }
}
