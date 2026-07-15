using MackySoft.Ucli.Application.Features.Assurance.Verify.Input;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Execution.PostRead;

/// <summary> Builds post-read verifier claims from normalized <c>verify --from</c> input. </summary>
internal static class PostReadClaimBuilder
{
    internal static readonly AssuranceVerifierId VerifierId = new("postRead");

    private static readonly IReadOnlyList<VerifyResidualRiskOutput> EmptyResidualRisks =
        Array.Empty<VerifyResidualRiskOutput>();

    /// <summary> Builds the required missing-input claim for a profile that requires post-read verification. </summary>
    /// <returns> A post-read claim set containing one unverified claim. </returns>
    public static PostReadClaimSet BuildMissingInput ()
    {
        var claim = CreatePostReadClaim(
            VerifyClaimCodes.ReadSurfaceSafe,
            AssuranceClaimStatus.Unverified,
            AssuranceCoverage.None,
            required: true,
            "No --from input was provided for required post-read verification.",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["kind"] = "postRead",
            },
            []);
        return new PostReadClaimSet([claim], EmptyResidualRisks);
    }

    /// <summary> Builds post-read claims and residual risks from normalized <c>--from</c> input. </summary>
    /// <param name="fromInput"> The normalized <c>--from</c> input. </param>
    /// <param name="profileRequired"> Whether generated post-read claims are required by the active profile. </param>
    /// <returns> The generated post-read claim set. </returns>
    public static PostReadClaimSet Build (
        VerifyFromInput fromInput,
        bool profileRequired)
    {
        ArgumentNullException.ThrowIfNull(fromInput);

        var claims = new List<VerifyClaimOutput>();
        var residualRisks = new List<VerifyResidualRiskOutput>();
        var diagnostics = fromInput.OpResults.SelectMany(static result => result.Diagnostics).ToArray();
        var neutralEvidence = CreatePostReadEvidence(
            fromInput,
            diagnostics.Length,
            VerifyDiagnosticImpact.None);

        var persistenceResults = fromInput.OpResults
            .Where(static result => result.Changed
                && result.PostReadSource.PersistenceExpected)
            .ToArray();
        if (persistenceResults.Length != 0)
        {
            var persistenceDiagnostics = SelectDiagnostics(persistenceResults);
            var persistenceStatus = ResolveDiagnosticStatus(persistenceDiagnostics);
            var persistenceCoverage = ResolveDiagnosticCoverage(persistenceDiagnostics);
            var persistenceEvidence = CreatePostReadEvidence(fromInput, persistenceDiagnostics.Count, ResolveDiagnosticImpact(persistenceDiagnostics));
            var touchedCount = persistenceResults.Sum(static result => result.TouchedCount);
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PersistenceUnitTouched,
                touchedCount > 0 || persistenceStatus == AssuranceClaimStatus.Failed
                    ? persistenceStatus
                    : AssuranceClaimStatus.Indeterminate,
                touchedCount > 0 ? persistenceCoverage : AssuranceCoverage.None,
                required: profileRequired,
                touchedCount > 0
                    ? "Touched persistence units were observed from the input result."
                    : "Changed operations did not report touched persistence units.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["changedCount"] = persistenceResults.Length,
                    ["touchedCount"] = touchedCount,
                },
                persistenceEvidence));
        }

        var hasReadSurfaceClaim = fromInput.ReadPostconditionRequirementCount > 0;
        if (hasReadSurfaceClaim)
        {
            var readDiagnostics = diagnostics;
            var readStatus = ResolveDiagnosticStatus(readDiagnostics);
            var readCoverage = ResolveDiagnosticCoverage(readDiagnostics);
            var readEvidence = CreatePostReadEvidence(fromInput, readDiagnostics.Length, ResolveDiagnosticImpact(readDiagnostics));
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.ReadSurfaceSafe,
                readStatus,
                readCoverage,
                required: profileRequired,
                "Read-postcondition requirements were observed for affected read surfaces.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["requirementCount"] = fromInput.ReadPostconditionRequirementCount,
                },
                readEvidence));
        }

        var deterministicMutationResults = fromInput.OpResults
            .Where(static result =>
                (result.Applied || result.Changed)
                && IpcExecutePostReadSourceRules.IsDeterministicMutationSource(
                    result.PostReadSource.SourceKind,
                    result.PostReadSource.ExpectedPostState))
            .ToArray();
        var deterministicMutationCount = deterministicMutationResults.Length;
        if (deterministicMutationCount > 0)
        {
            var deterministicDiagnostics = SelectDiagnostics(deterministicMutationResults);
            var deterministicStatus = ResolveDiagnosticStatus(deterministicDiagnostics);
            var deterministicCoverage = ResolveDiagnosticCoverage(deterministicDiagnostics);
            var deterministicEvidence = CreatePostReadEvidence(fromInput, deterministicDiagnostics.Count, ResolveDiagnosticImpact(deterministicDiagnostics));
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PostMutationObserved,
                deterministicStatus,
                deterministicCoverage,
                required: profileRequired,
                "Deterministic post-mutation state was observed from the input result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["observedMutationCount"] = deterministicMutationCount,
                },
                deterministicEvidence));
        }
        else if (fromInput.OpResults.Any(static result => result.Applied || result.Changed))
        {
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PostMutationObserved,
                AssuranceClaimStatus.OutOfScope,
                AssuranceCoverage.None,
                required: false,
                "Expected post-mutation state is not deterministic from the input result alone.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["reason"] = "expectedPostStateUnavailable",
                },
                neutralEvidence));
        }

        if (HasUnboundDiagnosticImpact(fromInput.OpResults, persistenceResults, deterministicMutationResults, hasReadSurfaceClaim))
        {
            residualRisks.Add(new VerifyResidualRiskOutput(
                VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value,
                Blocking: true)
            {
                Message = "Input diagnostics affected coverage but no generated post-read claim could carry that diagnostic impact.",
            });
        }

        if (claims.Count == 0 && profileRequired)
        {
            claims.Add(CreatePostReadClaim(
                VerifyClaimCodes.PostMutationObserved,
                AssuranceClaimStatus.Unverified,
                AssuranceCoverage.None,
                required: true,
                "No mutation completion evidence was available in the input result.",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["kind"] = "postRead",
                    ["reason"] = "noMutationEvidence",
                },
                neutralEvidence));
        }

        return new PostReadClaimSet(claims, residualRisks);
    }

    private static IReadOnlyList<VerifyFromDiagnostic> SelectDiagnostics (IReadOnlyList<VerifyFromOperationResult> results)
    {
        return results.SelectMany(static result => result.Diagnostics).ToArray();
    }

    private static bool HasUnboundDiagnosticImpact (
        IReadOnlyList<VerifyFromOperationResult> opResults,
        IReadOnlyList<VerifyFromOperationResult> persistenceResults,
        IReadOnlyList<VerifyFromOperationResult> deterministicMutationResults,
        bool hasReadSurfaceClaim)
    {
        if (hasReadSurfaceClaim)
        {
            return false;
        }

        var boundOpIds = new HashSet<IpcExecuteStepId>();
        foreach (var result in persistenceResults)
        {
            boundOpIds.Add(result.OpId);
        }

        foreach (var result in deterministicMutationResults)
        {
            boundOpIds.Add(result.OpId);
        }

        return opResults.Any(result =>
            !boundOpIds.Contains(result.OpId)
            && ResolveDiagnosticImpact(result.Diagnostics) != VerifyDiagnosticImpact.None);
    }

    private static VerifyEvidenceOutput[] CreatePostReadEvidence (
        VerifyFromInput fromInput,
        int diagnosticCount,
        VerifyDiagnosticImpact diagnosticImpact)
    {
        return
        [
            new VerifyEvidenceOutput("fromResultSummary")
            {
                Data = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["command"] = fromInput.Command,
                    ["opResultCount"] = fromInput.OpResults.Count,
                    ["changedCount"] = fromInput.OpResults.Count(static result => result.Changed),
                    ["touchedCount"] = fromInput.OpResults.Sum(static result => result.TouchedCount),
                    ["diagnosticCount"] = diagnosticCount,
                    ["diagnosticImpact"] = diagnosticImpact,
                },
            },
        ];
    }

    private static VerifyClaimOutput CreatePostReadClaim (
        UcliCode id,
        AssuranceClaimStatus status,
        AssuranceCoverage coverage,
        bool required,
        string statement,
        IReadOnlyDictionary<string, object?> subject,
        IReadOnlyList<VerifyEvidenceOutput> evidence)
    {
        return new VerifyClaimOutput(
            Id: id,
            Status: status,
            Coverage: coverage,
            Required: required,
            VerifierRef: VerifierId,
            Statement: statement,
            Subject: subject,
            Evidence: evidence,
            ResidualRisks: EmptyResidualRisks);
    }

    private static AssuranceClaimStatus ResolveDiagnosticStatus (IReadOnlyList<VerifyFromDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == UcliDiagnosticSeverity.Error))
        {
            return AssuranceClaimStatus.Failed;
        }

        return diagnostics.Any(static diagnostic => diagnostic.CoverageImpact == IpcExecuteDiagnosticCoverageImpact.Indeterminate)
            ? AssuranceClaimStatus.Indeterminate
            : AssuranceClaimStatus.Passed;
    }

    private static AssuranceCoverage ResolveDiagnosticCoverage (IReadOnlyList<VerifyFromDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => diagnostic.CoverageImpact == IpcExecuteDiagnosticCoverageImpact.Indeterminate))
        {
            return AssuranceCoverage.None;
        }

        return diagnostics.Any(static diagnostic => diagnostic.CoverageImpact == IpcExecuteDiagnosticCoverageImpact.Partial)
            ? AssuranceCoverage.Partial
            : AssuranceCoverage.Full;
    }

    private static VerifyDiagnosticImpact ResolveDiagnosticImpact (IReadOnlyList<VerifyFromDiagnostic> diagnostics)
    {
        if (diagnostics.Any(static diagnostic => diagnostic.Severity == UcliDiagnosticSeverity.Error))
        {
            return VerifyDiagnosticImpact.Error;
        }

        if (diagnostics.Any(static diagnostic => diagnostic.CoverageImpact == IpcExecuteDiagnosticCoverageImpact.Indeterminate))
        {
            return VerifyDiagnosticImpact.Indeterminate;
        }

        return diagnostics.Any(static diagnostic => diagnostic.CoverageImpact == IpcExecuteDiagnosticCoverageImpact.Partial)
            ? VerifyDiagnosticImpact.Partial
            : VerifyDiagnosticImpact.None;
    }
}
