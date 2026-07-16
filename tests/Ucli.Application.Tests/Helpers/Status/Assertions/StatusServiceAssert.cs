using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal static class StatusServiceAssert
{
    public static void NotRunningOutputReturnedWithoutPingTelemetry (
        StatusExecutionResult result,
        string expectedUnityVersion)
    {
        var output = AssertSuccessfulOutput(result);
        Assert.Equal(DaemonStatusKind.NotRunning, output.DaemonStatus);
        Assert.Equal(expectedUnityVersion, output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.Null(output.CompileState);
        Assert.Null(output.Generations);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(output.EditorMode);
        Assert.Null(output.PlayMode);
    }

    public static void StaleOutputReturnedWithoutPingTelemetry (
        StatusExecutionResult result,
        string expectedUnityVersion)
    {
        var output = AssertSuccessfulOutput(result);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        Assert.Equal(expectedUnityVersion, output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Null(output.LifecycleState);
        Assert.Null(output.BlockingReason);
        Assert.Null(output.CompileState);
        Assert.Null(output.Generations);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(output.EditorMode);
        Assert.Null(output.PlayMode);
        Assert.Null(output.ObservedAtUtc);
        Assert.Null(output.ActionRequired);
        Assert.Null(output.PrimaryDiagnostic);
    }

    public static void InvalidTimeoutStoppedBeforeDaemonStatus (
        StatusExecutionResult result,
        RecordingDaemonStatusOperation daemonStatusOperation)
    {
        var error = AssertFailure(result, ExecutionErrorKind.InvalidArgument);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
        Assert.Empty(daemonStatusOperation.Invocations);
    }

    public static void ContextResolutionFailureStoppedBeforeStatusResolution (
        StatusExecutionResult result,
        string expectedMessage,
        RecordingDaemonStatusOperation daemonStatusOperation,
        RecordingUnityVersionResolver unityVersionResolver)
    {
        var error = AssertFailure(result, ExecutionErrorKind.InvalidArgument);
        Assert.Equal(expectedMessage, error.Message);
        Assert.Empty(daemonStatusOperation.Invocations);
        Assert.Empty(unityVersionResolver.Invocations);
    }

    public static void DaemonStatusFailureReturned (
        StatusExecutionResult result,
        string expectedMessage)
    {
        var error = AssertFailure(result, ExecutionErrorKind.InternalError);
        Assert.Equal(expectedMessage, error.Message);
    }

    private static StatusExecutionOutput AssertSuccessfulOutput (StatusExecutionResult result)
    {
        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        return Assert.IsType<StatusExecutionOutput>(result.Output);
    }

    private static ExecutionError AssertFailure (
        StatusExecutionResult result,
        ExecutionErrorKind expectedKind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(expectedKind, error.Kind);
        return error;
    }
}
