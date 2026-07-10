using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityProcessOwnershipTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveDaemonLaunch_WhenMetadataIsAvailable_ReturnsMetadataWithoutConsumingHandle ()
    {
        var processStartedAtUtc = new DateTimeOffset(2026, 07, 11, 0, 0, 5, TimeSpan.Zero);
        var processHandle = new StubUnityBatchmodeProcessHandle
        {
            ProcessIdProvider = static () => 8123,
            StartTimeUtcProvider = () => processStartedAtUtc,
        };

        var result = await UnityProcessOwnership.ResolveDaemonLaunchAsync(
            processHandle,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8123, result.ProcessId);
        Assert.Equal(processStartedAtUtc, result.ProcessStartedAtUtc);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
        Assert.Equal(0, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveDaemonLaunch_WhenMetadataReadThrows_TerminatesAndDisposesProcess ()
    {
        var metadataFailure = new IOException("process start metadata failed");
        var processHandle = new StubUnityBatchmodeProcessHandle
        {
            StartTimeUtcProvider = () => throw metadataFailure,
        };

        var result = await UnityProcessOwnership.ResolveDaemonLaunchAsync(
            processHandle,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains(metadataFailure.Message, error.Message, StringComparison.Ordinal);
        var terminationInvocation = UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.ForceKill);
        Assert.False(terminationInvocation.CancellationToken.CanBeCanceled);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveDaemonLaunch_WhenStartTimeIsUnavailableAndTerminationFails_PreservesMetadataFailure ()
    {
        var processHandle = new StubUnityBatchmodeProcessHandle
        {
            StartTimeUtcProvider = static () => null,
            TerminateHandler = static (_, _) => Task.FromResult(ProcessTerminationResult.ForceKillFailed),
        };

        var result = await UnityProcessOwnership.ResolveDaemonLaunchAsync(
            processHandle,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("start time could not be read", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(ProcessTerminationResult.ForceKillFailed), error.Message, StringComparison.Ordinal);
        UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.ForceKill);
        Assert.Equal(1, processHandle.DisposeCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ResolveDaemonLaunch_WhenMetadataReadIsCanceledAndTerminationThrows_PreservesCancellation ()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellation = new OperationCanceledException(cancellationTokenSource.Token);
        var processHandle = new StubUnityBatchmodeProcessHandle
        {
            StartTimeUtcProvider = () =>
            {
                cancellationTokenSource.Cancel();
                throw cancellation;
            },
            TerminateHandler = static (_, _) => Task.FromException<ProcessTerminationResult>(
                new IOException("process termination failed")),
        };

        var actualCancellation = await Assert.ThrowsAsync<OperationCanceledException>(
            () => UnityProcessOwnership.ResolveDaemonLaunchAsync(
                    processHandle,
                    cancellationTokenSource.Token)
                .AsTask());

        Assert.Same(cancellation, actualCancellation);
        var terminationInvocation = UnityBatchmodeProcessHandleAssert.TerminatedOnceWithMode(
            processHandle,
            ProcessTerminationMode.ForceKill);
        Assert.False(terminationInvocation.CancellationToken.CanBeCanceled);
        Assert.Equal(1, processHandle.DisposeCount);
    }
}
