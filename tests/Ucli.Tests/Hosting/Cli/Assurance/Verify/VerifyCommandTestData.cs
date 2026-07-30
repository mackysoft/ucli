using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Vocabulary;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Tests;

internal static class VerifyCommandTestData
{
    private static readonly AssuranceVerifierId CompileVerifierId = new("compile");
    private static readonly AssuranceVerifierId ReadyVerifierId = new("ready.lifecycle");

    public static JsonGoldenFileNormalization CreateGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>")
            .NormalizeStringPropertyValue("unityVersion", "<unityVersion>");
    }

    public static VerifyExecutionOutput CreateOutput (Verdict verdict)
    {
        var compileClaimStatus = verdict == Verdict.Pass
            ? AssuranceClaimStatus.Passed
            : verdict == Verdict.Fail
                ? AssuranceClaimStatus.Failed
                : AssuranceClaimStatus.Indeterminate;
        var readyEvidence = (ReadyLifecycleEvidenceOutput)ReadyCommandTestData
            .CreateOutput(Verdict.Pass)
            .Claims
            .Single()
            .Evidence
            .Single();
        var compileOutput = CompileCommandTestData.CreateOutput(
            verdict == Verdict.Fail ? 1 : 0);
        var compileEvidence = (CompileScriptEvidenceOutput)compileOutput.Claims
            .Single(static claim => claim.Id == CompileClaimCodes.UnityCompileNoErrors)
            .Evidence
            .Single();
        return new VerifyExecutionOutput(
            Project: ProjectIdentityInfoTestFactory.Create(
                projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>"),
                unityVersion: "<unityVersion>"),
            Verifiers:
            [
                new VerifyVerifierOutput(
                    Id: ReadyVerifierId,
                    Kind: AssuranceVerifierKind.Ready,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [ReadyClaimCodes.UnityReadyExecution],
                    Effects: [],
                    ReportRef: null),
                new VerifyVerifierOutput(
                    Id: CompileVerifierId,
                    Kind: AssuranceVerifierKind.Compile,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [CompileClaimCodes.UnityCompileNoErrors],
                    Effects: AssuranceEffectSets.Compile,
                    ReportRef: AssuranceReportIds.CompileSummary),
            ],
            Claims:
            [
                new VerifyClaimOutput(
                    Id: ReadyClaimCodes.UnityReadyExecution,
                    Status: AssuranceClaimStatus.Passed,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: ReadyVerifierId,
                    Statement: "Unity is ready for execution.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = "execution",
                    },
                    Validity: ReadyClaimValidityOutput.ProbeOnly(),
                    Evidence:
                    [
                        VerifyReadyLifecycleEvidenceOutput.Create(readyEvidence),
                    ],
                    ResidualRisks: []),
                new VerifyClaimOutput(
                    Id: CompileClaimCodes.UnityCompileNoErrors,
                    Status: compileClaimStatus,
                    Coverage: AssuranceCoverage.Full,
                    Required: true,
                    VerifierRef: CompileVerifierId,
                    Statement: "Unity script compilation has no errors.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["kind"] = "unityCompile",
                    },
                    Validity: null,
                    Evidence:
                    [
                        VerifyScriptEvidenceOutput.Create(compileEvidence),
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                [AssuranceReportIds.CompileSummary.Value] = AssuranceReportReference.FromPath(
                    $".ucli/local/compile/{RunIdTestValues.CompileText}/summary.json",
                    digest: null),
                [AssuranceReportIds.CompileDiagnostics.Value] = AssuranceReportReference.FromPath(
                    $".ucli/local/compile/{RunIdTestValues.CompileText}/diagnostics.json",
                    digest: null),
            },
            ResidualRisks: [],
            Profile: VerifyProfileOutput.BuiltIn(
                "built-in:default",
                Sha256Digest.Parse("1111111111111111111111111111111111111111111111111111111111111111")),
            TimeoutMilliseconds: 120000);
    }

    public static VerifyStepProgressEntry CreateReadyStepProgressEntry ()
    {
        return new VerifyStepProgressEntry(
            VerifyStepKind.Ready,
            Required: true,
            Effects: [],
            SkipReason: null);
    }

    public static VerifyStepProgressEntry CreateSkippedPostReadProgressEntry ()
    {
        return new VerifyStepProgressEntry(
            VerifyStepKind.PostRead,
            Required: false,
            Effects: [],
            SkipReason: VerifyStepSkipReasons.PostReadNotNeeded);
    }

    public static VerifyDiagnosticEntry CreateDiagnosticEntry ()
    {
        return new VerifyDiagnosticEntry(
            "VERIFY_STUB",
            "stub diagnostic",
            UcliDiagnosticSeverity.Error,
            VerifyStepKind.Compile);
    }
}
