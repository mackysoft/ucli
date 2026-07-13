using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests;

internal static class ReadyCommandTestData
{
    public static ReadyExecutionOutput CreateOutput (
        string verdict = ReadyVerdictValues.Pass)
    {
        var lifecycle = CreateLifecycle();
        var claimStatus = string.Equals(verdict, ReadyVerdictValues.Pass, StringComparison.Ordinal)
            ? ReadyClaimStatusValues.Passed
            : ReadyClaimStatusValues.Failed;
        return new ReadyExecutionOutput(
            Verdict: verdict,
            Project: ProjectIdentityInfoTestFactory.Create(projectPath: "<projectPath>", projectFingerprint: "<projectFingerprint>"),
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: "ready.lifecycle",
                    Kind: "ready.lifecycle",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyExecution],
                    Effects: []),
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyExecution,
                    Status: claimStatus,
                    Coverage: ReadyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "ready.lifecycle",
                    Statement: "Unity is ready for execution.",
                    Subject: CreateSubject("execution", "auto", "oneshot", "transientProbe"),
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKindValues.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence:
                    [
                        new ReadyEvidenceOutput(
                            Kind: "lifecycleSnapshot",
                            Data: lifecycle),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, ReadyReportOutput>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: "execution",
            RequestedMode: "auto",
            ResolvedMode: "oneshot",
            SessionKind: "transientProbe",
            TimeoutMilliseconds: 10000,
            Lifecycle: lifecycle,
            ReadIndex: null);
    }

    public static ReadyExecutionOutput CreateReadIndexOutput ()
    {
        var readIndex = new ReadyReadIndexOutput("allowStale", CreateReadIndexArtifacts());
        return new ReadyExecutionOutput(
            Verdict: ReadyVerdictValues.Pass,
            Project: ProjectIdentityInfoTestFactory.Create(projectPath: "<projectPath>", projectFingerprint: "<projectFingerprint>"),
            Verifiers:
            [
                new ReadyVerifierOutput(
                    Id: "ready.readIndex",
                    Kind: "ready.readIndex",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyReadIndex],
                    Effects: []),
            ],
            Claims:
            [
                new ReadyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyReadIndex,
                    Status: ReadyClaimStatusValues.Passed,
                    Coverage: ReadyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "ready.readIndex",
                    Statement: "Unity is ready for readIndex.",
                    Subject: CreateSubject("readIndex", "auto", AssuranceExecutionModeCodec.NotApplicable, AssuranceSessionKindValues.ArtifactOnly),
                    Validity: new ReadyClaimValidityOutput(
                        ReadyValidityKindValues.ProbeOnly,
                        GuaranteesReusableSession: false),
                    Evidence:
                    [
                        new ReadyEvidenceOutput(
                            Kind: "readIndexSummary",
                            Data: readIndex),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, ReadyReportOutput>(StringComparer.Ordinal),
            ResidualRisks: [],
            Target: "readIndex",
            RequestedMode: "auto",
            ResolvedMode: AssuranceExecutionModeCodec.NotApplicable,
            SessionKind: AssuranceSessionKindValues.ArtifactOnly,
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
                Reason: "unityScriptCompilationFailed",
                Message: "Unity startup is blocked.",
                ReportedBy: "cli",
                IsInferred: true,
                UpdatedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:06+00:00"),
                ProcessId: 1234,
                EditorInstancePath: null,
                ProcessStartedAtUtc: DateTimeOffset.Parse("2026-03-12T04:05:01+00:00"),
                UnityLogPath: "/repo/.ucli/local/logs/unity.log",
                StartupPhase: DaemonDiagnosisStartupPhase.ScriptCompilation,
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: new DaemonPrimaryDiagnosticOutput(
                    Kind: "compiler",
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
        string target,
        string requestedMode,
        string resolvedMode,
        string sessionKind)
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = "unityReady",
            ["target"] = target,
            ["requestedMode"] = requestedMode,
            ["resolvedMode"] = resolvedMode,
            ["sessionKind"] = sessionKind,
        };
    }

    private static IReadOnlyList<ReadyReadIndexArtifactOutput> CreateReadIndexArtifacts ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-05-17T00:00:00Z");
        return
        [
            CreateCatalogArtifact("ops.catalog", generatedAtUtc),
            CreateCatalogArtifact("asset-search.lookup", generatedAtUtc),
            CreateCatalogArtifact("guid-path.lookup", generatedAtUtc),
        ];
    }

    private static ReadyReadIndexArtifactOutput CreateCatalogArtifact (
        string name,
        DateTimeOffset generatedAtUtc)
    {
        return new ReadyReadIndexArtifactOutput(
            Name: name,
            Status: ReadyReadIndexArtifactStatusValues.Available,
            Freshness: "fresh",
            SourceInputsHash: "source-hash",
            GeneratedAtUtc: generatedAtUtc);
    }
}
