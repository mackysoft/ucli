using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Tests;

internal static class VerifyCommandTestData
{
    public static JsonGoldenFileNormalization CreateGoldenNormalization ()
    {
        return new JsonGoldenFileNormalization()
            .NormalizeStringPropertyValue("projectPath", "<projectPath>")
            .NormalizeStringPropertyValue("projectFingerprint", "<projectFingerprint>")
            .NormalizeStringPropertyValue("unityVersion", "<unityVersion>");
    }

    public static VerifyExecutionOutput CreateOutput (
        string verdict = VerifyVerdictValues.Pass)
    {
        var compileClaimStatus = string.Equals(verdict, VerifyVerdictValues.Pass, StringComparison.Ordinal)
            ? VerifyClaimStatusValues.Passed
            : VerifyClaimStatusValues.Failed;
        return new VerifyExecutionOutput(
            Verdict: verdict,
            Project: ProjectIdentityInfoTestFactory.Create(
                projectPath: "<projectPath>",
                projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>"),
                unityVersion: "<unityVersion>"),
            Verifiers:
            [
                new VerifyVerifierOutput(
                    Id: "ready.lifecycle",
                    Kind: VerifyStepKindValues.Ready,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: ["UNITY_READY_EXECUTION"],
                    Effects: []),
                new VerifyVerifierOutput(
                    Id: "compile",
                    Kind: VerifyStepKindValues.Compile,
                    Deterministic: false,
                    Required: true,
                    PrimaryClaims: ["UNITY_COMPILE_NO_ERRORS"],
                    Effects: VerifyEffectValues.Compile)
                {
                    ReportRef = "compile.summary",
                },
            ],
            Claims:
            [
                new VerifyClaimOutput(
                    Id: "UNITY_READY_EXECUTION",
                    Status: VerifyClaimStatusValues.Passed,
                    Coverage: VerifyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "ready.lifecycle",
                    Statement: "Unity is ready for execution.",
                    Subject: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["target"] = "execution",
                    },
                    Evidence: [],
                    ResidualRisks: [])
                {
                    Validity = new ReadyClaimValidityOutput(
                        Kind: "probeOnly",
                        GuaranteesReusableSession: false),
                },
                new VerifyClaimOutput(
                    Id: "UNITY_COMPILE_NO_ERRORS",
                    Status: compileClaimStatus,
                    Coverage: VerifyCoverageValues.Full,
                    Required: true,
                    VerifierRef: "compile",
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
            Reports: new Dictionary<string, VerifyReportOutput>(StringComparer.Ordinal)
            {
                ["compile.summary"] = new VerifyReportOutput
                {
                    Path = ".ucli/local/compile/run-1/summary.json",
                },
            },
            ResidualRisks: [],
            Profile: new VerifyProfileOutput(
                Source: VerifyProfileSourceValues.BuiltIn,
                Name: "built-in:default",
                Path: null,
                Digest: "1111111111111111111111111111111111111111111111111111111111111111"),
            TimeoutMilliseconds: 120000);
    }

    public static VerifyStepProgressEntry CreateReadyStepProgressEntry ()
    {
        return new VerifyStepProgressEntry(
            VerifyStepKindValues.Ready,
            Required: true,
            Effects: [],
            SkipReason: null);
    }

    public static VerifyStepProgressEntry CreateSkippedPostReadProgressEntry ()
    {
        return new VerifyStepProgressEntry(
            VerifyStepKindValues.PostRead,
            Required: false,
            Effects: [],
            SkipReason: VerifyStepSkipReasons.PostReadNotNeeded);
    }

    public static VerifyDiagnosticEntry CreateDiagnosticEntry ()
    {
        return new VerifyDiagnosticEntry(
            "VERIFY_STUB",
            "stub diagnostic",
            "error",
            VerifyStepKindValues.Compile);
    }
}
