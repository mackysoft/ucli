using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

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
            new IpcUnityEditorObservation(
                serverVersion: "1.2.3",
                unityVersion: "6000.0.0f1",
                projectFingerprint: "project-fingerprint",
                state: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: IpcEditorLifecycleState.CompileFailed,
                    compileState: IpcCompileState.Failed,
                    generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: 1,
                        DomainReloadGeneration: 2,
                        AssetRefreshGeneration: 3,
                        PlayModeGeneration: 4),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                observedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
                actionRequired: DaemonDiagnosisActionRequiredValues.FixCompileErrors,
                primaryDiagnostic: new IpcPrimaryDiagnostic(
                    Kind: "compiler",
                    Code: "CS1002",
                    File: "Assets/Broken.cs",
                    Line: 4,
                    Column: 16,
                    Message: "; expected")));

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
            .HasString("unityVersion", "6000.0.0f1")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasString("observedAtUtc", "2026-06-12T00:00:00+00:00")
            .HasString("actionRequired", DaemonDiagnosisActionRequiredValues.FixCompileErrors)
            .HasProperty("primaryDiagnostic", diagnostic => diagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002")
                .HasString("file", "Assets/Broken.cs")
                .HasInt32("line", 4)
                .HasInt32("column", 16)
                .HasString("message", "; expected"))
            .HasProperty("state", state => state
                .HasString("editorMode", "batchmode")
                .HasString("lifecycleState", ContractLiteralCodec.ToValue(IpcEditorLifecycleState.CompileFailed))
                .HasString("compileState", ContractLiteralCodec.ToValue(IpcCompileState.Failed))
                .HasProperty("generations", generations => generations
                    .HasInt32("compileGeneration", 1)
                    .HasInt32("domainReloadGeneration", 2)
                    .HasInt32("assetRefreshGeneration", 3)
                    .HasInt32("playModeGeneration", 4))
                .HasProperty("playMode", playMode => playMode
                    .HasString("state", "stopped")
                    .HasString("transition", "none")
                    .HasBoolean("isPlaying", false)
                    .HasBoolean("isPlayingOrWillChangePlaymode", false)));
    }
}
