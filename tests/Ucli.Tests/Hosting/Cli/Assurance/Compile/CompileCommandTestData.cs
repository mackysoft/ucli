using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests;

internal static class CompileCommandTestData
{
    public static JsonGoldenFileNormalization CreateGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>");
    }

    public static CompileCompletedEntry CreateCompletedEntry ()
    {
        return new CompileCompletedEntry(
            RunId: "run-1",
            Verdict: "pass",
            ErrorCount: 0,
            WarningCount: 0,
            SummaryJsonPath: "/tmp/ucli/compile/run-1/summary.json",
            DiagnosticsJsonPath: "/tmp/ucli/compile/run-1/diagnostics.json");
    }

    public static CompileExecutionOutput CreateOutput (int errorCount = 0)
    {
        var compile = CreateCompileOutput(errorCount);
        var compileStatus = errorCount == 0 ? CompileClaimStatusValues.Passed : CompileClaimStatusValues.Failed;
        var lifecycleStatus = errorCount == 0 ? CompileClaimStatusValues.Passed : CompileClaimStatusValues.Failed;
        return new CompileExecutionOutput(
            Verdict: errorCount == 0 ? CompileVerdictValues.Pass : CompileVerdictValues.Fail,
            Project: ProjectIdentityInfoTestFactory.Create(
                projectPath: "<projectPath>",
                projectFingerprint: "<projectFingerprint>"),
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: "compile",
                    Kind: "compile",
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.All.Select(static code => code.Value).ToArray(),
                    Effects: CompileEffectValues.All,
                    ReportRef: "compile.summary"),
            ],
            Claims:
            [
                CreateClaim(
                    CompileClaimCodes.UnityCompileNoErrors,
                    compileStatus,
                    "Unity script compilation completed without compiler errors.",
                    "unityCompile",
                    new CompileEvidenceOutput(CompileEffectValues.ScriptCompilation, "compile.diagnostics", compile.ScriptCompilation)),
                CreateClaim(
                    CompileClaimCodes.UnityDomainReloadSettled,
                    CompileClaimStatusValues.Passed,
                    "Unity domain reload reached a settled state after compile observation.",
                    "unityDomainReload",
                    new CompileEvidenceOutput(CompileEffectValues.DomainReload, Data: compile.DomainReload)),
                CreateClaim(
                    CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                    lifecycleStatus,
                    "Unity lifecycle is ready after compile observation.",
                    "unityLifecycle",
                    new CompileEvidenceOutput("lifecycleSnapshot", Data: compile.Lifecycle)),
            ],
            Reports: new Dictionary<string, CompileReportOutput>(StringComparer.Ordinal)
            {
                ["compile.summary"] = new CompileReportOutput("/tmp/ucli/compile/summary.json"),
                ["compile.diagnostics"] = new CompileReportOutput("/tmp/ucli/compile/diagnostics.json"),
            },
            ResidualRisks: [],
            RequestedMode: AssuranceExecutionModeCodec.Auto,
            ResolvedMode: AssuranceExecutionModeCodec.Oneshot,
            SessionKind: AssuranceSessionKindValues.TransientProbe,
            TimeoutMilliseconds: 10000,
            Compile: compile);
    }

    private static CompileClaimOutput CreateClaim (
        string id,
        string status,
        string statement,
        string subjectKind,
        CompileEvidenceOutput evidence)
    {
        return new CompileClaimOutput(
            Id: id,
            Status: status,
            Coverage: CompileCoverageValues.Full,
            Required: true,
            VerifierRef: "compile",
            Statement: statement,
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = subjectKind,
                ["runId"] = "20260517_000000Z_abcdef12",
            },
            Evidence: [evidence],
            ResidualRisks: []);
    }

    private static CompileOutput CreateCompileOutput (int errorCount)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new CompilePrimaryDiagnosticOutput(
                Kind: "compiler",
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new CompileOutput(
            RunId: "20260517_000000Z_abcdef12",
            Refresh: new CompileRefreshOutput(
                Origin: CompileEffectValues.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            ScriptCompilation: new CompileScriptCompilationOutput(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new CompileDiagnosticsOutput(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new CompileDomainReloadOutput(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            Lifecycle: new CompileLifecycleOutput(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                EditorMode: DaemonEditorMode.Batchmode,
                LifecycleState: canAcceptExecutionRequests
                    ? IpcEditorLifecycleState.Ready
                    : IpcEditorLifecycleState.CompileFailed,
                BlockingReason: canAcceptExecutionRequests
                    ? null
                    : IpcEditorBlockingReason.CompileFailed,
                CompileState: canAcceptExecutionRequests
                    ? IpcCompileState.Ready
                    : IpcCompileState.Failed,
                Generations: new IpcUnityGenerationSnapshot(14, 7, 0, 0),
                CanAcceptExecutionRequests: canAcceptExecutionRequests,
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"),
                ActionRequired: canAcceptExecutionRequests ? null : "fixCompileErrors",
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
