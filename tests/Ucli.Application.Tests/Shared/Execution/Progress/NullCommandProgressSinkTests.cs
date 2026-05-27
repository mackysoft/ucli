using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution.Progress;

public sealed class NullCommandProgressSinkTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task OnEntryAsync_WithInvalidEntry_IgnoresEntry ()
    {
        await NullCommandProgressSink.Instance.OnEntryAsync(
            string.Empty,
            null!,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task OnEntryAsync_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => NullCommandProgressSink.Instance
            .OnEntryAsync(
                string.Empty,
                null!,
                cancellationTokenSource.Token)
            .AsTask());
    }
}
