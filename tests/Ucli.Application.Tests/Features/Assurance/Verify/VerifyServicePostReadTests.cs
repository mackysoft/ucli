using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyServicePostReadTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithPostReadPartialDiagnostics_ReturnsPartialOptionalClaimWithoutLogs ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithPostReadPartialDiagnostics_ReturnsPartialOptionalClaimWithoutLogs));
        var fromPath = scope.WriteFile("from.json", CreateFromJson("project-fingerprint", coverageImpact: "partial"));
        var logsService = new RecordingVerifyLogsUnityService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                    CreateLogEvent("cursor-1"),
                    "cursor-1",
                    cancellationToken)
                .ConfigureAwait(false);
            await onEvent(
                    CreateLogEvent("cursor-2"),
                    "cursor-2",
                    cancellationToken)
                .ConfigureAwait(false);
            return LogsReadServiceResult.Success();
        });
        var service = CreateService(scope.FullPath, logsService: logsService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        VerifyServiceAssert.PartialReadSurfaceClaimReturnedWithoutLogs(
            result,
            logsService);
    }


    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithRequiredPartialCoverageClaim_CollectsLogsEvidence ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithRequiredPartialCoverageClaim_CollectsLogsEvidence));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "steps": [
                {
                  "kind": "postRead",
                  "required": true
                },
                {
                  "kind": "logs",
                  "required": false
                }
              ]
            }
            """);
        var fromPath = scope.WriteFile("from.json", CreateFromJson("project-fingerprint", coverageImpact: "partial"));
        var logsService = new RecordingVerifyLogsUnityService(async (_, onEvent, cancellationToken) =>
        {
            await onEvent(
                    CreateLogEvent("cursor-1"),
                    "cursor-1",
                    cancellationToken)
                .ConfigureAwait(false);
            await onEvent(
                    CreateLogEvent("cursor-2"),
                    "cursor-2",
                    cancellationToken)
                .ConfigureAwait(false);
            return LogsReadServiceResult.Success();
        });
        var service = CreateService(scope.FullPath, logsService: logsService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        VerifyServiceAssert.RequiredPartialCoverageCollectedLogsEvidence(
            result,
            logsService);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithPostReadErrorDiagnostic_ReturnsFailedClaim ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithPostReadErrorDiagnostic_ReturnsFailedClaim));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile(
            "from.json",
            CreateFromJson(
                "project-fingerprint",
                coverageImpact: "none",
                severity: "error"));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Fail, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.ReadSurfaceSafe.Value, StringComparison.Ordinal));
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Failed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Full, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithDeterministicPostState_ReturnsRequiredObservedClaim ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithDeterministicPostState_ReturnsRequiredObservedClaim));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile(
            "from.json",
            CreateFromJson(
                "project-fingerprint",
                coverageImpact: "none",
                includeReadPostcondition: false));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Pass, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.PostMutationObserved.Value, StringComparison.Ordinal));
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Full, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithRawOperationPostStateUnavailable_ReturnsOutOfScopeClaim ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithRawOperationPostStateUnavailable_ReturnsOutOfScopeClaim));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile(
            "from.json",
            CreateFromJson(
                "project-fingerprint",
                coverageImpact: "none",
                touchedJson: "[]",
                sourceKind: "operation",
                commit: null,
                persistenceExpected: false,
                expectedPostState: "unavailable",
                includeReadPostcondition: false,
                op: UcliPrimitiveOperationNames.SceneOpen));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Pass, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims);
        Assert.Equal(VerifyClaimCodes.PostMutationObserved.Value, claim.Id);
        Assert.False(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.OutOfScope, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithNoOpRequiredPostRead_ReturnsIncompleteUnverifiedClaim ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithNoOpRequiredPostRead_ReturnsIncompleteUnverifiedClaim));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile(
            "from.json",
            CreateFromJson(
                "project-fingerprint",
                coverageImpact: "none",
                applied: false,
                changed: false,
                touchedJson: "[]",
                includeReadPostcondition: false));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Incomplete, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims);
        Assert.Equal(VerifyClaimCodes.PostMutationObserved.Value, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Unverified, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithEmptyNoOpRequiredPostRead_ReturnsIncompleteUnverifiedClaim ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithEmptyNoOpRequiredPostRead_ReturnsIncompleteUnverifiedClaim));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile("from.json", CreateNoOpFromJson("project-fingerprint"));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Incomplete, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims);
        Assert.Equal(VerifyClaimCodes.PostMutationObserved.Value, claim.Id);
        Assert.True(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Unverified, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithIndeterminateDiagnostic_ReturnsIndeterminateClaimCoverageNone ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithIndeterminateDiagnostic_ReturnsIndeterminateClaimCoverageNone));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile("from.json", CreateFromJson("project-fingerprint", coverageImpact: "indeterminate"));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Incomplete, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.ReadSurfaceSafe.Value, StringComparison.Ordinal));
        Assert.Equal(VerifyClaimStatusValues.Indeterminate, claim.Status);
        Assert.Equal(VerifyCoverageValues.None, claim.Coverage);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithUnboundCoverageDiagnostic_ReturnsBlockingResidualRisk ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithUnboundCoverageDiagnostic_ReturnsBlockingResidualRisk));
        var fromPath = scope.WriteFile(
            "from.json",
            CreateFromJson(
                "project-fingerprint",
                coverageImpact: "partial",
                touchedJson: "[]",
                sourceKind: "operation",
                commit: null,
                persistenceExpected: false,
                expectedPostState: "unavailable",
                includeReadPostcondition: false,
                op: UcliPrimitiveOperationNames.SceneOpen));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: "built-in:mutation",
            ProfilePath: null,
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Fail, result.Output!.Verdict);
        var risk = Assert.Single(result.Output.ResidualRisks);
        Assert.Equal(VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value, risk.Code);
        Assert.True(risk.Blocking);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMixedBoundAndUnboundDiagnostics_ReturnsBlockingResidualRisk ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithMixedBoundAndUnboundDiagnostics_ReturnsBlockingResidualRisk));
        WriteRequiredPostReadProfile(scope);
        var fromPath = scope.WriteFile("from.json", CreateMixedBoundAndUnboundDiagnosticFromJson("project-fingerprint"));
        var service = CreateService(scope.FullPath);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: fromPath,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(VerifyVerdictValues.Fail, result.Output!.Verdict);
        var observedClaim = Assert.Single(result.Output.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.PostMutationObserved.Value, StringComparison.Ordinal));
        Assert.Equal(VerifyCoverageValues.Full, observedClaim.Coverage);
        var risk = Assert.Single(result.Output.ResidualRisks);
        Assert.Equal(VerifyRiskCodes.FromDiagnosticCoverageUnbound.Value, risk.Code);
        Assert.True(risk.Blocking);
    }

}
