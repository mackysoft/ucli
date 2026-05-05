namespace MackySoft.Ucli.Tests.Daemon;

using System.Diagnostics;
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

    private static DaemonProcessTerminationService CreateService ()
    {
        return new DaemonProcessTerminationService(new DaemonProcessIdentityAssessor());
    }
}
