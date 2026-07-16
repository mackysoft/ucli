using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
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
        var build = new BuildVerifierOutput(BuildVerifierId, true, true, [], [], BuildArtifactKind.Build);
        var compile = new CompileVerifierOutput(CompileVerifierId, true, true, [], [], "compile");
        var ready = new ReadyVerifierOutput(ReadyVerifierId, true, true, []);

        Assert.Equal(AssuranceVerifierKind.Build, build.Kind);
        Assert.Equal(AssuranceVerifierKind.Compile, compile.Kind);
        Assert.Equal(AssuranceVerifierKind.Ready, ready.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyVerifierOutput_ExposesItsFixedEmptyEffectSetAsTypedValues ()
    {
        var ready = new ReadyVerifierOutput(ReadyVerifierId, true, true, []);

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
            static () => new ReadyClaimOutput(new UcliCode("READY_CLAIM"), InvalidStatus, AssuranceCoverage.Full, true, ReadyVerifierId, "statement", EmptySubject(), new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, false), [], []),
            static () => new VerifyClaimOutput(new UcliCode("VERIFY_CLAIM"), InvalidStatus, AssuranceCoverage.Full, true, VerifyVerifierId, "statement", EmptySubject(), [], []),
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
            static () => new ReadyClaimOutput(new UcliCode("READY_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, ReadyVerifierId, "statement", EmptySubject(), new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, false), [], []),
            static () => new VerifyClaimOutput(new UcliCode("VERIFY_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, VerifyVerifierId, "statement", EmptySubject(), [], []),
            static () => new BuildClaimOutput(new UcliCode("BUILD_CLAIM"), AssuranceClaimStatus.Passed, InvalidCoverage, true, BuildVerifierId, "statement", EmptySubject(), [], []),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal("Coverage", Assert.Throws<ArgumentOutOfRangeException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithUndefinedVerdict_ThrowArgumentOutOfRangeException ()
    {
        const AssuranceVerdict InvalidVerdict = (AssuranceVerdict)0;
        var constructors = new Action[]
        {
            static () => new BuildExecutionOutput(InvalidVerdict, null!, null!, null!, null!, null!, null!),
            static () => new CompileExecutionOutput(InvalidVerdict, null!, null!, null!, null!, null!, AssuranceRequestedExecutionMode.Auto, AssuranceResolvedExecutionMode.Oneshot, default, 0, null!),
            static () => new ReadyExecutionOutput(InvalidVerdict, null!, null!, null!, null!, null!, ReadyTarget.Execution, AssuranceRequestedExecutionMode.Auto, AssuranceResolvedExecutionMode.Oneshot, default, 0, null, null),
            static () => new VerifyExecutionOutput(InvalidVerdict, null!, null!, null!, null!, null!, null!, 0),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal("Verdict", Assert.Throws<ArgumentOutOfRangeException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithUndefinedSessionKind_ThrowArgumentOutOfRangeException ()
    {
        var constructors = new Action[]
        {
            static () => new CompileExecutionOutput(AssuranceVerdict.Pass, null!, null!, null!, null!, null!, AssuranceRequestedExecutionMode.Auto, AssuranceResolvedExecutionMode.Oneshot, default, 0, null!),
            static () => new ReadyExecutionOutput(AssuranceVerdict.Pass, null!, null!, null!, null!, null!, ReadyTarget.Execution, AssuranceRequestedExecutionMode.Auto, AssuranceResolvedExecutionMode.Oneshot, default, 0, null, null),
        };

        Assert.All(
            constructors,
            constructor => Assert.Equal("SessionKind", Assert.Throws<ArgumentOutOfRangeException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExecutionOutputs_WithUndefinedExecutionMode_ThrowArgumentOutOfRangeException ()
    {
        var constructors = new (Action Construct, string ParameterName)[]
        {
            (static () => new CompileExecutionOutput(AssuranceVerdict.Pass, null!, null!, null!, null!, null!, default, AssuranceResolvedExecutionMode.Oneshot, AssuranceSessionKind.TransientProbe, 0, null!), "RequestedMode"),
            (static () => new CompileExecutionOutput(AssuranceVerdict.Pass, null!, null!, null!, null!, null!, AssuranceRequestedExecutionMode.Auto, default, AssuranceSessionKind.TransientProbe, 0, null!), "ResolvedMode"),
            (static () => new ReadyExecutionOutput(AssuranceVerdict.Pass, null!, null!, null!, null!, null!, ReadyTarget.Execution, default, AssuranceResolvedExecutionMode.Oneshot, AssuranceSessionKind.TransientProbe, 0, null, null), "RequestedMode"),
            (static () => new ReadyExecutionOutput(AssuranceVerdict.Pass, null!, null!, null!, null!, null!, ReadyTarget.Execution, AssuranceRequestedExecutionMode.Auto, default, AssuranceSessionKind.TransientProbe, 0, null, null), "ResolvedMode"),
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
            static () => new ReadyClaimOutput(null!, AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, ReadyVerifierId, "statement", EmptySubject(), new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, false), [], []),
            static () => new VerifyClaimOutput(null!, AssuranceClaimStatus.Passed, AssuranceCoverage.Full, true, VerifyVerifierId, "statement", EmptySubject(), [], []),
        };

        Assert.All(constructors, constructor => Assert.Equal("Id", Assert.Throws<ArgumentNullException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifierOutputs_WithNullPrimaryClaims_ThrowArgumentNullException ()
    {
        var constructors = new Action[]
        {
            static () => new BuildVerifierOutput(BuildVerifierId, true, true, null!, [], BuildArtifactKind.Build),
            static () => new CompileVerifierOutput(CompileVerifierId, true, true, null!, [], "compile"),
            static () => new ReadyVerifierOutput(ReadyVerifierId, true, true, null!),
            static () => new VerifyVerifierOutput(VerifyVerifierId, AssuranceVerifierKind.Ready, true, true, null!, []),
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
            () => new BuildVerifierOutput(BuildVerifierId, true, true, invalidClaims, [], BuildArtifactKind.Build),
            () => new CompileVerifierOutput(CompileVerifierId, true, true, invalidClaims, [], "compile"),
            () => new ReadyVerifierOutput(ReadyVerifierId, true, true, invalidClaims),
            () => new VerifyVerifierOutput(VerifyVerifierId, AssuranceVerifierKind.Ready, true, true, invalidClaims, []),
        };

        Assert.All(constructors, constructor => Assert.Equal("PrimaryClaims", Assert.Throws<ArgumentException>(constructor).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void VerifierOutputs_ExposeImmutableClaimAndEffectSnapshots ()
    {
        var claim = new UcliCode("CLAIM");
        var replacementClaim = new UcliCode("REPLACEMENT");
        var effects = new[] { AssuranceEffect.UnityLifecycleRead };
        var build = new BuildVerifierOutput(BuildVerifierId, true, true, [claim], effects, BuildArtifactKind.Build);
        var compile = new CompileVerifierOutput(CompileVerifierId, true, true, [claim], effects, "compile");
        var ready = new ReadyVerifierOutput(ReadyVerifierId, true, true, [claim]);
        var verify = new VerifyVerifierOutput(VerifyVerifierId, AssuranceVerifierKind.PostRead, true, true, [claim], effects);

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
            Assert.Same(claim, values[0]);
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
        var readyEvidence = new[] { new ReadyEvidenceOutput("ready") };
        var readyRisks = new[] { new ReadyResidualRiskOutput("READY_RISK", false) };
        var compileEvidence = new[] { new CompileEvidenceOutput(CompileEvidenceKind.ScriptCompilation, null, null) };
        var compileRisks = new[] { new CompileResidualRiskOutput("COMPILE_RISK", false) };
        var buildEvidence = new[] { new BuildEvidenceOutput("build", EvidenceRef: null, Data: null) };
        var buildRisks = new[] { new BuildResidualRiskOutput("BUILD_RISK", UcliDiagnosticSeverity.Warning, false, "risk") };
        var verifyEvidence = new[] { new VerifyEvidenceOutput("verify") };
        var verifyRisks = new[] { new VerifyResidualRiskOutput("VERIFY_RISK", false) };

        var ready = new ReadyClaimOutput(
            new UcliCode("READY_CLAIM"),
            AssuranceClaimStatus.Passed,
            AssuranceCoverage.Full,
            true,
            ReadyVerifierId,
            "statement",
            subject,
            new ReadyClaimValidityOutput(ReadyValidityKind.ProbeOnly, false),
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
            verifyEvidence,
            verifyRisks);

        subject["value"] = "mutated";
        readyEvidence[0] = new ReadyEvidenceOutput("replacement");
        readyRisks[0] = new ReadyResidualRiskOutput("REPLACEMENT", false);
        compileEvidence[0] = new CompileEvidenceOutput(CompileEvidenceKind.DomainReload, null, null);
        compileRisks[0] = new CompileResidualRiskOutput("REPLACEMENT", false);
        buildEvidence[0] = new BuildEvidenceOutput("replacement", EvidenceRef: null, Data: null);
        buildRisks[0] = new BuildResidualRiskOutput("REPLACEMENT", UcliDiagnosticSeverity.Warning, false, "risk");
        verifyEvidence[0] = new VerifyEvidenceOutput("replacement");
        verifyRisks[0] = new VerifyResidualRiskOutput("REPLACEMENT", false);

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
        Assert.Equal("ready", Assert.IsType<ReadyEvidenceOutput>(ready.Evidence[0]).Kind);
        Assert.Equal("READY_RISK", ready.ResidualRisks[0].Code);
        Assert.Equal(CompileEvidenceKind.ScriptCompilation, compile.Evidence[0].Kind);
        Assert.Equal("COMPILE_RISK", compile.ResidualRisks[0].Code);
        Assert.Equal("build", build.Evidence[0].Kind);
        Assert.Equal("BUILD_RISK", build.ResidualRisks[0].Code);
        Assert.Equal("verify", verify.Evidence[0].Kind);
        Assert.Equal("VERIFY_RISK", verify.ResidualRisks[0].Code);
    }

    private static IReadOnlyDictionary<string, object?> EmptySubject ()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
