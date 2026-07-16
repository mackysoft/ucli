using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

public sealed class AssuranceExecutionOutputCollectionContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithNullRequiredPayload_ThrowArgumentNullException ()
    {
        var project = ProjectIdentityInfoTestFactory.Create();
        var reports = new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal);
        var buildReports = new Dictionary<BuildArtifactKind, AssuranceReportReference>();
        var constructors = new (Action Construct, string ParameterName)[]
        {
            (() => new BuildExecutionOutput(AssuranceVerdict.Pass, project, null!, [], [], buildReports, []), "Build"),
            (() => new CompileExecutionOutput(
                AssuranceVerdict.Pass,
                project,
                [],
                [],
                reports,
                [],
                AssuranceRequestedExecutionMode.Auto,
                AssuranceResolvedExecutionMode.Oneshot,
                AssuranceSessionKind.TransientProbe,
                1,
                null!), "Compile"),
            (() => new VerifyExecutionOutput(AssuranceVerdict.Pass, project, [], [], reports, [], null!, 1), "Profile"),
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
        var readyVerifier = new ReadyVerifierOutput(new AssuranceVerifierId("ready"), true, true, []);
        var readyClaim = new ReadyClaimOutput(
            new UcliCode("READY_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            readyVerifier.Id,
            "statement",
            EmptySubject(),
            new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, false),
            [],
            []);
        var readyRisk = new ReadyResidualRiskOutput("READY_RISK", false);
        var readyVerifiers = new[] { readyVerifier };
        var readyClaims = new[] { readyClaim };
        var readyRisks = new[] { readyRisk };

        var compileVerifier = new CompileVerifierOutput(new AssuranceVerifierId("compile"), true, true, [], [], "compile");
        var compileClaim = new CompileClaimOutput(
            new UcliCode("COMPILE_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            compileVerifier.Id,
            "statement",
            EmptySubject(),
            [],
            []);
        var compileRisk = new CompileResidualRiskOutput("COMPILE_RISK", false);
        var compileVerifiers = new[] { compileVerifier };
        var compileClaims = new[] { compileClaim };
        var compileRisks = new[] { compileRisk };

        var buildVerifier = new BuildVerifierOutput(new AssuranceVerifierId("build"), true, true, [], [], BuildArtifactKind.Build);
        var buildClaim = new BuildClaimOutput(
            new UcliCode("BUILD_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            buildVerifier.Id,
            "statement",
            EmptySubject(),
            [],
            []);
        var buildRisk = new BuildResidualRiskOutput("BUILD_RISK", UcliDiagnosticSeverity.Warning, false, "risk");
        var buildVerifiers = new[] { buildVerifier };
        var buildClaims = new[] { buildClaim };
        var buildRisks = new[] { buildRisk };

        var verifyVerifier = new VerifyVerifierOutput(new AssuranceVerifierId("verify"), AssuranceVerifierKind.Ready, true, true, [], []);
        var verifyClaim = new VerifyClaimOutput(
            new UcliCode("VERIFY_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            verifyVerifier.Id,
            "statement",
            EmptySubject(),
            [],
            []);
        var verifyRisk = new VerifyResidualRiskOutput("VERIFY_RISK", false);
        var verifyVerifiers = new[] { verifyVerifier };
        var verifyClaims = new[] { verifyClaim };
        var verifyRisks = new[] { verifyRisk };
        var reports = new Dictionary<string, AssuranceReportReference>(StringComparer.Ordinal);
        var buildReports = new Dictionary<BuildArtifactKind, AssuranceReportReference>();
        var project = ProjectIdentityInfoTestFactory.Create();

        var ready = new ReadyExecutionOutput(
            AssuranceVerdict.Pass,
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
            AssuranceVerdict.Pass,
            project,
            compileVerifiers,
            compileClaims,
            reports,
            compileRisks,
            AssuranceRequestedExecutionMode.Auto,
            AssuranceResolvedExecutionMode.Oneshot,
            AssuranceSessionKind.TransientProbe,
            1,
            AssuranceExecutionOutputTestFactory.CreateCompileOutput());
        var build = new BuildExecutionOutput(
            AssuranceVerdict.Pass,
            project,
            AssuranceExecutionOutputTestFactory.CreateBuildOutput(),
            buildVerifiers,
            buildClaims,
            buildReports,
            buildRisks);
        var verify = new VerifyExecutionOutput(
            AssuranceVerdict.Pass,
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
            Assert.Same(snapshot.Verifier, Assert.Single(snapshot.Verifiers));
            Assert.Same(snapshot.Claim, Assert.Single(snapshot.Claims));
            Assert.Same(snapshot.Risk, Assert.Single(snapshot.Risks));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Verifiers)[0] = new object());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Claims)[0] = new object());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Risks)[0] = new object());
        });
    }

    private static IReadOnlyDictionary<string, object?> EmptySubject ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
