using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Application.Tests.Features.Assurance.Build;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

public sealed class AssuranceCodeOutputContractTests
{
    private static readonly AssuranceVerifierId BuildVerifierId = new("build");
    private static readonly AssuranceVerifierId CompileVerifierId = new("compile");
    private static readonly AssuranceVerifierId ReadyVerifierId = new("ready");
    private static readonly AssuranceVerifierId VerifyVerifierId = new("verify");

    [Fact]
    [Trait("Size", "Small")]
    public void FixedVerifierOutputs_ExposeCommandKind ()
    {
        var claim = new UcliCode("CLAIM");
        var build = new BuildVerifierOutput(BuildVerifierId, true, true, [claim], []);
        var compile = new CompileVerifierOutput(
            CompileVerifierId,
            true,
            true,
            [claim],
            [],
            AssuranceReportIds.CompileSummary);
        var ready = new ReadyVerifierOutput(ReadyVerifierId, true, true, [claim]);

        Assert.Equal(AssuranceVerifierKind.Build, build.Kind);
        Assert.Equal(AssuranceVerifierKind.Compile, compile.Kind);
        Assert.Equal(AssuranceVerifierKind.Ready, ready.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyVerifierOutput_ExposesItsFixedEmptyEffectSetAsTypedValues ()
    {
        var ready = new ReadyVerifierOutput(
            ReadyVerifierId,
            Deterministic: true,
            Required: false,
            PrimaryClaims: []);

        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<AssuranceEffect>>(ready.Effects));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ClaimOutputs_WithUndefinedStatus_ThrowArgumentOutOfRangeException ()
    {
        const AssuranceClaimStatus InvalidStatus = (AssuranceClaimStatus)0;
        var constructors = new Action[]
        {
            static () => new CompileClaimOutput(new UcliCode("COMPILE_CLAIM"), InvalidStatus, AssuranceCoverage.Full, true, CompileVerifierId, "statement", EmptySubject(), [], []),
            static () => new ReadyClaimOutput(new UcliCode("READY_CLAIM"), InvalidStatus, AssuranceCoverage.Full, true, ReadyVerifierId, "statement", EmptySubject(), ReadyClaimValidityOutput.ProbeOnly(), [], []),
            static () => new VerifyClaimOutput(new UcliCode("VERIFY_CLAIM"), InvalidStatus, AssuranceCoverage.Full, true, VerifyVerifierId, "statement", EmptySubject(), null, [], []),
            static () => new BuildClaimOutput(new UcliCode("BUILD_CLAIM"), InvalidStatus, AssuranceCoverage.Full, true, BuildVerifierId, "statement", EmptySubject(), [], []),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal("Status", Assert.Throws<ArgumentOutOfRangeException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ClaimOutputs_WithUndefinedCoverage_ThrowArgumentOutOfRangeException ()
    {
        const AssuranceCoverage InvalidCoverage = (AssuranceCoverage)0;
        var constructors = new Action[]
        {
            static () => new CompileClaimOutput(new UcliCode("COMPILE_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, CompileVerifierId, "statement", EmptySubject(), [], []),
            static () => new ReadyClaimOutput(new UcliCode("READY_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, ReadyVerifierId, "statement", EmptySubject(), ReadyClaimValidityOutput.ProbeOnly(), [], []),
            static () => new VerifyClaimOutput(new UcliCode("VERIFY_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, VerifyVerifierId, "statement", EmptySubject(), null, [], []),
            static () => new BuildClaimOutput(new UcliCode("BUILD_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, BuildVerifierId, "statement", EmptySubject(), [], []),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal("Coverage", Assert.Throws<ArgumentOutOfRangeException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ClaimOutputs_WithoutEvidence_ThrowArgumentException ()
    {
        var constructors = new Action[]
        {
            static () => new BuildClaimOutput(new UcliCode("BUILD_CLAIM"), AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, BuildVerifierId, "statement", EmptySubject(), [], []),
            static () => new CompileClaimOutput(new UcliCode("COMPILE_CLAIM"), AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, CompileVerifierId, "statement", EmptySubject(), [], []),
            static () => new ReadyClaimOutput(new UcliCode("READY_CLAIM"), AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, ReadyVerifierId, "statement", EmptySubject(), ReadyClaimValidityOutput.ProbeOnly(), [], []),
            static () => new VerifyClaimOutput(new UcliCode("VERIFY_CLAIM"), AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, VerifyVerifierId, "statement", EmptySubject(), null, [], []),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                "Evidence",
                Assert.Throws<ArgumentException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyExecutionOutput_WithUndefinedSessionKind_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            static () => new ReadyExecutionOutput(
                null!,
                null!,
                null!,
                null!,
                null!,
                ReadyTarget.Execution,
                AssuranceRequestedExecutionMode.Auto,
                AssuranceResolvedExecutionMode.Oneshot,
                default,
                0,
                null,
                null));

        Assert.Equal("SessionKind", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyExecutionOutput_WithUndefinedExecutionMode_ThrowsArgumentOutOfRangeException ()
    {
        var constructors = new (Action Construct, string ParameterName)[]
        {
            (static () => new ReadyExecutionOutput(null!, null!, null!, null!, null!, ReadyTarget.Execution, default, AssuranceResolvedExecutionMode.Oneshot, AssuranceSessionKind.TransientProbe, 0, null, null), "RequestedMode"),
            (static () => new ReadyExecutionOutput(null!, null!, null!, null!, null!, ReadyTarget.Execution, AssuranceRequestedExecutionMode.Auto, default, AssuranceSessionKind.TransientProbe, 0, null, null), "ResolvedMode"),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                constructor.ParameterName,
                Assert.Throws<ArgumentOutOfRangeException>(constructor.Construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ClaimOutputs_WithNullId_ThrowArgumentNullException ()
    {
        var constructors = new Action[]
        {
            static () => new BuildClaimOutput(null!, AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, BuildVerifierId, "statement", EmptySubject(), [], []),
            static () => new CompileClaimOutput(null!, AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, CompileVerifierId, "statement", EmptySubject(), [], []),
            static () => new ReadyClaimOutput(null!, AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, ReadyVerifierId, "statement", EmptySubject(), ReadyClaimValidityOutput.ProbeOnly(), [], []),
            static () => new VerifyClaimOutput(null!, AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, VerifyVerifierId, "statement", EmptySubject(), null, [], []),
        };

        Assert.All(constructors, constructor => Assert.Equal("Id", Assert.Throws<ArgumentNullException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifierOutputs_WithNullPrimaryClaims_ThrowArgumentNullException ()
    {
        var constructors = new Action[]
        {
            static () => new BuildVerifierOutput(BuildVerifierId, true, true, null!, []),
            static () => new CompileVerifierOutput(
                CompileVerifierId,
                true,
                true,
                null!,
                [],
                AssuranceReportIds.CompileSummary),
            static () => new ReadyVerifierOutput(ReadyVerifierId, true, true, null!),
            static () => new VerifyVerifierOutput(VerifyVerifierId, AssuranceVerifierKind.Ready, true, true, null!, [], null),
        };

        Assert.All(constructors, constructor => Assert.Equal("PrimaryClaims", Assert.Throws<ArgumentNullException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifierOutputs_WithNullPrimaryClaim_ThrowArgumentException ()
    {
        var invalidClaims = new UcliCode[] { null! };
        var constructors = new Action[]
        {
            () => new BuildVerifierOutput(BuildVerifierId, true, true, invalidClaims, []),
            () => new CompileVerifierOutput(
                CompileVerifierId,
                true,
                true,
                invalidClaims,
                [],
                AssuranceReportIds.CompileSummary),
            () => new ReadyVerifierOutput(ReadyVerifierId, true, true, invalidClaims),
            () => new VerifyVerifierOutput(VerifyVerifierId, AssuranceVerifierKind.Ready, true, true, invalidClaims, [], null),
        };

        Assert.All(constructors, constructor => Assert.Equal("PrimaryClaims", Assert.Throws<ArgumentException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RequiredVerifierOutputs_WithoutPrimaryClaims_ThrowArgumentException ()
    {
        var constructors = new Action[]
        {
            () => new BuildVerifierOutput(BuildVerifierId, true, true, [], []),
            () => new CompileVerifierOutput(
                CompileVerifierId,
                true,
                true,
                [],
                [],
                AssuranceReportIds.CompileSummary),
            () => new ReadyVerifierOutput(ReadyVerifierId, true, true, []),
            () => new VerifyVerifierOutput(
                VerifyVerifierId,
                AssuranceVerifierKind.Ready,
                true,
                true,
                [],
                [],
                null),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal(
                "PrimaryClaims",
                Assert.Throws<ArgumentException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CompileVerifierOutput_WithNullReportReference_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CompileVerifierOutput(
                CompileVerifierId,
                Deterministic: true,
                Required: true,
                PrimaryClaims: [new UcliCode("CLAIM")],
                Effects: [],
                ReportRef: null!));

        Assert.Equal("ReportRef", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifierOutputs_ExposeImmutableClaimAndEffectSnapshots ()
    {
        var claim = new UcliCode("CLAIM");
        var replacementClaim = new UcliCode("REPLACEMENT");
        var effects = new[] { AssuranceEffect.UnityLifecycleRead };
        var build = new BuildVerifierOutput(BuildVerifierId, true, true, [claim], effects);
        var compile = new CompileVerifierOutput(
            CompileVerifierId,
            true,
            true,
            [claim],
            effects,
            AssuranceReportIds.CompileSummary);
        var ready = new ReadyVerifierOutput(ReadyVerifierId, true, true, [claim]);
        var verify = new VerifyVerifierOutput(VerifyVerifierId, AssuranceVerifierKind.PostRead, true, true, [claim], effects, null);

        var primaryClaims = new[]
        {
            build.PrimaryClaims,
            compile.PrimaryClaims,
            ready.PrimaryClaims,
            verify.PrimaryClaims,
        };
        var verifierEffects = new[]
        {
            build.Effects,
            compile.Effects,
            verify.Effects,
        };

        Assert.All(primaryClaims, values =>
        {
            var list = Assert.IsAssignableFrom<IList<UcliCode>>(values);
            Assert.Throws<NotSupportedException>(() => list[0] = replacementClaim);
            Assert.Equal(claim, values[0]);
        });
        Assert.All(verifierEffects, values =>
        {
            var list = Assert.IsAssignableFrom<IList<AssuranceEffect>>(values);
            Assert.Throws<NotSupportedException>(() => list[0] = AssuranceEffect.ProjectMutationAudit);
            Assert.Equal(AssuranceEffect.UnityLifecycleRead, values[0]);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ClaimOutputs_ExposeOwnedReadOnlyCollectionSnapshots ()
    {
        var subject = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["value"] = "original",
        };
        var compileOutput = AssuranceExecutionOutputTestFactory.CreateCompileOutput();
        var buildOutput = AssuranceExecutionOutputTestFactory.CreateBuildOutput();
        ReadyEvidenceOutput[] readyEvidence =
        [
            ReadyLifecycleEvidenceOutput.Create(
                AssuranceExecutionOutputTestFactory.CreateReadyLifecycleOutput()),
        ];
        var readyRisks = new[] { new ReadyResidualRiskOutput(new UcliCode("READY_RISK"), false, "risk") };
        CompileEvidenceOutput[] compileEvidence =
        [
            CompileScriptEvidenceOutput.Create(
                AssuranceReportIds.CompileSummary,
                compileOutput.ScriptCompilation),
        ];
        var compileRisks = new[] { new CompileResidualRiskOutput(new UcliCode("COMPILE_RISK"), false, "risk") };
        BuildEvidenceOutput[] buildEvidence =
        [
            BuildRunnerEvidenceOutput.Create(buildOutput.Runner),
        ];
        var buildRisks = new[] { new BuildResidualRiskOutput(new UcliCode("BUILD_RISK"), UcliDiagnosticSeverity.Warning, false, "risk") };
        VerifyEvidenceOutput[] verifyEvidence =
        [
            new VerifyTestSummaryEvidenceOutput(AssuranceReportIds.TestSummary),
        ];
        var verifyRisks = new[] { new VerifyResidualRiskOutput(new UcliCode("VERIFY_RISK"), false, "risk") };

        var ready = new ReadyClaimOutput(
            new UcliCode("READY_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            ReadyVerifierId,
            "statement",
            subject,
            ReadyClaimValidityOutput.ProbeOnly(),
            readyEvidence,
            readyRisks);
        var compile = new CompileClaimOutput(
            new UcliCode("COMPILE_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            CompileVerifierId,
            "statement",
            subject,
            compileEvidence,
            compileRisks);
        var build = new BuildClaimOutput(
            new UcliCode("BUILD_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            BuildVerifierId,
            "statement",
            subject,
            buildEvidence,
            buildRisks);
        var verify = new VerifyClaimOutput(
            new UcliCode("VERIFY_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            VerifyVerifierId,
            "statement",
            subject,
            null,
            verifyEvidence,
            verifyRisks);

        subject["value"] = "mutated";
        readyEvidence[0] = ReadyDecisionEvidenceOutput.Create(
            new ReadyDecisionEvidenceData(new UcliCode("NOT_READY"), "not ready"));
        readyRisks[0] = new ReadyResidualRiskOutput(new UcliCode("REPLACEMENT"), false, "replacement");
        compileEvidence[0] = CompileDomainReloadEvidenceOutput.Create(compileOutput.DomainReload);
        compileRisks[0] = new CompileResidualRiskOutput(new UcliCode("REPLACEMENT"), false, "replacement");
        buildEvidence[0] = BuildInputEvidenceOutput.Create(BuildServiceTestSupport.CreateInputProbe());
        buildRisks[0] = new BuildResidualRiskOutput(new UcliCode("REPLACEMENT"), UcliDiagnosticSeverity.Warning, false, "risk");
        verifyEvidence[0] = new VerifyFromResultMissingEvidenceOutput();
        verifyRisks[0] = new VerifyResidualRiskOutput(new UcliCode("REPLACEMENT"), false, "replacement");

        var snapshots = new (IReadOnlyDictionary<string, object?> Subject, IReadOnlyList<object> Evidence, IReadOnlyList<object> Risks)[]
        {
            (ready.Subject, ready.Evidence, ready.ResidualRisks),
            (compile.Subject, compile.Evidence, compile.ResidualRisks),
            (build.Subject, build.Evidence, build.ResidualRisks),
            (verify.Subject, verify.Evidence, verify.ResidualRisks),
        };
        Assert.All(snapshots, snapshot =>
        {
            Assert.Equal("original", snapshot.Subject["value"]);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Evidence)[0] = new object());
            Assert.Throws<NotSupportedException>(() => ((System.Collections.IList)snapshot.Risks)[0] = new object());
        });
        Assert.Equal(ReadyEvidenceKind.LifecycleSnapshot, Assert.IsType<ReadyLifecycleEvidenceOutput>(ready.Evidence[0]).Kind);
        Assert.Equal(new UcliCode("READY_RISK"), ready.ResidualRisks[0].Code);
        Assert.Equal(CompileEvidenceKind.ScriptCompilation, compile.Evidence[0].Kind);
        Assert.Equal(new UcliCode("COMPILE_RISK"), compile.ResidualRisks[0].Code);
        Assert.Equal(BuildEvidenceKind.BuildRunner, build.Evidence[0].Kind);
        Assert.Equal(new UcliCode("BUILD_RISK"), build.ResidualRisks[0].Code);
        Assert.Equal(VerifyEvidenceKind.TestSummary, verify.Evidence[0].Kind);
        Assert.Equal(new UcliCode("VERIFY_RISK"), verify.ResidualRisks[0].Code);
    }

    private static IReadOnlyDictionary<string, object?> EmptySubject ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
