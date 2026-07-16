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

    public static VerifyExecutionOutput CreateOutput (
        AssuranceVerdict verdict = AssuranceVerdict.Pass)
    {
        var compileClaimStatus = verdict == AssuranceVerdict.Pass
            ? AssuranceClaimStatus.Passed
            : AssuranceClaimStatus.Failed;
        return new VerifyExecutionOutput(
            Verdict: verdict,
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
                    Effects: []),
                new VerifyVerifierOutput(
                    Id: CompileVerifierId,
                    Kind: AssuranceVerifierKind.Compile,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: [CompileClaimCodes.UnityCompileNoErrors],
                    Effects: AssuranceEffectSets.Compile)
                {
                    ReportRef = "compile.summary",
                },
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
                    Evidence: [],
                    ResidualRisks: [])
                {
                    Validity = new ReadyClaimValidityOutput(
                        Kind: ReadyValidityKind.ProbeOnly,
                        GuaranteesReusableSession: false),
                },
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
                    Evidence:
                    [
                        new VerifyEvidenceOutput("compileSummary")
                        {
                            EvidenceRef = "compile.summary",
                        },
                    ],
                    ResidualRisks: []),
            ],
            Reports: new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
            {
                ["compile.summary"] = AssuranceReportReference.FromPath(
                    $".ucli/local/compile/{RunIdTestValues.CompileText}/summary.json",
                    digest: null),
            },
            ResidualRisks: [],
            Profile: new VerifyProfileOutput(
                Source: VerifyProfileSource.BuiltIn,
                Name: "built-in:default",
                Path: null,
                Digest: Sha256Digest.Parse("1111111111111111111111111111111111111111111111111111111111111111")),
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
