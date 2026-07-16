using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests;

internal static class ReadyCommandTestData
{
    private static readonly AssuranceVerifierId LifecycleVerifierId = new("ready.lifecycle");
    private static readonly AssuranceVerifierId ReadIndexVerifierId = new("ready.readIndex");

    public static ReadyExecutionOutput CreateOutput (
        AssuranceVerdict verdict = AssuranceVerdict.Pass)
    {
        var lifecycle = CreateLifecycle();
        var claimStatus = verdict == AssuranceVerdict.Pass
            ? AssuranceClaimStatus.Passed
            : AssuranceClaimStatus.Failed;
        return new ReadyExecutionOutput(
            Verdict: verdict,
            Project: ProjectIdentityInfoTestFactory.Create(
                projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>")),
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: LifecycleVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyExecution]),
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyExecution,
                    Status: claimStatus,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: LifecycleVerifierId,
                    Statement: "Unity is ready for execution.",
                    Subject: CreateSubject(
                        ReadyTarget.Execution,
                        AssuranceRequestedExecutionMode.Auto,
                        AssuranceResolvedExecutionMode.Oneshot,
                        AssuranceSessionKind.TransientProbe),
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKind.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence:
                    [
                        new ReadyEvidenceOutput(
                            Kind: "lifecycleSnapshot",
                            Data: lifecycle),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: ReadyTarget.Execution,
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
            SessionKind: AssuranceSessionKind.TransientProbe,
            TimeoutMilliseconds: 10000,
            Lifecycle: lifecycle,
            ReadIndex: null);
    }

    public static ReadyExecutionOutput CreateReadIndexOutput ()
    {
        var readIndex = new ReadyReadIndexOutput(ReadyReadIndexMode.AllowStale, CreateReadIndexArtifacts());
        return new ReadyExecutionOutput(
            Verdict: AssuranceVerdict.Pass,
            Project: ProjectIdentityInfoTestFactory.Create(
                projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>")),
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: ReadIndexVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyReadIndex]),
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyReadIndex,
                    Status: AssuranceClaimStatus.Passed,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: ReadIndexVerifierId,
                    Statement: "Unity is ready for readIndex.",
                    Subject: CreateSubject(
                        ReadyTarget.ReadIndex,
                        AssuranceRequestedExecutionMode.Auto,
                        AssuranceResolvedExecutionMode.NotApplicable,
                        AssuranceSessionKind.ArtifactOnly),
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKind.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence:
                    [
                        new ReadyEvidenceOutput(
                            Kind: "readIndexSummary",
                            Data: readIndex),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: ReadyTarget.ReadIndex,
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.NotApplicable,
            SessionKind: AssuranceSessionKind.ArtifactOnly,
            TimeoutMilliseconds: 10000,
            Lifecycle: null,
            ReadIndex: readIndex);
    }

    public static string[] GetReadIndexModeOptionSpellings ()
    {
        return
        [
            "--readIndexMode",
            "--read-index-mode",
        ];
    }

    public static StartupFailureDetail CreateStartupFailureDetail ()
    {
        return new StartupFailureDetail(
            Startup: new DaemonStartupObservationOutput(
                StartupStatus: DaemonStartupStatus.Blocked,
                StartupBlockingReason: DaemonStartupBlockingReason.Compile,
                LaunchAttemptId: null,
                EditorMode: DaemonEditorMode.Batchmode,
                OwnerKind: DaemonSessionOwnerKind.Cli,
                CanShutdownProcess: true,
                ProcessId: 1234,
                StartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                ElapsedMilliseconds: null,
                ProcessAction: DaemonStartupProcessAction.Terminated,
                ProcessTermination: null,
                ArtifactPath: null,
                RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix),
            Diagnosis: new DaemonDiagnosisOutput(
                Reason: DaemonDiagnosisReason.UnityScriptCompilationFailed,
                Message: "Unity startup is blocked.",
                ReportedBy: DaemonDiagnosisReportedBy.Cli,
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                    Code: "CS0246",
                    File: "Assets/Scripts/Broken.cs",
                    Line: 10,
                    Column: 5,
                    Message: "error CS0246")),
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            SafeToRetryImmediately: false);
    }

    public static JsonGoldenFileNormalization CreateReadyGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>");
    }

    private static ReadyLifecycleOutput CreateLifecycle ()
    {
        return new ReadyLifecycleOutput(
            ServerVersion: "0.5.0",
            UnityVersion: "6000.1.4f1",
            EditorMode: DaemonEditorMode.Batchmode,
            LifecycleState: IpcEditorLifecycleState.Ready,
            BlockingReason: null,
            CompileState: IpcCompileState.Ready,
            Generations: new IpcUnityGenerationSnapshot(12, 7, 0, 2),
            CanAcceptExecutionRequests: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            ActionRequired: null,
            PrimaryDiagnostic: null,
            PlayMode: new IpcPlayModeSnapshot(
                State: IpcPlayModeState.Stopped,
                Transition: IpcPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false));
    }

    private static Dictionary<string, object?> CreateSubject (
        ReadyTarget target,
        AssuranceRequestedExecutionMode requestedMode,
        AssuranceResolvedExecutionMode resolvedMode,
        AssuranceSessionKind sessionKind)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = "unityReady",
            ["target"] = ContractLiteralCodec.ToValue(target),
            ["requestedMode"] = ContractLiteralCodec.ToValue(requestedMode),
            ["resolvedMode"] = ContractLiteralCodec.ToValue(resolvedMode),
            ["sessionKind"] = sessionKind,
        };
    }

    private static IReadOnlyList<ReadyReadIndexArtifactOutput> CreateReadIndexArtifacts ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-05-17T00:00:00Z");
        return
        [
            CreateCatalogArtifact(ReadyReadIndexArtifactName.OpsCatalog, generatedAtUtc),
            CreateCatalogArtifact(ReadyReadIndexArtifactName.AssetSearchLookup, generatedAtUtc),
            CreateCatalogArtifact(ReadyReadIndexArtifactName.GuidPathLookup, generatedAtUtc),
        ];
    }

    private static ReadyReadIndexArtifactOutput CreateCatalogArtifact (
        ReadyReadIndexArtifactName name,
        DateTimeOffset generatedAtUtc)
    {
        return ReadyReadIndexArtifactOutput.Available(
            name,
            required: true,
            freshness: IndexFreshness.Fresh,
            Sha256DigestTestFactory.Compute("source-hash"),
            generatedAtUtc);
    }
}
