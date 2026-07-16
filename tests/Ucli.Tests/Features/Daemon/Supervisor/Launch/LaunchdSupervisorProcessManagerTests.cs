using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Supervisor;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Tests.Helpers.Process;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class LaunchdSupervisorProcessManagerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenStaleServiceIsAbsent_BootstrapsGeneratedLaunchAgent ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "bootstrap");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(3),
            ProcessRunResult.Exited(0));
        var manager = new LaunchdSupervisorProcessManager(processRunner);
        var plistPath = UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(scope.FullPath);

        var result = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(result.Lease);
        await lease.CommitAsync();
        Assert.True(File.Exists(plistPath));
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal(["-u"], invocation.Request.Arguments),
            invocation => Assert.Equal("--wait", invocation.Request.Arguments[1]),
            invocation => Assert.Equal(["bootstrap", "gui/501", plistPath], invocation.Request.Arguments));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenLaunchSucceeds_RollbackBootsOutCapturedServiceAndWaits ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "rollback");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(3),
            ProcessRunResult.Exited(0),
            ProcessRunResult.Exited(0));
        var manager = new LaunchdSupervisorProcessManager(processRunner);

        var launchResult = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(launchResult.Lease);

        var rollbackError = await lease.RollbackAsync();

        Assert.Null(rollbackError);
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal(["-u"], invocation.Request.Arguments),
            invocation => Assert.Equal(["bootout", "--wait", GetServiceTarget(scope.FullPath)], invocation.Request.Arguments),
            invocation => Assert.Equal("bootstrap", invocation.Request.Arguments[0]),
            invocation => Assert.Equal(["bootout", "--wait", GetServiceTarget(scope.FullPath)], invocation.Request.Arguments));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [Trait("Size", "Small")]
    public async Task ReleaseCurrentProcessRegistrationAsync_WhenPlistIsMissing_BootsOutDeterministicWorktreeService (int bootoutExitCode)
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "release-missing-plist");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(bootoutExitCode));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);
        var plistPath = UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(scope.FullPath);
        Assert.False(File.Exists(plistPath));

        var error = await processManager.ReleaseCurrentProcessRegistrationAsync(
            scope.FullPath,
            CancellationToken.None);

        Assert.Null(error);
        Assert.False(File.Exists(plistPath));
        Assert.Collection(
            processRunner.Invocations,
            invocation =>
            {
                Assert.Equal("/usr/bin/id", invocation.Request.FileName);
                Assert.Equal(["-u"], invocation.Request.Arguments);
            },
            invocation =>
            {
                var worktreeIdentity = SupervisorWorktreeIdentity.Create(scope.FullPath);
                var label = "dev.mackysoft.ucli.supervisor." + worktreeIdentity.LaunchServiceNameSuffix;
                Assert.Equal("/bin/launchctl", invocation.Request.FileName);
                Assert.Equal(["bootout", $"gui/501/{label}"], invocation.Request.Arguments);
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchLeaseRollbackAsync_WhenBootoutFails_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "release-failure");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(3),
            ProcessRunResult.Exited(0),
            ProcessRunResult.Exited(5, "bootout failed"));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);

        var launchResult = await processManager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(launchResult.Lease);

        var error = await lease.RollbackAsync();

        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("--wait", processRunner.Invocations[3].Request.Arguments[1]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchLeaseRollbackAsync_WhenBootoutTimesOut_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "release-timeout");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(3),
            ProcessRunResult.Exited(0),
            ProcessRunResult.TimedOut("bootout timed out"));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);

        var launchResult = await processManager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(launchResult.Lease);

        var error = await lease.RollbackAsync();

        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenStaleBootoutFails_DoesNotPublishOrBootstrapSuccessor ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "bootout-failure");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(5, "launchctl bootout failed"),
            ProcessRunResult.Exited(0),
            ProcessRunResult.Exited(0));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);

        var result = await processManager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Null(result.Lease);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(scope.FullPath)));
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal(["-u"], invocation.Request.Arguments),
            invocation => Assert.Equal("--wait", invocation.Request.Arguments[1]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenBootstrapTimesOut_RollsBackPossibleRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "bootstrap-timeout");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(3),
            ProcessRunResult.TimedOut("bootstrap timed out"),
            ProcessRunResult.Exited(0));
        var manager = new LaunchdSupervisorProcessManager(processRunner);

        var result = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error.Kind);
        Assert.Null(result.Lease);
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal(["-u"], invocation.Request.Arguments),
            invocation => Assert.Equal("bootout", invocation.Request.Arguments[0]),
            invocation => Assert.Equal("bootstrap", invocation.Request.Arguments[0]),
            invocation => Assert.Equal(["bootout", "--wait", GetServiceTarget(scope.FullPath)], invocation.Request.Arguments));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenBootstrapIsCanceled_RollsBackBeforePropagatingCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "bootstrap-cancellation");
        using var cancellation = new CancellationTokenSource();
        var rollbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rollbackAllowed = new TaskCompletionSource<ProcessRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processRunner = new RecordingProcessRunner();
        processRunner.RunHandler = (_, cancellationToken) =>
        {
            return processRunner.Invocations.Count switch
            {
                1 => Task.FromResult(ProcessRunResult.Exited(0, standardOutput: "501\n")),
                2 => Task.FromResult(ProcessRunResult.Exited(3)),
                3 => CancelBootstrap(cancellation, cancellationToken),
                4 => WaitForRollback(rollbackStarted, rollbackAllowed),
                _ => throw new InvalidOperationException("Unexpected launchctl invocation."),
            };
        };
        var manager = new LaunchdSupervisorProcessManager(processRunner);

        var resultTask = manager.LaunchAsync(
                scope.FullPath,
                new SupervisorLaunchCommand("ucli", []),
                cancellation.Token)
            .AsTask();

        await TestAwaiter.WaitAsync(
            rollbackStarted.Task,
            "LaunchAgent rollback start",
            SupervisorBootstrapperTestSupport.SignalWaitTimeout);
        Assert.False(resultTask.IsCompleted);
        rollbackAllowed.TrySetResult(ProcessRunResult.Exited(0));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => resultTask);

        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal(["-u"], invocation.Request.Arguments),
            invocation => Assert.Equal("bootout", invocation.Request.Arguments[0]),
            invocation => Assert.Equal("bootstrap", invocation.Request.Arguments[0]),
            invocation =>
            {
                Assert.Equal(["bootout", "--wait", GetServiceTarget(scope.FullPath)], invocation.Request.Arguments);
                Assert.Equal(CancellationToken.None, invocation.CancellationToken);
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenCanceledAfterBootstrapSucceeds_ReturnsOwnedLease ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "post-bootstrap-cancellation");
        using var cancellation = new CancellationTokenSource();
        var processRunner = new RecordingProcessRunner();
        processRunner.RunHandler = (_, _) =>
        {
            return processRunner.Invocations.Count switch
            {
                1 => Task.FromResult(ProcessRunResult.Exited(0, standardOutput: "501\n")),
                2 => Task.FromResult(ProcessRunResult.Exited(3)),
                3 => CancelAfterBootstrapSuccess(cancellation),
                _ => throw new InvalidOperationException("Unexpected launchctl invocation."),
            };
        };
        var manager = new LaunchdSupervisorProcessManager(processRunner);

        var launchResult = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Lease);
        Assert.Equal(3, processRunner.Invocations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenBootstrapAndRollbackFail_ReturnsFailureWithOwnedLease ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "bootstrap-rollback-failure");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(3),
            ProcessRunResult.TimedOut("bootstrap timed out"),
            ProcessRunResult.TimedOut("bootout timed out"));
        var manager = new LaunchdSupervisorProcessManager(processRunner);

        var launchResult = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.False(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Lease);
        Assert.NotNull(launchResult.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, launchResult.Error.Kind);
        Assert.Contains("RegistrationRollback=", launchResult.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenCancellationRollbackFails_ReturnsFailureWithOwnedLease ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "cancellation-rollback-failure");
        using var cancellation = new CancellationTokenSource();
        var processRunner = new RecordingProcessRunner();
        processRunner.RunHandler = (_, cancellationToken) =>
        {
            return processRunner.Invocations.Count switch
            {
                1 => Task.FromResult(ProcessRunResult.Exited(0, standardOutput: "501\n")),
                2 => Task.FromResult(ProcessRunResult.Exited(3)),
                3 => CancelBootstrap(cancellation, cancellationToken),
                4 => Task.FromResult(ProcessRunResult.Exited(5, "bootout failed")),
                _ => throw new InvalidOperationException("Unexpected launchctl invocation."),
            };
        };
        var manager = new LaunchdSupervisorProcessManager(processRunner);

        var launchResult = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Lease);
        Assert.NotNull(launchResult.Error);
        Assert.Contains("RegistrationRollback=", launchResult.Error.Message, StringComparison.Ordinal);
    }

    private static Task<ProcessRunResult> CancelBootstrap (
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        Assert.Equal(cancellation.Token, cancellationToken);
        cancellation.Cancel();
        return Task.FromResult(ProcessRunResult.Canceled("bootstrap canceled"));
    }

    private static Task<ProcessRunResult> CancelAfterBootstrapSuccess (CancellationTokenSource cancellation)
    {
        cancellation.Cancel();
        return Task.FromResult(ProcessRunResult.Exited(0));
    }

    private static Task<ProcessRunResult> WaitForRollback (
        TaskCompletionSource rollbackStarted,
        TaskCompletionSource<ProcessRunResult> rollbackAllowed)
    {
        rollbackStarted.TrySetResult();
        return rollbackAllowed.Task;
    }

    private static string GetServiceTarget (string storageRoot)
    {
        var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
        return $"gui/501/dev.mackysoft.ucli.supervisor.{worktreeIdentity.LaunchServiceNameSuffix}";
    }
}
