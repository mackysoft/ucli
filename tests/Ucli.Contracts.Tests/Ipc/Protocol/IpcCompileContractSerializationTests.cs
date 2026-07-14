using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcCompileContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string RunIdText = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid RunId = Guid.Parse(RunIdText);

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileResponse_WhenSummaryIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new IpcCompileResponse(null!));

        Assert.Equal("Summary", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcCompileRequest(RunId)
        {
            TimeoutMilliseconds = 10000,
        };
        var responsePayload = new IpcCompileResponse(CreateCompileSummary(RunId));

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasString("runId", RunIdText)
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(response)
            .HasProperty("summary", summary => summary
                .HasString("runId", RunIdText)
                .HasString("projectFingerprint", ProjectFingerprintText)
                .HasBoolean("completed", true)
                .HasProperty("scriptCompilation", scriptCompilation => scriptCompilation
                    .HasInt32("compileGenerationBefore", 12)
                    .HasInt32("compileGenerationAfter", 14)
                    .HasProperty("diagnostics", diagnostics => diagnostics
                        .HasInt32("errorCount", 1)
                        .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                            .HasString("kind", "compiler")
                            .HasString("code", "CS1002"))))
                .HasProperty("domainReload", domainReload => domainReload
                    .HasInt32("generationBefore", 7)
                    .HasInt32("generationAfter", 7))
                .HasProperty("lifecycle", lifecycle => lifecycle
                    .HasProperty("state", state => state
                        .HasProperty("generations", generations => generations
                            .HasInt32("compileGeneration", 14)
                            .HasInt32("domainReloadGeneration", 7)
                            .HasInt32("assetRefreshGeneration", 8)
                            .HasInt32("playModeGeneration", 9)))));
        Assert.False(response.TryGetProperty("summaryJsonPath", out _));
        Assert.False(response.TryGetProperty("diagnosticsJsonPath", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileProgressContracts_SerializeWithCamelCaseFields ()
    {
        var startedPayload = new CompileStartedEntry(
            RunId: RunId,
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            RequestedMode: "auto",
            ResolvedMode: "oneshot",
            SessionKind: "transientProbe",
            TimeoutMilliseconds: 10000);
        var refreshPayload = new CompileRefreshStartedEntry(
            RunId: RunId,
            RefreshOrigin: "assetDatabaseRefresh",
            ObservationSource: "hostDispatch");
        var recoveredPayload = new CompileRecoveredEntry(
            RunId: RunId,
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DispatchFailureCode: IpcTransportErrorCodes.IpcTimeout.Value,
            PollAttempts: 2);
        var diagnosticPayload = new CompileDiagnosticEntry(
            RunId: RunId,
            RefreshOrigin: "diagnosticsRead",
            PrimaryDiagnostic: new IpcPrimaryDiagnostic(
                Kind: "compiler",
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected"));
        var completedPayload = new CompileCompletedEntry(
            RunId: RunId,
            Verdict: "fail",
            ErrorCount: 1,
            WarningCount: 0,
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DiagnosticsJsonPath: "/tmp/ucli/compile/run-1/diagnostics.json");

        var started = IpcPayloadCodec.SerializeToElement(startedPayload);
        var refresh = IpcPayloadCodec.SerializeToElement(refreshPayload);
        var recovered = IpcPayloadCodec.SerializeToElement(recoveredPayload);
        var diagnostic = IpcPayloadCodec.SerializeToElement(diagnosticPayload);
        var completed = IpcPayloadCodec.SerializeToElement(completedPayload);

        Assert.Equal("compile.started", CompileProgressEventNames.Started);
        Assert.Equal("compile.refresh.started", CompileProgressEventNames.RefreshStarted);
        Assert.Equal("compile.recovered", CompileProgressEventNames.Recovered);
        Assert.Equal("compile.diagnostic", CompileProgressEventNames.Diagnostic);
        Assert.Equal("compile.completed", CompileProgressEventNames.Completed);
        JsonAssert.For(started)
            .HasString("runId", RunIdText)
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasString("requestedMode", "auto")
            .HasString("resolvedMode", "oneshot")
            .HasString("sessionKind", "transientProbe")
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(refresh)
            .HasString("runId", RunIdText)
            .HasString("refreshOrigin", "assetDatabaseRefresh")
            .HasString("observationSource", "hostDispatch");
        JsonAssert.For(recovered)
            .HasString("runId", RunIdText)
            .HasString("summaryJsonPath", "/tmp/ucli/compile/run-1/summary.json")
            .HasString("dispatchFailureCode", IpcTransportErrorCodes.IpcTimeout.Value)
            .HasInt32("pollAttempts", 2);
        JsonAssert.For(diagnostic)
            .HasString("runId", RunIdText)
            .HasString("refreshOrigin", "diagnosticsRead")
            .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002"));
        JsonAssert.For(completed)
            .HasString("runId", RunIdText)
            .HasString("verdict", "fail")
            .HasInt32("errorCount", 1)
            .HasInt32("warningCount", 0)
            .HasString("summaryJsonPath", "/tmp/ucli/compile/run-1/summary.json")
            .HasString("diagnosticsJsonPath", "/tmp/ucli/compile/run-1/diagnostics.json");
    }

    private static IpcCompileSummary CreateCompileSummary (Guid runId)
    {
        var primaryDiagnostic = new IpcPrimaryDiagnostic(
            Kind: "compiler",
            Code: "CS1002",
            File: "Assets/Broken.cs",
            Line: 4,
            Column: 16,
            Message: "; expected");
        return new IpcCompileSummary(
            RunId: runId,
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            Completed: true,
            StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00+00:00"),
            CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02+00:00"),
            Refresh: new IpcCompileSummary.RefreshEvidence(
                Origin: "assetDatabaseRefresh",
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00+00:00"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01+00:00"),
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: 1,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                State: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: IpcEditorLifecycleState.CompileFailed,
                    compileState: IpcCompileState.Failed,
                    generations: new IpcUnityGenerationSnapshot(
                        CompileGeneration: 14,
                        DomainReloadGeneration: 7,
                        AssetRefreshGeneration: 8,
                        PlayModeGeneration: 9),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02+00:00"),
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
