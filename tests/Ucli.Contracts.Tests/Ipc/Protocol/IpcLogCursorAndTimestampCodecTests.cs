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
        "ABCDEF0123456789ABCDEF0123456789:1",
        "abcdef0123456789abcdef0123456789:01",
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
    public void IpcLogCursor_CreateAndTryParse_RoundTripsCanonicalValues (
        Guid inputStreamId,
        long inputSequence,
        string expectedCursor)
    {
        var cursor = IpcLogCursor.Create(inputStreamId, inputSequence);

        var result = IpcLogCursor.TryParse(cursor.Value, out var parsedCursor);

        Assert.Equal(expectedCursor, cursor.Value);
        Assert.True(result);
        var parsed = Assert.IsType<IpcLogCursor>(parsedCursor);
        Assert.Equal(inputStreamId, parsed.StreamId);
        Assert.Equal(inputSequence, parsed.Sequence);
        Assert.Equal(cursor, parsed);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcLogCursor_Create_WithEmptyStreamId_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => IpcLogCursor.Create(Guid.Empty, 0));

        Assert.Equal("streamId", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidLogCursors))]
    [Trait("Size", "Small")]
    public void IpcLogCursor_TryParse_InvalidValue_ReturnsFalse (string? value)
    {
        var result = IpcLogCursor.TryParse(value, out var cursor);

        Assert.False(result);
        Assert.Null(cursor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcLogCursor_Create_WithNegativeSequence_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => IpcLogCursor.Create(Guid.NewGuid(), -1));

        Assert.Equal("sequence", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcLogCursor_Constructor_WithMalformedValue_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new IpcLogCursor("stream:1"));

        Assert.Equal("value", exception.ParamName);
    }
}
