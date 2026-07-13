using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests;

internal static class StatusServiceAssert
{
    public static void NotRunningOutputReturnedWithoutPingTelemetry (
        StatusExecutionResult result,
        string expectedUnityVersion,
        RecordingDaemonPingInfoClient daemonPingInfoClient)
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
        Assert.Empty(daemonPingInfoClient.Invocations);
    }

    public static void StaleOutputReturnedWithoutPingTelemetry (
        StatusExecutionResult result,
        string expectedUnityVersion,
        RecordingDaemonPingInfoClient daemonPingInfoClient)
    {
        var output = AssertSuccessfulOutput(result);
        Assert.Equal(DaemonStatusKind.Stale, output.DaemonStatus);
        Assert.Equal(expectedUnityVersion, output.UnityVersion);
        Assert.Null(output.ServerVersion);
        Assert.Equal(IpcEditorLifecycleState.Unavailable, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReason.Unavailable, output.BlockingReason);
        Assert.Null(output.CompileState);
        Assert.Null(output.Generations);
        Assert.False(output.CanAcceptExecutionRequests);
        Assert.Null(output.EditorMode);
        Assert.Null(output.PlayMode);
        Assert.NotNull(output.ObservedAtUtc);
        Assert.Equal(DaemonDiagnosisActionRequiredValues.InspectUnityLog, output.ActionRequired);
        Assert.Null(output.PrimaryDiagnostic);
        Assert.Empty(daemonPingInfoClient.Invocations);
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

    public static void DaemonStatusFailureStoppedBeforePingTelemetry (
        StatusExecutionResult result,
        string expectedMessage,
        RecordingDaemonPingInfoClient daemonPingInfoClient)
    {
        var error = AssertFailure(result, ExecutionErrorKind.InternalError);
        Assert.Equal(expectedMessage, error.Message);
        Assert.Empty(daemonPingInfoClient.Invocations);
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
