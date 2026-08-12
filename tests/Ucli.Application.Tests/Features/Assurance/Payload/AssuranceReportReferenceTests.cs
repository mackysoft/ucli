using System.Collections.ObjectModel;
using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

public sealed class AssuranceReportReferenceTests
{
    public static TheoryData<string?> InvalidLocators => new()
    {
        null,
        string.Empty,
        " ",
        " report.json",
        "report.json ",
        "\treport.json",
        "report.json\r\n",
    };

    [Fact]
    [Trait("Size", "Small")]
    public void FromPath_WithValidLocator_SetsOnlyPathAndPreservesDigest ()
    {
        var digest = Sha256Digest.Parse(new string('a', 64));

        var reference = AssuranceReportReference.FromPath("artifacts/report.json", digest);

        Assert.Equal("artifacts/report.json", reference.Path);
        Assert.Null(reference.Uri);
        Assert.Equal(digest, reference.Digest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FromUri_WithValidLocator_SetsOnlyUriAndPreservesDigest ()
    {
        var digest = Sha256Digest.Parse(new string('b', 64));

        var reference = AssuranceReportReference.FromUri("ucli://logs/unity?tail=200", digest);

        Assert.Null(reference.Path);
        Assert.Equal("ucli://logs/unity?tail=200", reference.Uri);
        Assert.Equal(digest, reference.Digest);
    }

    [Theory]
    [MemberData(nameof(InvalidLocators))]
    [Trait("Size", "Small")]
    public void Factories_WithInvalidLocator_ThrowArgumentException (string? locator)
    {
        var pathException = Assert.ThrowsAny<ArgumentException>(
            () => AssuranceReportReference.FromPath(locator!, digest: null));
        var uriException = Assert.ThrowsAny<ArgumentException>(
            () => AssuranceReportReference.FromUri(locator!, digest: null));

        Assert.Equal("path", pathException.ParamName);
        Assert.Equal("uri", uriException.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StringKeyedExecutionOutputs_ExposeOrdinalReadOnlyReportSnapshots ()
    {
        var report = AssuranceReportReference.FromPath("artifacts/report.json", digest: null);
        var source = new Dictionary<string, AssuranceReportReference>(StringComparer.OrdinalIgnoreCase)
        {
            ["report"] = report,
        };
        var snapshots = CreateReportSnapshots(source);

        source.Add("late", report);

        Assert.All(snapshots, snapshot =>
        {
            var readOnly = Assert.IsType<ReadOnlyDictionary<string, AssuranceReportReference>>(snapshot);
            Assert.Equal(report, readOnly["report"]);
            Assert.False(readOnly.ContainsKey("REPORT"));
            Assert.False(readOnly.ContainsKey("late"));
            Assert.Throws<NotSupportedException>(() =>
                ((IDictionary<string, AssuranceReportReference>)readOnly).Add("other", report));
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithNullReports_ThrowArgumentNullException ()
    {
        var constructors = new Action[]
        {
            static () => new BuildExecutionOutput(
                Project: null!,
                Build: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: []),
            static () => new CompileExecutionOutput(
                Project: null!,
                LifecycleExecutionRef: null!,
                Verdict: Verdict.Pass,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: [],
                Compile: null!),
            static () => new ReadyExecutionOutput(
                Project: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: [],
                Target: ReadyTarget.Execution,
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
                SessionKind: AssuranceSessionKind.TransientProbe,
                TimeoutMilliseconds: 1,
                Lifecycle: null,
                ReadIndex: null),
            static () => new VerifyExecutionOutput(
                Project: null!,
                Verifiers: [],
                Claims: [],
                Reports: null!,
                ResidualRisks: [],
                Profile: null!,
                TimeoutMilliseconds: 1),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                "Reports",
                Assert.Throws<ArgumentNullException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileAndVerifyOutputs_WithUnresolvedTypedReportReference_ThrowArgumentException ()
    {
        var constructors = new Action[]
        {
            () => CreateCompileOutput(
                verifierReportRef: new AssuranceReportId("missing"),
                evidenceRef: new AssuranceReportId("report")),
            () => CreateCompileOutput(
                verifierReportRef: new AssuranceReportId("report"),
                evidenceRef: new AssuranceReportId("missing")),
            () => CreateVerifyOutput(
                verifierReportRef: new AssuranceReportId("missing"),
                evidenceRef: new AssuranceReportId("report")),
            () => CreateVerifyOutput(
                verifierReportRef: new AssuranceReportId("report"),
                evidenceRef: new AssuranceReportId("missing")),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                "Reports",
                Assert.Throws<ArgumentException>(constructor).ParamName));
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, AssuranceReportReference>> CreateReportSnapshots (
        IReadOnlyDictionary<string, AssuranceReportReference> reports)
    {
        var project = ProjectIdentityInfoTestFactory.Create();
        return
        [
            new CompileExecutionOutput(
                Project: project,
                LifecycleExecutionRef:
                    AssuranceExecutionOutputTestFactory.CreateCompileExecutionRef(),
                Verdict: Verdict.Pass,
                Verifiers: [],
                Claims: [],
                Reports: reports,
                ResidualRisks: [],
                Compile: AssuranceExecutionOutputTestFactory.CreateCompileOutput()).Reports,
            new ReadyExecutionOutput(
                Project: project,
                Verifiers: [],
                Claims: [],
                Reports: reports,
                ResidualRisks: [],
                Target: ReadyTarget.Execution,
                RequestedMode: AssuranceRequestedExecutionMode.Auto,
                ResolvedMode: AssuranceResolvedExecutionMode.Oneshot,
                SessionKind: AssuranceSessionKind.TransientProbe,
                TimeoutMilliseconds: 1,
                Lifecycle: null,
                ReadIndex: null).Reports,
            new VerifyExecutionOutput(
                Project: project,
                Verifiers: [],
                Claims: [],
                Reports: reports,
                ResidualRisks: [],
                Profile: AssuranceExecutionOutputTestFactory.CreateVerifyProfileOutput(),
                TimeoutMilliseconds: 1).Reports,
        ];
    }

    private static CompileExecutionOutput CreateCompileOutput (
        AssuranceReportId verifierReportRef,
        AssuranceReportId evidenceRef)
    {
        var verifierId = new AssuranceVerifierId("compile");
        var claimId = new UcliCode("COMPILE_CLAIM");
        return new CompileExecutionOutput(
            Project: ProjectIdentityInfoTestFactory.Create(),
            LifecycleExecutionRef:
                AssuranceExecutionOutputTestFactory.CreateCompileExecutionRef(),
            Verdict: Verdict.Pass,
            Verifiers:
            [
                new CompileVerifierOutput(
                    verifierId,
                    Deterministic: true,
                    Required: true,
                    PrimaryClaims: [claimId],
                    Effects: [],
                    ReportRef: verifierReportRef),
            ],
            Claims:
            [
                new CompileClaimOutput(
                    claimId,
                    AssuranceClaimStatus.Passed,
                    AssuranceCoverage.Full,
                    Required: true,
                    verifierId,
                    "statement",
                    new Dictionary<string, object?>(StringComparer.Ordinal),
                    [
                        CompileScriptEvidenceOutput.Create(
                            evidenceRef,
                            AssuranceExecutionOutputTestFactory.CreateCompileOutput().ScriptCompilation),
                    ],
                    []),
            ],
            Reports: CreateSingleReportMap(),
            ResidualRisks: [],
            Compile: AssuranceExecutionOutputTestFactory.CreateCompileOutput());
    }

    private static VerifyExecutionOutput CreateVerifyOutput (
        AssuranceReportId verifierReportRef,
        AssuranceReportId evidenceRef)
    {
        var verifierId = new AssuranceVerifierId("verify");
        var claimId = new UcliCode("VERIFY_CLAIM");
        var compileEvidence = CompileScriptEvidenceOutput.Create(
            evidenceRef,
            AssuranceExecutionOutputTestFactory.CreateCompileOutput().ScriptCompilation);
        return new VerifyExecutionOutput(
            Project: ProjectIdentityInfoTestFactory.Create(),
            Verifiers:
            [
                new VerifyVerifierOutput(
                    verifierId,
                    AssuranceVerifierKind.Compile,
                    Deterministic: true,
                    Required: true,
                    PrimaryClaims: [claimId],
                    Effects: [],
                    ReportRef: verifierReportRef),
            ],
            Claims:
            [
                new VerifyClaimOutput(
                    claimId,
                    AssuranceClaimStatus.Passed,
                    AssuranceCoverage.Full,
                    Required: true,
                    verifierId,
                    "statement",
                    new Dictionary<string, object?>(StringComparer.Ordinal),
                    Validity: null,
                    [VerifyScriptEvidenceOutput.Create(compileEvidence)],
                    []),
            ],
            Reports: CreateSingleReportMap(),
            ResidualRisks: [],
            Profile: AssuranceExecutionOutputTestFactory.CreateVerifyProfileOutput(),
            TimeoutMilliseconds: 1);
    }

    private static IReadOnlyDictionary<string, AssuranceReportReference> CreateSingleReportMap ()
    {
        return new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
        {
            ["report"] = AssuranceReportReference.FromPath("artifacts/report.json", digest: null),
        };
    }
}
