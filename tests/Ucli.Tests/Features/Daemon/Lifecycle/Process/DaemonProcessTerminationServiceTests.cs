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

        var result = await service.EnsureStopped(
            processId: null,
            expectedIssuedAtUtc: null,
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenProcessIdentityCannotBeVerified_ReturnsFailureWithoutKilling ()
    {
        var service = CreateService();
        var currentProcessId = Environment.ProcessId;

        var result = await service.EnsureStopped(
            processId: currentProcessId,
            expectedIssuedAtUtc: null,
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity could not be verified", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenProcessStartTimeDoesNotMatchSessionIssuedAt_ReturnsFailure ()
    {
        var service = CreateService();
        var currentProcess = Process.GetCurrentProcess();

        var result = await service.EnsureStopped(
            processId: currentProcess.Id,
            expectedIssuedAtUtc: DateTimeOffset.UtcNow.AddHours(1),
            timeout: TimeSpan.FromMilliseconds(100),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("identity mismatch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenMatchingProcessHandlesSigTerm_ReturnsSuccessAfterGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "sigterm");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList =
            {
                "-c",
                $"trap 'printf term > {ShellSingleQuote(markerPath)}; exit 0' TERM; printf ready > {ShellSingleQuote(readyPath)}; while :; do sleep 1; done",
            },
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Test process could not be started.");
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStopped(
                process.Id,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenExtraTimeoutBudgetAndMatchingProcessExitsDuringPassiveWait_DoesNotRequestGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "passive-exit");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList =
            {
                "-c",
                $"trap 'printf term > {ShellSingleQuote(markerPath)}; exit 0' TERM; printf ready > {ShellSingleQuote(readyPath)}; sleep 0.2; exit 0",
            },
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Test process could not be started.");
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStopped(
                process.Id,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(15),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenDefaultTimeoutAndMatchingProcessExitsAfterOneSecond_RequestsGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "delayed-passive-exit");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList =
            {
                "-c",
                $"trap 'printf term > {ShellSingleQuote(markerPath)}; exit 0' TERM; printf ready > {ShellSingleQuote(readyPath)}; sleep 1; exit 0",
            },
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Test process could not be started.");
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStopped(
                process.Id,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(10),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.True(File.Exists(markerPath));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task EnsureStopped_WhenExtraTimeoutBudgetAndMatchingProcessExitsAfterOneSecond_DoesNotRequestGracefulTermination ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope("daemon-process-termination", "extra-budget-passive-exit");
        var readyPath = scope.GetPath("ready-marker");
        var markerPath = scope.GetPath("term-marker");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/sh",
            ArgumentList =
            {
                "-c",
                $"trap 'printf term > {ShellSingleQuote(markerPath)}; exit 0' TERM; printf ready > {ShellSingleQuote(readyPath)}; sleep 1; exit 0",
            },
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Test process could not be started.");
        var service = CreateService();

        try
        {
            await WaitForFileExistsAsync(readyPath, TimeSpan.FromSeconds(5), CancellationToken.None);

            var result = await service.EnsureStopped(
                process.Id,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(15),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static DaemonProcessTerminationService CreateService ()
    {
        return new DaemonProcessTerminationService(new DaemonProcessIdentityAssessor());
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

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        Assert.Fail($"File was not created within {timeout}: {path}");
    }

    private static string ShellSingleQuote (string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
