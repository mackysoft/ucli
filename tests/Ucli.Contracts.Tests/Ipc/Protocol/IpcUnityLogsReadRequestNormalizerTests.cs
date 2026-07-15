using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcUnityLogsReadRequestNormalizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryNormalize_WhenStackTraceModeIsNone_ClearsStackTraceLimits ()
    {
        var result = IpcUnityLogsReadRequestNormalizer.TryNormalize(
            new IpcUnityLogsReadRequest(
                Tail: 4,
                After: "stream-1:4",
                Since: "2026-03-05T10:35:22.0000000+09:00",
                Until: "2026-03-05T10:36:22.0000000+09:00",
                Level: null,
                Query: " socket ",
                QueryTarget: null,
                Source: null,
                StackTrace: IpcUnityLogStackTraceMode.None,
                StackTraceMaxFrames: 8,
                StackTraceMaxChars: 4096),
            out var normalizedRequest,
            out var sinceTimestamp,
            out var untilTimestamp,
            out var errorMessage);

        Assert.True(result);
        Assert.NotNull(normalizedRequest);
        Assert.Null(normalizedRequest.Level);
        Assert.Equal(IpcLogQueryTarget.Message, normalizedRequest.QueryTarget);
        Assert.Null(normalizedRequest.Source);
        Assert.Equal(IpcUnityLogStackTraceMode.None, normalizedRequest.StackTrace);
        Assert.Null(normalizedRequest.StackTraceMaxFrames);
        Assert.Null(normalizedRequest.StackTraceMaxChars);
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
        var result = IpcUnityLogsReadRequestNormalizer.TryNormalize(
            new IpcUnityLogsReadRequest(
                Tail: tail,
                After: null,
                Since: since,
                Until: until,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: null,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null),
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
    public void TryNormalize_WhenStackTraceCharLimitIsTooSmall_ReturnsError ()
    {
        var result = IpcUnityLogsReadRequestNormalizer.TryNormalize(
            new IpcUnityLogsReadRequest(
                Tail: null,
                After: null,
                Since: null,
                Until: null,
                Level: null,
                Query: null,
                QueryTarget: null,
                Source: null,
                StackTrace: IpcUnityLogStackTraceMode.All,
                StackTraceMaxFrames: null,
                StackTraceMaxChars: IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars - 1),
            out var normalizedRequest,
            out _,
            out _,
            out var errorMessage);

        Assert.False(result);
        Assert.Null(normalizedRequest);
        Assert.Equal(
            $"stackTraceMaxChars must be between {IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars} and {IpcUnityLogsReadRequestNormalizer.MaximumStackTraceMaxChars}. Actual: {IpcUnityLogsReadRequestNormalizer.MinimumStackTraceMaxChars - 1}.",
            errorMessage);
    }
}
