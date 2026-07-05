namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Startup;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Unity;

internal static class DaemonStartupReadinessProbeTestSupport
{
    public static DaemonStartupReadinessProbe CreateProbe (
        IDaemonPingInfoClient pingClient,
        IUnityLogReader logReader,
        UnityProjectLockFileProbeResult? lockFileProbeResult = null,
        TimeProvider? timeProvider = null)
    {
        return new DaemonStartupReadinessProbe(
            pingClient,
            logReader,
            CreateProjectLockPreflightService(lockFileProbeResult),
            timeProvider);
    }

    public static async Task<DaemonStartupReadinessProbeResult> WaitUntilStartupDeadlineAsync (
        DaemonStartupReadinessProbe probe,
        RecordingDaemonPingInfoClient pingClient,
        ManualTimeProvider timeProvider,
        string description,
        string projectFingerprint)
    {
        var timeout = TimeSpan.FromMilliseconds(20);
        var resultTask = probe.WaitUntilReadyAsync(
                ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(projectFingerprint),
                timeout,
                cancellationToken: CancellationToken.None)
            .AsTask();

        await pingClient.WaitForFirstInvocationAsync(description, TimeSpan.FromSeconds(5));
        await timeProvider.WaitForTimerDueWithinAsync(timeout);
        timeProvider.Advance(timeout);
        return await TestAwaiter.WaitAsync(resultTask, description, TimeSpan.FromSeconds(5));
    }

    public static IpcPingResponse CreatePingPayload (
        string lifecycleState = IpcEditorLifecycleStateCodec.Ready,
        bool canAcceptExecutionRequests = true)
    {
        return IpcPingResponseTestFactory.Create(
            lifecycleState: lifecycleState,
            canAcceptExecutionRequests: canAcceptExecutionRequests);
    }

    private static RecordingUnityProjectLockPreflightService CreateProjectLockPreflightService (
        UnityProjectLockFileProbeResult? result)
    {
        if (result is null)
        {
            return new RecordingUnityProjectLockPreflightService();
        }

        return new RecordingUnityProjectLockPreflightService(ConvertProbeResult(result))
        {
            CleanupResult = ConvertPostExitProbeResult(result),
        };
    }

    private static UnityProjectLockPreflightResult ConvertProbeResult (
        UnityProjectLockFileProbeResult result)
    {
        if (!result.IsSuccess)
        {
            return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
        }

        if (!result.IsLocked)
        {
            return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
        }

        return UnityProjectLockPreflightResult.ActiveLock(
            result.LockFilePath!,
            "Unity project is already open.");
    }

    private static UnityProjectLockPreflightResult ConvertPostExitProbeResult (
        UnityProjectLockFileProbeResult result)
    {
        if (!result.IsSuccess)
        {
            return UnityProjectLockPreflightResult.InspectionFailed(result.ErrorMessage!);
        }

        if (!result.IsLocked)
        {
            return UnityProjectLockPreflightResult.Unlocked(result.LockFilePath!);
        }

        return UnityProjectLockPreflightResult.StaleLockCleared(result.LockFilePath!);
    }
}
