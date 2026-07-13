using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

internal static class IpcBuildContractSerializationTestSupport
{
    public static readonly ProjectFingerprint TestProjectFingerprint =
        new("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    public static IpcUnityEditorObservation CreateBuildLifecycleSnapshot (
        long generation,
        bool canAcceptExecutionRequests)
    {
        return new IpcUnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: TestProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: DaemonEditorMode.Batchmode,
                lifecycleState: canAcceptExecutionRequests
                    ? IpcEditorLifecycleState.Ready
                    : IpcEditorLifecycleState.Busy,
                compileState: IpcCompileState.Ready,
                generations: new IpcUnityGenerationSnapshot(
                    CompileGeneration: generation,
                    DomainReloadGeneration: generation,
                    AssetRefreshGeneration: generation,
                    PlayModeGeneration: generation),
                playMode: new IpcPlayModeSnapshot(
                    State: IpcPlayModeState.Stopped,
                    Transition: IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static IpcBuildProjectMutationAudit CreateProjectMutationAudit ()
    {
        return new IpcBuildProjectMutationAudit(
            Mode: "forbid",
            Coverage: ContractLiteralCodec.ToValue(IpcBuildProjectMutationAuditCoverage.Full),
            Mutated: true,
            BeforeDigest: new string('a', 64),
            AfterDigest: new string('b', 64),
            Items:
            [
                new IpcBuildProjectMutationAuditItem(
                    Path: "Assets/Generated.asset",
                    ChangeKind: ContractLiteralCodec.ToValue(IpcBuildProjectMutationChangeKind.Added),
                    BeforeSha256: null,
                    AfterSha256: new string('b', 64)),
            ]);
    }
}
