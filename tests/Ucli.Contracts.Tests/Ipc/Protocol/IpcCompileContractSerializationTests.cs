using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcCompileContractSerializationTests
{
    private const string ProjectFingerprintText = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string ExecutionIdText = "11111111-2222-3333-4444-555555555555";
    private static readonly Guid ExecutionId = Guid.Parse(ExecutionIdText);

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileResponse_WhenResultIsNull_ThrowsArgumentNullException ()
    {
        var reference = LifecycleExecutionContractTestFactory.CreateReference(
            LifecycleExecutionKind.Compile,
            ExecutionLifecycle.Terminal,
            LifecycleExecutionState.Completed);

        var exception = Assert.Throws<ArgumentNullException>(
            () => new IpcCompileResponse(reference, null!));

        Assert.Equal("result", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcCompileContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcCompileRequest(
            LifecycleExecutionContractTestFactory.CreateStart(
                LifecycleExecutionKind.Compile));
        var responsePayload = new IpcCompileResponse(
            LifecycleExecutionContractTestFactory.CreateReference(
                LifecycleExecutionKind.Compile,
                ExecutionLifecycle.Terminal,
                LifecycleExecutionState.Completed),
            CreateCompileResult());

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        Assert.True(
            IpcPayloadCodec.TryDeserialize(
                request,
                out IpcCompileRequest _,
                out var requestReadError),
            requestReadError.Message);
        Assert.True(
            IpcPayloadCodec.TryDeserialize(
                response,
                out IpcCompileResponse _,
                out var responseReadError),
            responseReadError.Message);
        JsonAssert.For(request)
            .HasProperty("start", start => start
                .HasProperty("lifecycleExecutionRef", reference => reference
                    .HasString("kind", "compile")
                    .HasString("lifecycle", "active")));
        Assert.False(request.TryGetProperty("timeoutMilliseconds", out _));
        JsonAssert.For(response)
            .HasProperty("lifecycleExecutionRef", reference => reference
                .HasString("kind", "compile")
                .HasString("lifecycle", "terminal")
                .HasString("state", "completed"))
            .HasProperty("result", result => result
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
            ExecutionId: ExecutionId,
            ProjectFingerprint: new ProjectFingerprint(ProjectFingerprintText),
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
            SessionKind: AssuranceSessionKind.TransientProbe,
            TimeoutMilliseconds: 10000);
        var refreshPayload = new CompileRefreshStartedEntry(
            ExecutionId: ExecutionId,
            RefreshOrigin: CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
            ObservationSource: "hostDispatch");
        var recoveredPayload = new CompileRecoveredEntry(
            ExecutionId: ExecutionId);
        var diagnosticPayload = new CompileDiagnosticEntry(
            ExecutionId: ExecutionId,
            RefreshOrigin: CompileLifecycleRefreshOrigin.DiagnosticsRead,
            PrimaryDiagnostic: new UnityEditorPrimaryDiagnostic(
                Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected"));
        var completedPayload = new CompileCompletedEntry(
            ExecutionId: ExecutionId,
            Verdict: Verdict.Fail,
            ErrorCount: 1,
            WarningCount: 0);

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
            .HasString("executionId", ExecutionIdText)
            .HasString("projectFingerprint", ProjectFingerprintText)
            .HasString("requestedMode", "auto")
            .HasString("resolvedMode", "oneshot")
            .HasString("sessionKind", "transientProbe")
            .HasInt32("timeoutMilliseconds", 10000);
        JsonAssert.For(refresh)
            .HasString("executionId", ExecutionIdText)
            .HasString("refreshOrigin", "assetDatabaseRefresh")
            .HasString("observationSource", "hostDispatch");
        JsonAssert.For(recovered)
            .HasString("executionId", ExecutionIdText);
        JsonAssert.For(diagnostic)
            .HasString("executionId", ExecutionIdText)
            .HasString("refreshOrigin", "diagnosticsRead")
            .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                .HasString("kind", "compiler")
                .HasString("code", "CS1002"));
        JsonAssert.For(completed)
            .HasString("executionId", ExecutionIdText)
            .HasString("verdict", TextVocabulary.GetText(Verdict.Fail))
            .HasInt32("errorCount", 1)
            .HasInt32("warningCount", 0);
    }

    private static CompileLifecycleResult CreateCompileResult ()
    {
        var primaryDiagnostic = new UnityEditorPrimaryDiagnostic(
            Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
            Code: "CS1002",
            File: "Assets/Broken.cs",
            Line: 4,
            Column: 16,
            Message: "; expected");
        return new CompileLifecycleResult(
            Refresh: new CompileLifecycleResult.RefreshEvidence(
                Origin: CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00+00:00"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:01+00:00"),
                Completed: true),
            ScriptCompilation: new CompileLifecycleResult.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new CompileLifecycleResult.DiagnosticsEvidence(
                    ErrorCount: 1,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new CompileLifecycleResult.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            Lifecycle: new CompileLifecycleResult.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                State: new UnityEditorStateSnapshot(
                    editorMode: UnityEditorMode.Batchmode,
                    lifecycleState: UnityEditorLifecycleState.CompileFailed,
                    compileState: UnityEditorCompileState.Failed,
                    generations: new UnityEditorGenerationSnapshot(
                        CompileGeneration: 14,
                        DomainReloadGeneration: 7,
                        AssetRefreshGeneration: 8,
                        PlayModeGeneration: 9),
                    playMode: new UnityEditorPlayModeSnapshot(
                        State: UnityEditorPlayModeState.Stopped,
                        Transition: UnityEditorPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02+00:00"),
                ActionRequired: UnityEditorActionRequired.FixCompileErrors,
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
