using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Features.Daemon.Supervisor;
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

        var error = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.Null(error);
        Assert.True(File.Exists(plistPath));
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal(["-u"], invocation.Request.Arguments),
            invocation => Assert.Equal("--wait", invocation.Request.Arguments[1]),
            invocation => Assert.Equal(["bootstrap", "gui/501", plistPath], invocation.Request.Arguments));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [Trait("Size", "Small")]
    public async Task ReleaseAsync_WhenPlistIsMissing_BootsOutDeterministicWorktreeService (int bootoutExitCode)
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "release-missing-plist");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(bootoutExitCode));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);
        var plistPath = UcliStoragePathResolver.ResolveSupervisorLaunchAgentPlistPath(scope.FullPath);
        Assert.False(File.Exists(plistPath));

        var error = await processManager.ReleaseAsync(
            scope.FullPath,
            SupervisorProcessReleaseMode.CurrentProcess,
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
    public async Task ReleaseAsync_WhenBootoutFails_ReturnsInternalError ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "release-failure");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.Exited(5, "bootout failed"));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);

        var error = await processManager.ReleaseAsync(
            scope.FullPath,
            SupervisorProcessReleaseMode.AwaitTermination,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal("--wait", processRunner.Invocations[1].Request.Arguments[1]);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReleaseAsync_WhenBootoutTimesOut_ReturnsTimeout ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "release-timeout");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0, standardOutput: "501\n"),
            ProcessRunResult.TimedOut("bootout timed out"));
        var processManager = new LaunchdSupervisorProcessManager(processRunner);

        var error = await processManager.ReleaseAsync(
            scope.FullPath,
            SupervisorProcessReleaseMode.AwaitTermination,
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReleaseAsync_WhenReleaseModeIsUndefined_RejectsBeforeStartingProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("launchd-supervisor", "invalid-release-mode");
        var processRunner = new RecordingProcessRunner();
        var processManager = new LaunchdSupervisorProcessManager(processRunner);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => processManager.ReleaseAsync(
                scope.FullPath,
                (SupervisorProcessReleaseMode)999,
                CancellationToken.None)
            .AsTask());

        Assert.Empty(processRunner.Invocations);
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

        var error = await processManager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.NotNull(error);
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

        var error = await manager.LaunchAsync(
            scope.FullPath,
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
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

    private static Task<ProcessRunResult> CancelBootstrap (
        CancellationTokenSource cancellation,
        CancellationToken cancellationToken)
    {
        Assert.Equal(cancellation.Token, cancellationToken);
        cancellation.Cancel();
        return Task.FromResult(ProcessRunResult.Canceled("bootstrap canceled"));
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
