using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcDaemonLogsReadRequestNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenRequestIsValid_ReturnsCanonicalValues ()
    {
        var result = IpcDaemonLogsReadRequestNormalizer.TryNormalize(
            new IpcDaemonLogsReadRequest(
                Tail: 4,
                After: "stream-1:4",
                Since: "2026-03-05T10:35:22.0000000+09:00",
                Until: "2026-03-05T10:36:22.0000000+09:00",
                Level: null,
                Query: " socket ",
                QueryTarget: null,
                Category: " ipc "),
            out var normalizedRequest,
            out var sinceTimestamp,
            out var untilTimestamp,
            out var errorMessage);

        Assert.True(result);
        Assert.NotNull(normalizedRequest);
        Assert.Null(normalizedRequest.Level);
        Assert.Equal(IpcLogQueryTarget.Message, normalizedRequest.QueryTarget);
        Assert.Equal("ipc", normalizedRequest.Category);
        Assert.Equal("socket", normalizedRequest.Query);
        Assert.True(sinceTimestamp.HasValue);
        Assert.True(untilTimestamp.HasValue);
        Assert.Null(errorMessage);
    }

    [Theory]
    [MemberData(nameof(IpcLogCodecTestSupport.WindowBoundsFailureCases), MemberType = typeof(IpcLogCodecTestSupport))]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenWindowBoundsAreInvalid_ReturnsError (
        int? tail,
        string? since,
        string? until,
        string expectedErrorMessage)
    {
        var result = IpcDaemonLogsReadRequestNormalizer.TryNormalize(
            new IpcDaemonLogsReadRequest(
                Tail: tail,
                After: null,
                Since: since,
                Until: until,
                Level: null,
                Query: null,
                QueryTarget: null,
                Category: null),
            out var normalizedRequest,
            out _,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Null(normalizedRequest);
        Assert.Equal(expectedErrorMessage, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenQueryTargetIsStack_ReturnsError ()
    {
        var result = IpcDaemonLogsReadRequestNormalizer.TryNormalize(
            new IpcDaemonLogsReadRequest(
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: IpcLogQueryTarget.Stack,
                Category: null),
            out var normalizedRequest,
            out _,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Null(normalizedRequest);
        Assert.Equal("queryTarget 'stack' is not supported for daemon logs. Supported: message, both.", errorMessage);
    }
}
