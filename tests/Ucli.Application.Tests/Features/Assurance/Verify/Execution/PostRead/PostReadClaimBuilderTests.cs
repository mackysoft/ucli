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
        Assert.Equal(VerifyClaimCodes.ReadSurfaceSafe, claim.Id);
        Assert.Equal(AssuranceClaimStatus.Unverified, claim.Status);
        Assert.Equal(AssuranceCoverage.None, claim.Coverage);
        Assert.True(claim.Required);
        Assert.Equal(PostReadClaimBuilder.VerifierId, claim.VerifierRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPersistenceTouched_ReturnsPassedRequiredClaim ()
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: IpcExecutePostReadSourceKind.Refresh,
            commit: null,
            persistenceExpected: true,
            expectedPostState: IpcExecuteExpectedPostState.Unavailable,
            touchedCount: 1,
            op: UcliPrimitiveOperationNames.ProjectRefresh));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PersistenceUnitTouched);
        Assert.True(claim.Required);
        Assert.Equal(AssuranceClaimStatus.Passed, claim.Status);
        Assert.Equal(AssuranceCoverage.Full, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPersistenceExpectedAndNoTouchedUnits_ReturnsIndeterminateClaim ()
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: IpcExecutePostReadSourceKind.Refresh,
            commit: null,
            persistenceExpected: true,
            expectedPostState: IpcExecuteExpectedPostState.Unavailable,
            touchedCount: 0,
            op: UcliPrimitiveOperationNames.ProjectRefresh));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PersistenceUnitTouched);
        Assert.Equal(AssuranceClaimStatus.Indeterminate, claim.Status);
        Assert.Equal(AssuranceCoverage.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithReadPostconditionRequirement_ReturnsReadSurfaceClaim ()
    {
        var input = CreateInput(readPostconditionRequirementCount: 1);

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims);
        Assert.Equal(VerifyClaimCodes.ReadSurfaceSafe, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(AssuranceClaimStatus.Passed, claim.Status);
        Assert.Equal(AssuranceCoverage.Full, claim.Coverage);
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
        Assert.Equal(VerifyClaimCodes.PostMutationObserved, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(AssuranceClaimStatus.Passed, claim.Status);
        Assert.Equal(AssuranceCoverage.Full, claim.Coverage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(IpcExecutePostReadSourceKind.Operation, null, false, UcliPrimitiveOperationNames.SceneOpen)]
    [InlineData(IpcExecutePostReadSourceKind.Refresh, null, true, UcliPrimitiveOperationNames.ProjectRefresh)]
    public void Build_WithUnavailablePostState_ReturnsOutOfScopePostMutationClaim (
        IpcExecutePostReadSourceKind sourceKind,
        IpcExecutePostReadCommit? commit,
        bool persistenceExpected,
        string op)
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: sourceKind,
            commit: commit,
            persistenceExpected: persistenceExpected,
            expectedPostState: IpcExecuteExpectedPostState.Unavailable,
            touchedCount: 0,
            op: op));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PostMutationObserved);
        Assert.False(claim.Required);
        Assert.Equal(AssuranceClaimStatus.OutOfScope, claim.Status);
        Assert.Equal(AssuranceCoverage.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayModeLiveMutation_ReturnsOutOfScopePostMutationClaim ()
    {
        var input = CreateInput(CreateOperationResult(
            playModeMutation: true,
            persistenceExpected: false,
            expectedPostState: IpcExecuteExpectedPostState.Unavailable,
            touchedCount: 0));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PostMutationObserved);
        Assert.False(claim.Required);
        Assert.Equal(AssuranceClaimStatus.OutOfScope, claim.Status);
        Assert.Equal(AssuranceCoverage.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayModeLiveMutationAndPersistenceExpected_DoesNotCountAsDeterministic ()
    {
        var input = CreateInput(CreateOperationResult(
            playModeMutation: true,
            persistenceExpected: true,
            expectedPostState: IpcExecuteExpectedPostState.Unavailable,
            touchedCount: 1));

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var persistenceClaim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PersistenceUnitTouched);
        Assert.Equal(AssuranceClaimStatus.Passed, persistenceClaim.Status);
        var postMutationClaim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PostMutationObserved);
        Assert.False(postMutationClaim.Required);
        Assert.Equal(AssuranceClaimStatus.OutOfScope, postMutationClaim.Status);
        Assert.Equal(AssuranceCoverage.None, postMutationClaim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithPlayModeLiveMutationAndPersistentMutation_DoesNotCountLiveMutationAsDeterministic ()
    {
        var input = CreateInput([
            CreateOperationResult(
                opId: "live",
                playModeMutation: true,
                persistenceExpected: false,
                expectedPostState: IpcExecuteExpectedPostState.Unavailable,
                touchedCount: 0),
            CreateOperationResult(
                opId: "persistent",
                persistenceExpected: true,
                expectedPostState: IpcExecuteExpectedPostState.Deterministic,
                touchedCount: 1),
        ]);

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var persistenceClaim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PersistenceUnitTouched);
        Assert.Equal(AssuranceClaimStatus.Passed, persistenceClaim.Status);
        var postMutationClaim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.PostMutationObserved);
        Assert.Equal(AssuranceClaimStatus.Passed, postMutationClaim.Status);
        Assert.Equal(1, postMutationClaim.Subject["observedMutationCount"]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithNoMutationEvidenceAndRequiredProfile_ReturnsUnverifiedClaim ()
    {
        var input = CreateInput();

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims);
        Assert.Equal(VerifyClaimCodes.PostMutationObserved, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(AssuranceClaimStatus.Unverified, claim.Status);
        Assert.Equal(AssuranceCoverage.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithCoverageDiagnostic_MapsClaimStatusAndCoverage ()
    {
        var testCases = new[]
        {
            (IpcExecuteDiagnosticCoverageImpact.Partial, AssuranceClaimStatus.Passed, AssuranceCoverage.Partial),
            (IpcExecuteDiagnosticCoverageImpact.Indeterminate, AssuranceClaimStatus.Indeterminate, AssuranceCoverage.None),
        };

        foreach (var (coverageImpact, expectedStatus, expectedCoverage) in testCases)
        {
            var input = CreateInput(
                CreateOperationResult(diagnostics: [CreateDiagnostic(coverageImpact)]),
                readPostconditionRequirementCount: 1);

            var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

            var claim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.ReadSurfaceSafe);
            Assert.Equal(expectedStatus, claim.Status);
            Assert.Equal(expectedCoverage, claim.Coverage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithHierarchyPathUnrepresentableDiagnostic_ReturnsPartialReadSurfaceClaim ()
    {
        var input = CreateInput(
            CreateOperationResult(diagnostics:
            [
                new VerifyFromDiagnostic(
                    Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                    Severity: UcliDiagnosticSeverity.Warning,
                    CoverageImpact: IpcExecuteDiagnosticCoverageImpact.Partial,
                    Message: "Hierarchy paths cannot represent every object.")
            ]),
            readPostconditionRequirementCount: 1);

        var claimSet = PostReadClaimBuilder.Build(input, profileRequired: true);

        var claim = Assert.Single(claimSet.Claims, static claim => claim.Id == VerifyClaimCodes.ReadSurfaceSafe);
        Assert.Equal(AssuranceClaimStatus.Passed, claim.Status);
        Assert.Equal(AssuranceCoverage.Partial, claim.Coverage);
        Assert.Empty(claim.ResidualRisks);
        Assert.Empty(claimSet.ResidualRisks);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WithUnboundCoverageDiagnostic_ReturnsBlockingResidualRisk ()
    {
        var input = CreateInput(CreateOperationResult(
            sourceKind: IpcExecutePostReadSourceKind.Operation,
            commit: null,
            persistenceExpected: false,
            expectedPostState: IpcExecuteExpectedPostState.Unavailable,
            touchedCount: 0,
            diagnostics: [CreateDiagnostic(IpcExecuteDiagnosticCoverageImpact.Partial)],
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
            Command: UcliCommandIds.Call.Name,
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            OpResults: opResults,
            ReadPostconditionRequirementCount: readPostconditionRequirementCount);
    }

    private static VerifyFromOperationResult CreateOperationResult (
        IpcExecutePostReadSourceKind sourceKind = IpcExecutePostReadSourceKind.Edit,
        IpcExecutePostReadCommit? commit = IpcExecutePostReadCommit.None,
        bool persistenceExpected = false,
        IpcExecuteExpectedPostState expectedPostState = IpcExecuteExpectedPostState.Deterministic,
        bool playModeMutation = false,
        bool applied = true,
        bool changed = true,
        int touchedCount = 0,
        IReadOnlyList<VerifyFromDiagnostic>? diagnostics = null,
        string op = "edit",
        string opId = "op-1")
    {
        var executeStepId = new IpcExecuteStepId(opId);

        return new VerifyFromOperationResult(
            OpId: executeStepId,
            Op: op,
            Applied: applied,
            Changed: changed,
            TouchedCount: touchedCount,
            Diagnostics: diagnostics ?? Array.Empty<VerifyFromDiagnostic>(),
            PostReadSource: new VerifyFromPostReadSourceStep(
                OpId: executeStepId,
                SourceKind: sourceKind,
                PlayModeMutation: playModeMutation,
                Commit: commit,
                PersistenceExpected: persistenceExpected,
                ExpectedPostState: expectedPostState));
    }

    private static VerifyFromDiagnostic CreateDiagnostic (IpcExecuteDiagnosticCoverageImpact coverageImpact)
    {
        return new VerifyFromDiagnostic(
            Code: new UcliCode("READ_SURFACE_PARTIAL"),
            Severity: UcliDiagnosticSeverity.Warning,
            CoverageImpact: coverageImpact,
            Message: "Read surface coverage is partial.");
    }
}
