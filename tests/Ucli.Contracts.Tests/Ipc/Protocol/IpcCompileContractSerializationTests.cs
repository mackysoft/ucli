using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcCompileContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcCompileRequest("run-1")
        {
            TimeoutMilliseconds = 10000,
        };
        var responsePayload = new IpcCompileResponse(
            RunId: "run-1",
            Summary: CreateCompileSummary());

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasString("runId", "run-1")
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(response)
            .HasString("runId", "run-1")
            .HasProperty("summary", summary => summary
                .HasString("runId", "run-1")
                .HasString("projectFingerprint", "project-fingerprint")
                .HasBoolean("completed", true)
                .HasProperty("scriptCompilation", scriptCompilation => scriptCompilation
                    .HasProperty("diagnostics", diagnostics => diagnostics
                        .HasInt32("errorCount", 1)
                        .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                            .HasString("kind", "compiler")
                            .HasString("code", "CS1002")))));
        Assert.False(response.TryGetProperty("summaryJsonPath", out _));
        Assert.False(response.TryGetProperty("diagnosticsJsonPath", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileProgressContracts_SerializeWithCamelCaseFields ()
    {
        var startedPayload = new CompileStartedEntry(
            RunId: "run-1",
            ProjectFingerprint: "project-fingerprint",
            RequestedMode: "auto",
            ResolvedMode: "oneshot",
            SessionKind: "transientProbe",
            TimeoutMilliseconds: 10000);
        var refreshPayload = new CompileRefreshStartedEntry(
            RunId: "run-1",
            RefreshOrigin: "assetDatabaseRefresh",
            ObservationSource: "hostDispatch");
        var recoveredPayload = new CompileRecoveredEntry(
            RunId: "run-1",
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DispatchFailureCode: IpcTransportErrorCodes.IpcTimeout.Value,
            PollAttempts: 2);
        var diagnosticPayload = new CompileDiagnosticEntry(
            RunId: "run-1",
            RefreshOrigin: "diagnosticsRead",
            PrimaryDiagnostic: new IpcPrimaryDiagnostic(
                Kind: "compiler",
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected"));
        var completedPayload = new CompileCompletedEntry(
            RunId: "run-1",
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
            .HasString("runId", "run-1")
            .HasString("projectFingerprint", "project-fingerprint")
            .HasString("requestedMode", "auto")
            .HasString("resolvedMode", "oneshot")
            .HasString("sessionKind", "transientProbe")
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(refresh)
            .HasString("runId", "run-1")
            .HasString("refreshOrigin", "assetDatabaseRefresh")
            .HasString("observationSource", "hostDispatch");
        JsonAssert.For(recovered)
            .HasString("runId", "run-1")
            .HasString("summaryJsonPath", "/tmp/ucli/compile/run-1/summary.json")
            .HasString("dispatchFailureCode", IpcTransportErrorCodes.IpcTimeout.Value)
            .HasInt32("pollAttempts", 2);
        JsonAssert.For(diagnostic)
            .HasString("runId", "run-1")
            .HasString("refreshOrigin", "diagnosticsRead")
            .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002"));
        JsonAssert.For(completed)
            .HasString("runId", "run-1")
            .HasString("verdict", "fail")
            .HasInt32("errorCount", 1)
            .HasInt32("warningCount", 0)
            .HasString("summaryJsonPath", "/tmp/ucli/compile/run-1/summary.json")
            .HasString("diagnosticsJsonPath", "/tmp/ucli/compile/run-1/diagnostics.json");
    }

    private static IpcCompileSummary CreateCompileSummary ()
    {
        var primaryDiagnostic = new IpcPrimaryDiagnostic(
            Kind: "compiler",
            Code: "CS1002",
            File: "Assets/Broken.cs",
            Line: 4,
            Column: 16,
            Message: "; expected");
        return new IpcCompileSummary(
            RunId: "run-1",
            ProjectFingerprint: "project-fingerprint",
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
                CompileGenerationBefore: "12",
                CompileGenerationAfter: "14",
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: 1,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: "7",
                GenerationAfter: "7",
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                EditorMode: "batchmode",
                LifecycleState: "compileFailed",
                BlockingReason: "compileFailed",
                CompileState: "failed",
                CompileGeneration: "14",
                DomainReloadGeneration: "7",
                CanAcceptExecutionRequests: false,
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02+00:00"),
                ActionRequired: "fixCompileErrors",
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
