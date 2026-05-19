using MackySoft.Ucli.Application.Features.Assurance.Verify.Execution.PostRead;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.Execution.PostRead;

public sealed class PostReadClaimBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildMissingInput_ReturnsRequiredUnverifiedReadSurfaceClaim ()
    {
        var claimSet = PostReadClaimBuilder.BuildMissingInput();

        var claim = Assert.Single(claimSet.Claims);
        Assert.Empty(claimSet.ResidualRisks);
        Assert.Equal(VerifyClaimCodes.ReadSurfaceSafe.Value, claim.Id);
        Assert.Equal(VerifyClaimStatusValues.Unverified, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
        Assert.True(claim.Required);
        Assert.Equal(PostReadClaimBuilder.VerifierId, claim.VerifierRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPersistenceTouched_ReturnsPassedRequiredClaim ()
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: IpcExecutePostReadSourceKindNames.Refresh,
            commit: null,
            persistenceExpected: true,
            expectedPostState: IpcExecuteExpectedPostStateNames.Unavailable,
            touchedCount: 1,
            op: UcliPrimitiveOperationNames.ProjectRefresh));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.PersistenceUnitTouched.Value, StringComparison.Ordinal));
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Full, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPersistenceExpectedAndNoTouchedUnits_ReturnsIndeterminateClaim ()
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: IpcExecutePostReadSourceKindNames.Refresh,
            commit: null,
            persistenceExpected: true,
            expectedPostState: IpcExecuteExpectedPostStateNames.Unavailable,
            touchedCount: 0,
            op: UcliPrimitiveOperationNames.ProjectRefresh));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.PersistenceUnitTouched.Value, StringComparison.Ordinal));
        Assert.Equal(VerifyClaimStatusValues.Indeterminate, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithReadPostconditionRequirement_ReturnsReadSurfaceClaim ()
    {
        var input = CreateInput(readPostconditionRequirementCount: 1);

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims);
        Assert.Equal(VerifyClaimCodes.ReadSurfaceSafe.Value, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Full, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithDeterministicEdit_ReturnsPostMutationObservedClaim ()
    {
        var input = CreateInput(CreateOperationResult(
            persistenceExpected: false,
            touchedCount: 0));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims);
        Assert.Equal(VerifyClaimCodes.PostMutationObserved.Value, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Full, claim.Coverage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcExecutePostReadSourceKindNames.Operation, null, false, UcliPrimitiveOperationNames.SceneOpen)]
    [InlineData(IpcExecutePostReadSourceKindNames.Refresh, null, true, UcliPrimitiveOperationNames.ProjectRefresh)]
    public void Build_WithUnavailablePostState_ReturnsOutOfScopePostMutationClaim (
        string sourceKind,
        string? commit,
        bool persistenceExpected,
        string op)
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: sourceKind,
            commit: commit,
            persistenceExpected: persistenceExpected,
            expectedPostState: IpcExecuteExpectedPostStateNames.Unavailable,
            touchedCount: 0,
            op: op));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.PostMutationObserved.Value, StringComparison.Ordinal));
        Assert.False(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.OutOfScope, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithNoMutationEvidenceAndRequiredProfile_ReturnsUnverifiedClaim ()
    {
        var input = CreateInput();

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims);
        Assert.Equal(VerifyClaimCodes.PostMutationObserved.Value, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Unverified, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcExecuteDiagnosticCoverageImpactNames.Partial, VerifyClaimStatusValues.Passed, VerifyCoverageValues.Partial)]
    [InlineData(IpcExecuteDiagnosticCoverageImpactNames.Indeterminate, VerifyClaimStatusValues.Indeterminate, VerifyCoverageValues.None)]
    public void Build_WithCoverageDiagnostic_MapsClaimStatusAndCoverage (
        string coverageImpact,
        string expectedStatus,
        string expectedCoverage)
    {
        var input = CreateInput(
            CreateOperationResult(diagnostics: [CreateDiagnostic(coverageImpact)]),
            readPostconditionRequirementCount: 1);

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.ReadSurfaceSafe.Value, StringComparison.Ordinal));
        Assert.Equal(expectedStatus, claim.Status);
        Assert.Equal(expectedCoverage, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithUnboundCoverageDiagnostic_ReturnsBlockingResidualRisk ()
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: IpcExecutePostReadSourceKindNames.Operation,
            commit: null,
            persistenceExpected: false,
            expectedPostState: IpcExecuteExpectedPostStateNames.Unavailable,
            touchedCount: 0,
            diagnostics: [CreateDiagnostic(IpcExecuteDiagnosticCoverageImpactNames.Partial)],
            op: UcliPrimitiveOperationNames.SceneOpen));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: false);

        var risk = Assert.Single(claimSet.ResidualRisks);
        Assert.Equal(VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value, risk.Code);
        Assert.True(risk.Blocking);
    }

    private static VerifyFromInput CreateInput (
        params VerifyFromOperationResult[] opResults)
    {
        return CreateInput(opResults, readPostconditionRequirementCount: 0);
    }

    private static VerifyFromInput CreateInput (int readPostconditionRequirementCount)
    {
        return CreateInput(Array.Empty<VerifyFromOperationResult>(), readPostconditionRequirementCount);
    }

    private static VerifyFromInput CreateInput (
        VerifyFromOperationResult opResult,
        int readPostconditionRequirementCount)
    {
        return CreateInput([opResult], readPostconditionRequirementCount);
    }

    private static VerifyFromInput CreateInput (
        IReadOnlyList<VerifyFromOperationResult> opResults,
        int readPostconditionRequirementCount)
    {
        return new VerifyFromInput(
            Command: UcliCommandIds.Call,
            ProjectFingerprint: "project-fingerprint",
            OpResults: opResults,
            ReadPostconditionRequirementCount: readPostconditionRequirementCount);
    }

    private static VerifyFromOperationResult CreateOperationResult (
        string sourceKind = IpcExecutePostReadSourceKindNames.Edit,
        string? commit = IpcExecutePostReadCommitNames.None,
        bool persistenceExpected = false,
        string expectedPostState = IpcExecuteExpectedPostStateNames.Deterministic,
        bool applied = true,
        bool changed = true,
        int touchedCount = 0,
        IReadOnlyList<VerifyFromDiagnostic>? diagnostics = null,
        string op = "edit")
    {
        return new VerifyFromOperationResult(
            OpId: "op-1",
            Op: op,
            Applied: applied,
            Changed: changed,
            TouchedCount: touchedCount,
            Diagnostics: diagnostics ?? Array.Empty<VerifyFromDiagnostic>(),
            PostReadSource: new VerifyFromPostReadSourceStep(
                OpId: "op-1",
                SourceKind: sourceKind,
                PlayModeMutation: false,
                Commit: commit,
                PersistenceExpected: persistenceExpected,
                ExpectedPostState: expectedPostState));
    }

    private static VerifyFromDiagnostic CreateDiagnostic (string coverageImpact)
    {
        return new VerifyFromDiagnostic(
            Code: "READ_SURFACE_PARTIAL",
            Severity: IpcExecuteDiagnosticSeverityNames.Warning,
            CoverageImpact: coverageImpact,
            Message: "Read surface coverage is partial.");
    }
}
