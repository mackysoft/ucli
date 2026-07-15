using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Tests;

internal static class CompileCommandTestData
{
    private static readonly AssuranceVerifierId CompileVerifierId = new("compile");

    public static JsonGoldenFileNormalization CreateGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>");
    }

    public static CompileCompletedEntry CreateCompletedEntry ()
    {
        return new CompileCompletedEntry(
            RunId: RunIdTestValues.Compile,
            Verdict: AssuranceVerdict.Pass,
            ErrorCount: 0,
            WarningCount: 0,
            SummaryJsonPath: $"/tmp/ucli/compile/{RunIdTestValues.CompileText}/summary.json",
            DiagnosticsJsonPath: $"/tmp/ucli/compile/{RunIdTestValues.CompileText}/diagnostics.json");
    }

    public static CompileExecutionOutput CreateOutput (int errorCount = 0)
    {
        var compile = CreateCompileOutput(errorCount);
        var compileStatus = errorCount == 0 ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed;
        var lifecycleStatus = errorCount == 0 ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed;
        return new CompileExecutionOutput(
            Verdict: errorCount == 0 ? AssuranceVerdict.Pass : AssuranceVerdict.Fail,
            Project: ProjectIdentityInfoTestFactory.Create(
                projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>")),
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: CompileVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.All,
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: "compile.summary"),
            ],
            Claims:
            [
                CreateClaim(
                    CompileClaimCodes.UnityCompileNoErrors,
                    compileStatus,
                    "Unity script compilation completed without compiler errors.",
                    "unityCompile",
                    new CompileEvidenceOutput(CompileEvidenceKind.ScriptCompilation, "compile.diagnostics", compile.ScriptCompilation)),
                CreateClaim(
                    CompileClaimCodes.UnityDomainReloadSettled,
                    AssuranceClaimStatus.Passed,
                    "Unity domain reload reached a settled state after compile observation.",
                    "unityDomainReload",
                    new CompileEvidenceOutput(CompileEvidenceKind.DomainReload, EvidenceRef: null, Data: compile.DomainReload)),
                CreateClaim(
                    CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                    lifecycleStatus,
                    "Unity lifecycle is ready after compile observation.",
                    "unityLifecycle",
                    new CompileEvidenceOutput(CompileEvidenceKind.LifecycleSnapshot, EvidenceRef: null, Data: compile.Lifecycle)),
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                ["compile.summary"] = AssuranceReportReference.FromPath("/tmp/ucli/compile/summary.json", digest: null),
                ["compile.diagnostics"] = AssuranceReportReference.FromPath("/tmp/ucli/compile/diagnostics.json", digest: null),
            },
            ResidualRisks: [],
            RequestedMode: AssuranceRequestedExecutionMode.Auto,
            ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
            SessionKind: AssuranceSessionKind.TransientProbe,
            TimeoutMilliseconds: 10000,
            Compile: compile);
    }

    private static CompileClaimOutput CreateClaim (
        UcliCode id,
        AssuranceClaimStatus status,
        string statement,
        string subjectKind,
        CompileEvidenceOutput evidence)
    {
        return new CompileClaimOutput(
            Id: id,
            Status: status,
            Coverage: AssuranceCoverage.Full,
            Required: true,
            VerifierRef: CompileVerifierId,
            Statement: statement,
            Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = subjectKind,
                ["runId"] = RunIdTestValues.Compile,
            },
            Evidence: [evidence],
            ResidualRisks: []);
    }

    private static CompileOutput CreateCompileOutput (int errorCount)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new CompilePrimaryDiagnosticOutput(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new CompileOutput(
            runId: RunIdTestValues.Compile,
            refresh: new CompileRefreshOutput(
                Origin: CompileRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            scriptCompilation: new CompileScriptCompilationOutput(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new CompileDiagnosticsOutput(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            domainReload: new CompileDomainReloadOutput(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            lifecycle: new CompileLifecycleOutput(
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
                ActionRequired: canAcceptExecutionRequests ? null : DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: primaryDiagnostic));
    }
}
