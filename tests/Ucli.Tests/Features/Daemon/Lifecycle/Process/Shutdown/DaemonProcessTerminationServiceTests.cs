namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;

public sealed class DaemonProcessTerminationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenProcessIdIsNull_ReturnsSuccess ()
    {
        var service = CreateService();

        var result = await service.EnsureStoppedAsync(
            target: null,
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureStopped_WhenProcessIdentityCannotBeVerified_ReturnsFailureWithoutKilling ()
    {
        var service = CreateService();
        var currentProcessId = Environment.ProcessId;

        var result = await service.EnsureStoppedAsync(
            target: CreateTarget(currentProcessId, processStartedAtUtc: null),
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity could not be verified", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureStopped_WhenProcessStartTimeDoesNotMatchExpectedStartTime_ReturnsFailure ()
    {
        var service = CreateService();
        var currentProcess = Process.GetCurrentProcess();

        var result = await service.EnsureStoppedAsync(
            target: CreateTarget(currentProcess.Id, DateTimeOffset.UtcNow.AddHours(1)),
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureStopped_WhenMatchingProcessHandlesSigTerm_ReturnsSuccessAfterGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "sigterm");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = TestProcessInvocations.StartProcess(
            TestProcessInvocations.CreateUnixReadyTermSignalMarkerLoop(readyPath, markerPath));
        var processStartedAtUtc = process.StartTime.ToUniversalTime();
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStoppedAsync(
                CreateTarget(process.Id, processStartedAtUtc),
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            TestProcessAwaiter.TerminateBestEffort(process);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureStopped_WhenExtraTimeoutBudgetAndMatchingProcessExitsDuringPassiveWait_DoesNotRequestGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "passive-exit");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = TestProcessInvocations.StartProcess(
            TestProcessInvocations.CreateUnixReadyTermSignalMarkerPassiveExit(readyPath, markerPath));
        var processStartedAtUtc = process.StartTime.ToUniversalTime();
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStoppedAsync(
                CreateTarget(process.Id, processStartedAtUtc),
                TimeSpan.FromSeconds(15),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            TestProcessAwaiter.TerminateBestEffort(process);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task EnsureStopped_WhenDefaultTimeoutAndMatchingProcessOutlivesPassiveWait_RequestsGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "delayed-passive-exit");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = TestProcessInvocations.StartProcess(
            TestProcessInvocations.CreateUnixReadyTermSignalMarkerLoop(readyPath, markerPath));
        var processStartedAtUtc = process.StartTime.ToUniversalTime();
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStoppedAsync(
                CreateTarget(process.Id, processStartedAtUtc),
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            TestProcessAwaiter.TerminateBestEffort(process);
        }
    }

    private static DaemonProcessTerminationService CreateService ()
    {
        return new DaemonProcessTerminationService(
            new DaemonProcessIdentityAssessor(),
            TimeProvider.System);
    }

    private static DaemonProcessTerminationTarget CreateTarget (
        int processId,
        DateTimeOffset? processStartedAtUtc)
    {
        return new DaemonProcessTerminationTarget(
            ProcessId: processId,
            ProcessStartedAtUtc: processStartedAtUtc);
    }

    private static async Task WaitForFileExistsAsync (
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken);
        }

        Assert.Fail($"File was not created within {timeout}: {path}");
    }

}
