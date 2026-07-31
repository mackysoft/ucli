using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

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
            ExecutionId: RunIdTestValues.Compile,
            Verdict: Verdict.Pass,
            ErrorCount: 0,
            WarningCount: 0);
    }

    public static CompileExecutionOutput CreateOutput (int errorCount = 0)
    {
        var compile = CreateCompileOutput(errorCount);
        var compileStatus = errorCount == 0 ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed;
        var lifecycleStatus = errorCount == 0 ? AssuranceClaimStatus.Passed : AssuranceClaimStatus.Failed;
        var lifecycleExecutionRef = CreateTerminalReference();
        var terminalRecordRef = Assert.IsType<PathArtifactRef>(
            lifecycleExecutionRef.TerminalRecordRef);
        return new CompileExecutionOutput(
            Project: ProjectIdentityInfoTestFactory.Create(
                projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>")),
            LifecycleExecutionRef: lifecycleExecutionRef,
            Verdict: errorCount == 0
                ? Verdict.Pass
                : Verdict.Fail,
            Verifiers:
            [
                new CompileVerifierOutput(
                    Id: CompileVerifierId,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: CompileClaimCodes.All,
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: AssuranceReportIds.CompileSummary),
            ],
            Claims:
            [
                CreateClaim(
                    CompileClaimCodes.UnityCompileNoErrors,
                    compileStatus,
                    "Unity script compilation completed without compiler errors.",
                    "unityCompile",
                    CompileScriptEvidenceOutput.Create(
                        AssuranceReportIds.CompileDiagnostics,
                        compile.ScriptCompilation)),
                CreateClaim(
                    CompileClaimCodes.UnityDomainReloadSettled,
                    AssuranceClaimStatus.Passed,
                    "Unity domain reload reached a settled state after compile observation.",
                    "unityDomainReload",
                    CompileDomainReloadEvidenceOutput.Create(compile.DomainReload)),
                CreateClaim(
                    CompileClaimCodes.UnityLifecycleReadyAfterCompile,
                    lifecycleStatus,
                    "Unity lifecycle is ready after compile observation.",
                    "unityLifecycle",
                    CompileLifecycleEvidenceOutput.Create(compile.Lifecycle)),
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                [AssuranceReportIds.CompileSummary.Value] = AssuranceReportReference.FromPath(
                    terminalRecordRef.Path.Value,
                    terminalRecordRef.Digest),
                [AssuranceReportIds.CompileDiagnostics.Value] = AssuranceReportReference.FromPath(
                    terminalRecordRef.Path.Value,
                    terminalRecordRef.Digest),
            },
            ResidualRisks: [],
            Compile: compile);
    }

    public static ExecutionRef CreateActiveReference ()
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        return new ActiveExecutionRef(
            definition.ExecutionKind,
            RunIdTestValues.Compile,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState("compiling"),
            new ExecutionStatusLocator(
                $"lifecycle-executions/{RunIdTestValues.Compile:N}/status.json"));
    }

    public static ExecutionRef CreatePublishingReference ()
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        return new RecoveryExecutionRef(
            definition.ExecutionKind,
            RunIdTestValues.Compile,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState("publishing"),
            new ExecutionStatusLocator(
                $"lifecycle-executions/{RunIdTestValues.Compile:N}/status.json"));
    }

    public static CompileLifecycleResult CreateLifecycleResult ()
    {
        return new CompileLifecycleResult(
            new CompileLifecycleResult.RefreshEvidence(
                CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            new CompileLifecycleResult.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                new CompileLifecycleResult.DiagnosticsEvidence(
                    ErrorCount: 0,
                    WarningCount: 0,
                    PrimaryDiagnostic: null)),
            new CompileLifecycleResult.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            new CompileLifecycleResult.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                State: null,
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"),
                ActionRequired: null,
                PrimaryDiagnostic: null));
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
                ["executionId"] = RunIdTestValues.Compile,
            },
            Evidence: [evidence],
            ResidualRisks: []);
    }

    private static CompileOutput CreateCompileOutput (int errorCount)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new CompilePrimaryDiagnosticOutput(
                Kind: UnityEditorPrimaryDiagnosticKind.Compiler,
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new CompileOutput(
            refresh: new CompileRefreshOutput(
                Origin: CompileLifecycleRefreshOrigin.AssetDatabaseRefresh,
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
                EditorMode: UnityEditorMode.Batchmode,
                LifecycleState: canAcceptExecutionRequests
                    ? UnityEditorLifecycleState.Ready
                    : UnityEditorLifecycleState.CompileFailed,
                BlockingReason: canAcceptExecutionRequests
                    ? null
                    : UnityEditorBlockingReason.CompileFailed,
                CompileState: canAcceptExecutionRequests
                    ? UnityEditorCompileState.Ready
                    : UnityEditorCompileState.Failed,
                Generations: new UnityEditorGenerationSnapshot(14, 7, 0, 0),
                CanAcceptExecutionRequests: canAcceptExecutionRequests,
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"),
                ActionRequired: canAcceptExecutionRequests ? null : UnityEditorActionRequired.FixCompileErrors,
                PrimaryDiagnostic: primaryDiagnostic));
    }

    private static TerminalExecutionRef CreateTerminalReference ()
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        return new TerminalExecutionRef(
            definition.ExecutionKind,
            RunIdTestValues.Compile,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState("completed"),
            statusLocator: null,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $"lifecycle-executions/{RunIdTestValues.Compile:N}/terminal-record.json"),
                Sha256Digest.Parse(new string('f', 64)),
                sizeBytes: 512,
                DateTimeOffset.Parse("2026-05-17T00:00:04Z")));
    }
}
