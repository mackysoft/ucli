using System.ComponentModel;
using System.Diagnostics;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class WindowsDetachedSupervisorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildStartInfo_AppendsInternalSupervisorInvocationArguments ()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "windows-detached-supervisor-launcher", "start-info");
        var normalizedStorageRoot = Path.GetFullPath(storageRoot);
        var launchCommand = new SupervisorLaunchCommand("ucli", ["--base"]);

        var startInfo = WindowsDetachedSupervisorProcessLauncher.BuildStartInfo(storageRoot, launchCommand);

        Assert.Equal("ucli", startInfo.FileName);
        Assert.Equal(normalizedStorageRoot, startInfo.WorkingDirectory);
        Assert.True(startInfo.UseShellExecute);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Equal(
            [
                "--base",
                ..SupervisorInvocationArguments.Build(normalizedStorageRoot),
            ],
            startInfo.ArgumentList);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Launch_WhenProcessStarts_ReturnsLeaseWithoutReleasingHandle ()
    {
        var processHandle = new StubDetachedProcessHandle();
        var processStarter = new StubDetachedProcessStarter
        {
            ProcessHandle = processHandle,
        };
        var launcher = new WindowsDetachedSupervisorProcessLauncher(processStarter);

        var result = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Lease);
        Assert.Null(result.Error);
        Assert.Single(processStarter.StartInvocations);
        Assert.Empty(processHandle.TerminateInvocations);
        Assert.Equal(0, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Launch_WhenProcessStartFails_ReturnsStructuredError ()
    {
        var processStarter = new StubDetachedProcessStarter
        {
            StartException = new Win32Exception("Process creation failed."),
        };
        var launcher = new WindowsDetachedSupervisorProcessLauncher(processStarter);

        var result = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Lease);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Launch_WhenProcessStartReturnsNoHandle_ReturnsStructuredError ()
    {
        var processStarter = new StubDetachedProcessStarter();
        var launcher = new WindowsDetachedSupervisorProcessLauncher(processStarter);

        var result = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Lease);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CommitAsync_TransfersLifetimeWithoutTerminatingProcess ()
    {
        var processHandle = new StubDetachedProcessHandle();
        var launcher = new WindowsDetachedSupervisorProcessLauncher(
            new StubDetachedProcessStarter
            {
                ProcessHandle = processHandle,
            });
        var launchResult = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        await launchResult.Lease!.CommitAsync();
        var rollbackResult = await launchResult.Lease.RollbackAsync();

        Assert.Null(rollbackResult);
        Assert.Empty(processHandle.TerminateInvocations);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CommitAsync_WhenHandleReleaseFails_PreservesTransferredProcessLifetime ()
    {
        var processHandle = new StubDetachedProcessHandle
        {
            DisposeHandler = static () => throw new IOException("Handle release failed."),
        };
        var launcher = new WindowsDetachedSupervisorProcessLauncher(
            new StubDetachedProcessStarter
            {
                ProcessHandle = processHandle,
            });
        var launchResult = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        await launchResult.Lease!.CommitAsync();
        var rollbackResult = await launchResult.Lease.RollbackAsync();

        Assert.Null(rollbackResult);
        Assert.Empty(processHandle.TerminateInvocations);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RollbackAsync_ForceKillsWithoutCallerCancellationAndThenReleasesHandle ()
    {
        var processHandle = new StubDetachedProcessHandle();
        var launcher = new WindowsDetachedSupervisorProcessLauncher(
            new StubDetachedProcessStarter
            {
                ProcessHandle = processHandle,
            });
        var launchResult = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        var rollbackResult = await launchResult.Lease!.RollbackAsync();

        Assert.Null(rollbackResult);
        var invocation = Assert.Single(processHandle.TerminateInvocations);
        Assert.Same(ProcessTerminationPolicy.ForceKill, invocation.TerminationPolicy);
        Assert.Equal(CancellationToken.None, invocation.CancellationToken);
        Assert.Equal(["terminate", "dispose"], processHandle.LifetimeEvents);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RollbackAsync_WhenHandleReleaseFails_StillConfirmsTermination ()
    {
        var processHandle = new StubDetachedProcessHandle
        {
            DisposeHandler = static () => throw new IOException("Handle release failed."),
        };
        var launcher = new WindowsDetachedSupervisorProcessLauncher(
            new StubDetachedProcessStarter
            {
                ProcessHandle = processHandle,
            });
        var launchResult = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        var firstRollbackResult = await launchResult.Lease!.RollbackAsync();
        var secondRollbackResult = await launchResult.Lease.RollbackAsync();

        Assert.Null(firstRollbackResult);
        Assert.Null(secondRollbackResult);
        Assert.Single(processHandle.TerminateInvocations);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RollbackAsync_WhenTerminationCannotBeConfirmed_RetainsHandleForRetry ()
    {
        var terminationAttempt = 0;
        var processHandle = new StubDetachedProcessHandle
        {
            TerminateHandler = (_, _) => Task.FromResult(
                terminationAttempt++ == 0
                    ? ProcessTerminationResult.ForceKillFailed
                    : ProcessTerminationResult.ForceKilled),
        };
        var launcher = new WindowsDetachedSupervisorProcessLauncher(
            new StubDetachedProcessStarter
            {
                ProcessHandle = processHandle,
            });
        var launchResult = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        var firstRollbackResult = await launchResult.Lease!.RollbackAsync();
        var secondRollbackResult = await launchResult.Lease.RollbackAsync();

        Assert.NotNull(firstRollbackResult);
        Assert.Equal(ExecutionErrorKind.InternalError, firstRollbackResult.Kind);
        Assert.Null(secondRollbackResult);
        Assert.Equal(2, processHandle.TerminateInvocations.Count);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RollbackAsync_WhenTerminationThrows_RetainsHandleForRetry ()
    {
        var terminationAttempt = 0;
        var processHandle = new StubDetachedProcessHandle
        {
            TerminateHandler = (_, _) => terminationAttempt++ == 0
                ? Task.FromException<ProcessTerminationResult>(new InvalidOperationException("Termination failed."))
                : Task.FromResult(ProcessTerminationResult.ForceKilled),
        };
        var launcher = new WindowsDetachedSupervisorProcessLauncher(
            new StubDetachedProcessStarter
            {
                ProcessHandle = processHandle,
            });
        var launchResult = launcher.Launch(
            Path.GetTempPath(),
            new SupervisorLaunchCommand("ucli", Array.Empty<string>()));

        var firstRollbackResult = await launchResult.Lease!.RollbackAsync();
        var secondRollbackResult = await launchResult.Lease.RollbackAsync();

        Assert.NotNull(firstRollbackResult);
        Assert.Equal(ExecutionErrorKind.InternalError, firstRollbackResult.Kind);
        Assert.Null(secondRollbackResult);
        Assert.Equal(2, processHandle.TerminateInvocations.Count);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    private sealed class StubDetachedProcessStarter : IDetachedProcessStarter
    {
        private readonly List<ProcessStartInfo> startInvocations = [];

        public IDetachedProcessHandle? ProcessHandle { get; init; }

        public Exception? StartException { get; init; }

        public IReadOnlyList<ProcessStartInfo> StartInvocations => startInvocations;

        public IDetachedProcessHandle? Start (ProcessStartInfo startInfo)
        {
            ArgumentNullException.ThrowIfNull(startInfo);
            startInvocations.Add(startInfo);
            if (StartException is not null)
            {
                throw StartException;
            }

            return ProcessHandle;
        }
    }

    private sealed class StubDetachedProcessHandle : IDetachedProcessHandle
    {
        private readonly List<TerminateInvocation> terminateInvocations = [];

        private readonly List<string> lifetimeEvents = [];

        public Func<ProcessTerminationPolicy, CancellationToken, Task<ProcessTerminationResult>>? TerminateHandler { get; init; }

        public Func<ValueTask>? DisposeHandler { get; init; }

        public IReadOnlyList<TerminateInvocation> TerminateInvocations => terminateInvocations;

        public IReadOnlyList<string> LifetimeEvents => lifetimeEvents;

        public int DisposeCount { get; private set; }

        public Task<ProcessTerminationResult> TerminateAsync (
            ProcessTerminationPolicy terminationPolicy,
            CancellationToken cancellationToken)
        {
            terminateInvocations.Add(new TerminateInvocation(terminationPolicy, cancellationToken));
            lifetimeEvents.Add("terminate");
            return TerminateHandler?.Invoke(terminationPolicy, cancellationToken)
                ?? Task.FromResult(ProcessTerminationResult.ForceKilled);
        }

        public ValueTask DisposeAsync ()
        {
            DisposeCount++;
            lifetimeEvents.Add("dispose");
            return DisposeHandler?.Invoke() ?? ValueTask.CompletedTask;
        }

        internal readonly record struct TerminateInvocation (
            ProcessTerminationPolicy TerminationPolicy,
            CancellationToken CancellationToken);
    }
}
