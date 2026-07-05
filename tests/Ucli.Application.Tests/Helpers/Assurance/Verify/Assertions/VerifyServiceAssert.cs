using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;

namespace MackySoft.Ucli.Application.Tests;

internal static class VerifyServiceAssert
{
    public static void BuiltInMutationProfileCompletedWithoutCompile (
        VerifyExecutionResult result,
        RecordingVerifyCompileService compileService)
    {
        var output = AssertSuccessfulOutput(result);
        Assert.Empty(compileService.Invocations);
        Assert.DoesNotContain(output.Verifiers, static verifier => string.Equals(verifier.Kind, VerifyStepKindValues.Compile, StringComparison.Ordinal));
        Assert.DoesNotContain(
            output.Verifiers.SelectMany(static verifier => verifier.Effects),
            static effect => VerifyEffectValues.Compile.Contains(effect, StringComparer.Ordinal));
    }

    public static void ProjectFingerprintMismatchStoppedBeforeReady (
        VerifyExecutionResult result,
        RecordingVerifyReadyService readyService)
    {
        var error = AssertFailure(result);
        Assert.Equal(VerifyErrorCodes.ProjectFingerprintMismatch, error.Code);
        Assert.Empty(readyService.Invocations);
    }

    public static void PartialReadSurfaceClaimReturnedWithoutLogs (
        VerifyExecutionResult result,
        RecordingVerifyLogsUnityService logsService)
    {
        var output = AssertSuccessfulOutput(result);
        Assert.Equal(VerifyVerdictValues.Pass, output.Verdict);
        var claim = AssertReadSurfaceClaim(output);
        Assert.False(claim.Required);
        Assert.Equal(VerifyClaimStatusValues.Passed, claim.Status);
        Assert.Equal(VerifyCoverageValues.Partial, claim.Coverage);
        Assert.Empty(logsService.Invocations);
    }

    public static void LogsStepTimedOutAfterAttempt (
        VerifyExecutionResult result,
        RecordingVerifyLogsUnityService logsService)
    {
        var error = AssertFailure(result);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Single(logsService.Invocations);
    }

    public static void CompileStepTimedOutAfterAttempt (
        VerifyExecutionResult result,
        RecordingVerifyCompileService compileService)
    {
        var error = AssertFailure(result);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Single(compileService.Invocations);
    }

    public static void RequiredPartialCoverageCollectedLogsEvidence (
        VerifyExecutionResult result,
        RecordingVerifyLogsUnityService logsService)
    {
        var output = AssertSuccessfulOutput(result);
        Assert.Equal(VerifyVerdictValues.Incomplete, output.Verdict);
        Assert.Single(logsService.Invocations);
        var report = output.Reports["logs.unity"];
        Assert.Equal("ucli://logs/unity?tail=200&count=2", report.Uri);
    }

    private static VerifyExecutionOutput AssertSuccessfulOutput (VerifyExecutionResult result)
    {
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        return Assert.IsType<VerifyExecutionOutput>(result.Output);
    }

    private static ApplicationFailure AssertFailure (VerifyExecutionResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        return Assert.Single(result.Errors);
    }

    private static VerifyClaimOutput AssertReadSurfaceClaim (VerifyExecutionOutput output)
    {
        return Assert.Single(output.Claims, static claim => string.Equals(claim.Id, VerifyClaimCodes.ReadSurfaceSafe.Value, StringComparison.Ordinal));
    }
}
