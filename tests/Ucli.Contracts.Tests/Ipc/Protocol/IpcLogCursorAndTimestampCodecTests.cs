using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcLogCursorAndTimestampCodecTests
{
    public static TheoryData<string?, bool, bool> OptionalTimestampParseCases => new()
    {
        { "2026-03-05T10:35:22.0000000+09:00", true, true },
        { "2026-03-05T01:35:22.0000000Z", true, true },
        { "2026-03-05", false, false },
        { "2026-03-05+09:00", false, false },
        { "2026-03-05T10:35:22", false, false },
        { "invalid", false, false },
        { "", true, false },
        { " ", true, false },
        { null, true, false },
    };

    public static TheoryData<Guid, long, string> LogCursorRoundTripCases => new()
    {
        {
            Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789"),
            0L,
            "abcdef0123456789abcdef0123456789:0"
        },
        {
            Guid.Parse("ABCDEF01-2345-6789-ABCD-EF0123456789"),
            long.MaxValue,
            "abcdef0123456789abcdef0123456789:9223372036854775807"
        },
    };

    public static TheoryData<string?> InvalidLogCursors => new()
    {
        "not-a-guid:1",
        "abcdef01-2345-6789-abcd-ef0123456789:1",
        "00000000000000000000000000000000:1",
        "stream:with:colon:1",
        " abcdef0123456789abcdef0123456789:1",
        "abcdef0123456789abcdef0123456789 :1",
        "abcdef0123456789abcdef0123456789:",
        ":1",
        "abcdef0123456789abcdef0123456789:-1",
        "",
        " ",
        null,
    };

    [Theory]
    [MemberData(nameof(OptionalTimestampParseCases))]
    [Trait("Size", "Small")]
    public void IpcIso8601TimestampCodec_TryParseOptionalWithTimezoneOffset_ReturnsExpectedResult (
        string? value,
        bool expectedResult,
        bool expectedHasValue)
    {
        var result = IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(value, out var timestamp);

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedHasValue, timestamp.HasValue);
    }

    [Theory]
    [MemberData(nameof(LogCursorRoundTripCases))]
    [Trait("Size", "Small")]
    public void IpcLogCursorCodec_EncodeAndTryParse_RoundTripsValues (
        Guid inputStreamId,
        long inputSequence,
        string expectedCursor)
    {
        var cursor = IpcLogCursorCodec.Encode(inputStreamId, inputSequence);

        var result = IpcLogCursorCodec.TryParse(cursor, out var streamId, out var sequence);

        Assert.Equal(expectedCursor, cursor);
        Assert.True(result);
        Assert.Equal(inputStreamId, streamId);
        Assert.Equal(inputSequence, sequence);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcLogCursorCodec_Encode_EmptyStreamId_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => IpcLogCursorCodec.Encode(Guid.Empty, 0));

        Assert.Equal("streamId", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidLogCursors))]
    [Trait("Size", "Small")]
    public void IpcLogCursorCodec_TryParse_InvalidValue_ReturnsFalse (string? value)
    {
        var result = IpcLogCursorCodec.TryParse(value, out var streamId, out var sequence);

        Assert.False(result);
        Assert.Equal(Guid.Empty, streamId);
        Assert.Equal(default, sequence);
    }
}
