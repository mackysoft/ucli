using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Features.Daemon.Supervisor;
using MackySoft.Ucli.Tests.Helpers.Process;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SystemdRunSupervisorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArguments_AppendsInternalSupervisorInvocationArguments ()
    {
        const string repositoryRoot = "/repo";
        var absoluteRepositoryRoot = AbsolutePath.Parse(repositoryRoot);
        const string unitName = "mackysoft-ucli-supervisor-test";
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var arguments = SystemdRunSupervisorProcessLauncher.BuildArguments(
            absoluteRepositoryRoot,
            unitName,
            launchCommand);

        Assert.Equal(
            [
                "--user",
                "--quiet",
                "--collect",
                "--unit",
                unitName,
                "--working-directory",
                repositoryRoot,
                "ucli",
                "--base",
                ..SupervisorInvocationArguments.Build(absoluteRepositoryRoot),
            ],
            arguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenSystemdRunSucceeds_CommitDoesNotStartAnotherProcess ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "commit");
        var processRunner = new RecordingProcessRunner(ProcessRunResult.Exited(0));
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);

        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(launchResult.Lease);

        await lease.CommitAsync();

        Assert.True(launchResult.IsSuccess);
        Assert.Null(launchResult.Error);
        var invocation = Assert.Single(processRunner.Invocations);
        Assert.Equal("systemd-run", invocation.Request.FileName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenSystemdRunSucceeds_RollbackStopsCapturedUnit (int stopExitCode)
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "rollback");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0),
            ProcessRunResult.Exited(stopExitCode));
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);
        var unitName = GetUnitName(AbsolutePath.Parse(scope.FullPath));

        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(launchResult.Lease);

        var rollbackError = await lease.RollbackAsync();

        Assert.Null(rollbackError);
        Assert.Collection(
            processRunner.Invocations,
            invocation =>
            {
                Assert.Equal("systemd-run", invocation.Request.FileName);
                Assert.Contains(unitName, invocation.Request.Arguments);
            },
            invocation =>
            {
                Assert.Equal("systemctl", invocation.Request.FileName);
                Assert.Equal(["--user", "stop", unitName], invocation.Request.Arguments);
            });
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchLeaseRollbackAsync_WhenSystemctlTimesOut_CanRetryTermination ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "rollback-timeout");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(0),
            ProcessRunResult.TimedOut("systemctl stop timed out"),
            ProcessRunResult.Exited(0));
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);
        var unitName = GetUnitName(AbsolutePath.Parse(scope.FullPath));
        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);
        var lease = Assert.IsAssignableFrom<ISupervisorProcessLaunchLease>(launchResult.Lease);

        var firstError = await lease.RollbackAsync();
        var secondError = await lease.RollbackAsync();

        Assert.NotNull(firstError);
        Assert.Equal(ExecutionErrorKind.Timeout, firstError.Kind);
        Assert.Null(secondError);
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal("systemd-run", invocation.Request.FileName),
            invocation => Assert.Equal(["--user", "stop", unitName], invocation.Request.Arguments),
            invocation => Assert.Equal(["--user", "stop", unitName], invocation.Request.Arguments));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenCanceledAfterSystemdRunSucceeds_ReturnsOwnedLease ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "post-launch-cancellation");
        using var cancellation = new CancellationTokenSource();
        var processRunner = new RecordingProcessRunner
        {
            RunHandler = (request, cancellationToken) =>
            {
                Assert.Equal("systemd-run", request.FileName);
                Assert.Equal(cancellation.Token, cancellationToken);
                cancellation.Cancel();
                return Task.FromResult(ProcessRunResult.Exited(0));
            },
        };
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);

        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.True(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Lease);
        Assert.Single(processRunner.Invocations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenSystemdRunTimesOut_RollsBackPossibleUnit ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "launch-timeout");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.TimedOut("systemd-run timed out"),
            ProcessRunResult.Exited(0));
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);
        var unitName = GetUnitName(AbsolutePath.Parse(scope.FullPath));

        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.False(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, launchResult.Error.Kind);
        Assert.Null(launchResult.Lease);
        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal("systemd-run", invocation.Request.FileName),
            invocation => Assert.Equal(["--user", "stop", unitName], invocation.Request.Arguments));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenSystemdRunFailsAndRollbackFails_AppendsRollbackError ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "launch-rollback-failure");
        var processRunner = new RecordingProcessRunner(
            ProcessRunResult.Exited(1, "systemd-run failed"),
            ProcessRunResult.Exited(1, "systemctl stop failed"));
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);

        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            CancellationToken.None);

        Assert.False(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, launchResult.Error.Kind);
        Assert.NotNull(launchResult.Lease);
        Assert.Contains("systemd-run failed", launchResult.Error.Message, StringComparison.Ordinal);
        Assert.Contains("UnitRollback=", launchResult.Error.Message, StringComparison.Ordinal);
        Assert.Contains("systemctl stop failed", launchResult.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, processRunner.Invocations.Count);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenSystemdRunIsCanceled_RollsBackBeforePropagatingCancellation ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "launch-cancellation");
        using var cancellation = new CancellationTokenSource();
        var processRunner = new RecordingProcessRunner();
        processRunner.RunHandler = (request, cancellationToken) =>
        {
            if (request.FileName == "systemd-run")
            {
                Assert.Equal(cancellation.Token, cancellationToken);
                cancellation.Cancel();
                return Task.FromResult(ProcessRunResult.Canceled("systemd-run canceled"));
            }

            Assert.Equal(CancellationToken.None, cancellationToken);
            return Task.FromResult(ProcessRunResult.Exited(0));
        };
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);
        var unitName = GetUnitName(AbsolutePath.Parse(scope.FullPath));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launcher.LaunchAsync(
                AbsolutePath.Parse(scope.FullPath),
                new SupervisorLaunchCommand("ucli", []),
                cancellation.Token)
            .AsTask());

        Assert.Collection(
            processRunner.Invocations,
            invocation => Assert.Equal("systemd-run", invocation.Request.FileName),
            invocation => Assert.Equal(["--user", "stop", unitName], invocation.Request.Arguments));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task LaunchAsync_WhenCancellationRollbackFails_ReturnsFailureWithOwnedLease ()
    {
        using var scope = TestDirectories.CreateTempScope("systemd-run-supervisor", "cancellation-rollback-failure");
        using var cancellation = new CancellationTokenSource();
        var processRunner = new RecordingProcessRunner();
        processRunner.RunHandler = (request, _) =>
        {
            if (request.FileName == "systemd-run")
            {
                cancellation.Cancel();
                return Task.FromResult(ProcessRunResult.Canceled("systemd-run canceled"));
            }

            return Task.FromResult(ProcessRunResult.Exited(1, "systemctl stop failed"));
        };
        var launcher = new SystemdRunSupervisorProcessLauncher(processRunner);

        var launchResult = await launcher.LaunchAsync(
            AbsolutePath.Parse(scope.FullPath),
            new SupervisorLaunchCommand("ucli", []),
            cancellation.Token);

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(launchResult.IsSuccess);
        Assert.NotNull(launchResult.Lease);
        Assert.NotNull(launchResult.Error);
        Assert.Contains("UnitRollback=", launchResult.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, processRunner.Invocations.Count);
    }

    private static string GetUnitName (AbsolutePath storageRoot)
    {
        var worktreeIdentity = SupervisorWorktreeIdentity.Create(storageRoot);
        return "mackysoft-ucli-supervisor-" + worktreeIdentity.LaunchServiceNameSuffix;
    }
}
