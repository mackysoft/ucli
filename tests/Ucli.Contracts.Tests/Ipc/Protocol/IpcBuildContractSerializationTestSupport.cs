using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

internal static class IpcBuildContractSerializationTestSupport
{
    public static IpcBuildLifecycleSnapshot CreateBuildLifecycleSnapshot (
        string generationSuffix,
        bool canAcceptExecutionRequests)
    {
        return new IpcBuildLifecycleSnapshot(
            ServerVersion: "0.5.0",
            EditorMode: "batchmode",
            UnityVersion: "6000.1.4f1",
            ProjectFingerprint: "project-fingerprint",
            LifecycleState: "ready",
            BlockingReason: "none",
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
            AssetRefreshGeneration: $"asset-{generationSuffix}");
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
