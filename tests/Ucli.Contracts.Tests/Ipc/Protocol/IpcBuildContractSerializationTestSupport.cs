using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

internal static class IpcBuildContractSerializationTestSupport
{
    public static readonly ProjectFingerprint TestProjectFingerprint =
        new("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    public static UnityEditorObservation CreateBuildLifecycleSnapshot (
        long generation,
        bool canAcceptExecutionRequests)
    {
        return new UnityEditorObservation(
            serverVersion: "0.5.0",
            unityVersion: "6000.1.4f1",
            projectFingerprint: TestProjectFingerprint,
            state: new UnityEditorStateSnapshot(
                editorMode: UnityEditorMode.Batchmode,
                lifecycleState: canAcceptExecutionRequests
                    ? UnityEditorLifecycleState.Ready
                    : UnityEditorLifecycleState.Busy,
                compileState: UnityEditorCompileState.Ready,
                generations: new UnityEditorGenerationSnapshot(
                    CompileGeneration: generation,
                    DomainReloadGeneration: generation,
                    AssetRefreshGeneration: generation,
                    PlayModeGeneration: generation),
                playMode: new UnityEditorPlayModeSnapshot(
                    State: UnityEditorPlayModeState.Stopped,
                    Transition: UnityEditorPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.Parse("2026-06-12T00:00:00+00:00"),
            actionRequired: null,
            primaryDiagnostic: null);
    }

    public static IpcBuildProjectMutationAudit CreateProjectMutationAudit ()
    {
        return new IpcBuildProjectMutationAudit(
            Mode: BuildProfileProjectMutationMode.Forbid,
            Coverage: IpcBuildProjectMutationAuditCoverage.Full,
            Mutated: true,
            BeforeDigest: Sha256Digest.Parse(new string('a', 64)),
            AfterDigest: Sha256Digest.Parse(new string('b', 64)),
            Items:
            [
                new IpcBuildProjectMutationAuditItem(
                    Path: new ProjectMutationAuditPath("Assets/Generated.asset"),
                    ChangeKind: IpcBuildProjectMutationChangeKind.Added,
                    BeforeSha256: null,
                    AfterSha256: Sha256Digest.Parse(new string('b', 64))),
            ]);
    }
}
