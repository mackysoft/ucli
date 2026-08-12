using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

public sealed class AssuranceExecutionOutputCollectionContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithNullRequiredPayload_ThrowArgumentNullException ()
    {
        var project = ProjectIdentityInfoTestFactory.Create();
        var report = AssuranceReportReference.FromPath("report.json", digest: null);
        var reports = new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
        {
            ["compile"] = report,
            [AssuranceReportIds.CompileSummary.Value] = report,
        };
        var buildReports = new BuildReportsOutput(report, null, report, report);
        var constructors = new (Action Construct, string ParameterName)[]
        {
            (() => new BuildExecutionOutput(project, null!, [], [], buildReports, []), "Build"),
            (() => new CompileExecutionOutput(
                project,
                AssuranceExecutionOutputTestFactory.CreateCompileExecutionRef(),
                Verdict.Pass,
                [],
                [],
                reports,
                [],
                null!), "Compile"),
            (() => new VerifyExecutionOutput(project, [], [], reports, [], null!, 1), "Profile"),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                constructor.ParameterName,
                Assert.Throws<ArgumentNullException>(constructor.Construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_ExposeOwnedReadOnlyCollectionSnapshots ()
    {
        var compileOutput = AssuranceExecutionOutputTestFactory.CreateCompileOutput();
        var readyDecisionEvidence = ReadyDecisionEvidenceOutput.Create(
            new ReadyDecisionEvidenceData(Code: null, Message: "ready"));
        var readyClaimId = new UcliCode("READY_CLAIM");
        var readyVerifier = new ReadyVerifierOutput(new AssuranceVerifierId("ready"), true, true, [readyClaimId]);
        var readyClaim = new ReadyClaimOutput(
            readyClaimId,
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            readyVerifier.Id,
            "statement",
            EmptySubject(),
            ReadyClaimValidityOutput.ProbeOnly(),
            [readyDecisionEvidence],
            []);
        var readyRisk = new ReadyResidualRiskOutput(new UcliCode("READY_RISK"), false, "risk");
        var readyVerifiers = new[] { readyVerifier };
        var readyClaims = new[] { readyClaim };
        var readyRisks = new[] { readyRisk };

        var compileClaimId = new UcliCode("COMPILE_CLAIM");
        var compileVerifier = new CompileVerifierOutput(
            new AssuranceVerifierId("compile"),
            true,
            true,
            [compileClaimId],
            [],
            AssuranceReportIds.CompileSummary);
        var compileClaim = new CompileClaimOutput(
            compileClaimId,
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            compileVerifier.Id,
            "statement",
            EmptySubject(),
            [CompileScriptEvidenceOutput.Create(AssuranceReportIds.CompileSummary, compileOutput.ScriptCompilation)],
            []);
        var compileRisk = new CompileResidualRiskOutput(new UcliCode("COMPILE_RISK"), false, "risk");
        var compileVerifiers = new[] { compileVerifier };
        var compileClaims = new[] { compileClaim };
        var compileRisks = new[] { compileRisk };

        var buildClaimId = new UcliCode("BUILD_CLAIM");
        var buildVerifier = new BuildVerifierOutput(new AssuranceVerifierId("build"), true, true, [buildClaimId], []);
        var buildClaim = new BuildClaimOutput(
            buildClaimId,
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            buildVerifier.Id,
            "statement",
            EmptySubject(),
            [BuildInputEvidenceOutput.Create(BuildServiceTestSupport.CreateInputProbe())],
            []);
        var buildRisk = new BuildResidualRiskOutput(new UcliCode("BUILD_RISK"), UcliDiagnosticSeverity.Warning, false, "risk");
        var buildVerifiers = new[] { buildVerifier };
        var buildClaims = new[] { buildClaim };
        var buildRisks = new[] { buildRisk };

        var verifyClaimId = new UcliCode("VERIFY_CLAIM");
        var verifyVerifier = new VerifyVerifierOutput(new AssuranceVerifierId("verify"), AssuranceVerifierKind.Ready, true, true, [verifyClaimId], [], null);
        var verifyClaim = new VerifyClaimOutput(
            verifyClaimId,
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            verifyVerifier.Id,
            "statement",
            EmptySubject(),
            null,
            [VerifyReadinessEvidenceOutput.Create(readyDecisionEvidence)],
            []);
        var verifyRisk = new VerifyResidualRiskOutput(new UcliCode("VERIFY_RISK"), false, "risk");
        var verifyVerifiers = new[] { verifyVerifier };
        var verifyClaims = new[] { verifyClaim };
        var verifyRisks = new[] { verifyRisk };
        var report = AssuranceReportReference.FromPath("report.json", digest: null);
        var reports = new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal)
        {
            ["compile"] = report,
            [AssuranceReportIds.CompileSummary.Value] = report,
        };
        var buildReports = new BuildReportsOutput(report, null, report, report);
        var project = ProjectIdentityInfoTestFactory.Create();

        var ready = new ReadyExecutionOutput(
            project,
            readyVerifiers,
            readyClaims,
            reports,
            readyRisks,
            ReadyTarget.Execution,
            AssuranceRequestedExecutionMode.Auto,
            AssuranceResolvedExecutionMode.Oneshot,
            AssuranceSessionKind.TransientProbe,
            1,
            Lifecycle: null,
            ReadIndex: null);
        var compile = new CompileExecutionOutput(
            project,
            AssuranceExecutionOutputTestFactory.CreateCompileExecutionRef(),
            Verdict.Pass,
            compileVerifiers,
            compileClaims,
            reports,
            compileRisks,
            compileOutput);
        var build = new BuildExecutionOutput(
            project,
            AssuranceExecutionOutputTestFactory.CreateBuildOutput(),
            buildVerifiers,
            buildClaims,
            buildReports,
            buildRisks);
        var verify = new VerifyExecutionOutput(
            project,
            verifyVerifiers,
            verifyClaims,
            reports,
            verifyRisks,
            AssuranceExecutionOutputTestFactory.CreateVerifyProfileOutput(),
            1);

        readyVerifiers[0] = null!;
        readyClaims[0] = null!;
        readyRisks[0] = null!;
        compileVerifiers[0] = null!;
        compileClaims[0] = null!;
        compileRisks[0] = null!;
        buildVerifiers[0] = null!;
        buildClaims[0] = null!;
        buildRisks[0] = null!;
        verifyVerifiers[0] = null!;
        verifyClaims[0] = null!;
        verifyRisks[0] = null!;

        var snapshots = new (IReadOnlyList<object> Verifiers, object Verifier, IReadOnlyList<object> Claims, object Claim, IReadOnlyList<object> Risks, object Risk)[]
        {
            (ready.Verifiers, readyVerifier, ready.Claims, readyClaim, ready.ResidualRisks, readyRisk),
            (compile.Verifiers, compileVerifier, compile.Claims, compileClaim, compile.ResidualRisks, compileRisk),
            (build.Verifiers, buildVerifier, build.Claims, buildClaim, build.ResidualRisks, buildRisk),
            (verify.Verifiers, verifyVerifier, verify.Claims, verifyClaim, verify.ResidualRisks, verifyRisk),
        };
        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal(snapshot.Verifier, Assert.Single(snapshot.Verifiers));
            Assert.Equal(snapshot.Claim, Assert.Single(snapshot.Claims));
            Assert.Equal(snapshot.Risk, Assert.Single(snapshot.Risks));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Verifiers)[0] = new object());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Claims)[0] = new object());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Risks)[0] = new object());
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildExecutionOutput_WhenAssuranceAggregateIsNotEstablished_ThrowsArgumentException ()
    {
        var firstVerifierId = new AssuranceVerifierId("first");
        var secondVerifierId = new AssuranceVerifierId("second");
        var firstClaimId = new UcliCode("FIRST_CLAIM");
        var invalidAggregates = new Action[]
        {
            () => CreateBuildExecutionOutput(
                [
                    CreateBuildVerifier(firstVerifierId, required: false, []),
                    CreateBuildVerifier(firstVerifierId, required: false, []),
                ],
                []),
            () => CreateBuildExecutionOutput(
                [CreateBuildVerifier(firstVerifierId, required: false, [])],
                [
                    CreateBuildClaim(firstClaimId, firstVerifierId, required: false),
                    CreateBuildClaim(firstClaimId, firstVerifierId, required: false),
                ]),
            () => CreateBuildExecutionOutput(
                [],
                [CreateBuildClaim(firstClaimId, firstVerifierId, required: false)]),
            () => CreateBuildExecutionOutput(
                [CreateBuildVerifier(firstVerifierId, required: false, [])],
                [CreateBuildClaim(firstClaimId, firstVerifierId, required: true)]),
            () => CreateBuildExecutionOutput(
                [CreateBuildVerifier(firstVerifierId, required: true, [])],
                [CreateBuildClaim(firstClaimId, firstVerifierId, required: true)]),
            () => CreateBuildExecutionOutput(
                [CreateBuildVerifier(firstVerifierId, required: false, [firstClaimId])],
                []),
            () => CreateBuildExecutionOutput(
                [
                    CreateBuildVerifier(firstVerifierId, required: false, [firstClaimId]),
                    CreateBuildVerifier(secondVerifierId, required: false, []),
                ],
                [CreateBuildClaim(firstClaimId, secondVerifierId, required: false)]),
            () => CreateBuildExecutionOutput(
                [CreateBuildVerifier(firstVerifierId, required: true, [firstClaimId])],
                [CreateBuildClaim(firstClaimId, firstVerifierId, required: false)]),
        };

        Assert.All(invalidAggregates, construct => Assert.Throws<ArgumentException>(construct));
    }

    private static BuildExecutionOutput CreateBuildExecutionOutput (
        IReadOnlyList<BuildVerifierOutput> verifiers,
        IReadOnlyList<BuildClaimOutput> claims)
    {
        var report = AssuranceReportReference.FromPath("report.json", digest: null);
        return new BuildExecutionOutput(
            ProjectIdentityInfoTestFactory.Create(),
            AssuranceExecutionOutputTestFactory.CreateBuildOutput(),
            verifiers,
            claims,
            new BuildReportsOutput(report, null, report, report),
            []);
    }

    private static BuildVerifierOutput CreateBuildVerifier (
        AssuranceVerifierId id,
        bool required,
        IReadOnlyList<UcliCode> primaryClaims)
    {
        return new BuildVerifierOutput(
            id,
            Deterministic: true,
            required,
            primaryClaims,
            []);
    }

    private static BuildClaimOutput CreateBuildClaim (
        UcliCode id,
        AssuranceVerifierId verifierId,
        bool required)
    {
        return new BuildClaimOutput(
            id,
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            required,
            verifierId,
            "statement",
            EmptySubject(),
            [BuildInputEvidenceOutput.Create(BuildServiceTestSupport.CreateInputProbe())],
            []);
    }

    private static IReadOnlyDictionary<string, object?> EmptySubject ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
