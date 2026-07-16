using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one canonical opaque position in a log stream. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
public sealed class IpcLogCursor : UcliStringValue
{
    private const int StreamIdTextLength = 32;

    /// <summary> Initializes one cursor from its canonical wire value. </summary>
    /// <param name="value"> The lowercase stream identifier and non-negative sequence in <c>&lt;guid-n&gt;:&lt;sequence&gt;</c> form. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not canonical or contains an empty stream identifier. </exception>
    public IpcLogCursor (string value)
        : this(Parse(value))
    {
    }

    private IpcLogCursor (ParsedValue parsedValue)
        : base(parsedValue.Value)
    {
        StreamId = parsedValue.StreamId;
        Sequence = parsedValue.Sequence;
    }

    /// <summary> Gets the non-empty stream identifier. </summary>
    public Guid StreamId { get; }

    /// <summary> Gets the non-negative position in the stream. </summary>
    public long Sequence { get; }

    /// <summary> Creates one canonical cursor from structured values. </summary>
    /// <param name="streamId"> The non-empty stream identifier. </param>
    /// <param name="sequence"> The non-negative stream position. </param>
    /// <returns> The canonical cursor. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="streamId" /> is empty. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="sequence" /> is negative. </exception>
    public static IpcLogCursor Create (
        Guid streamId,
        long sequence)
    {
        if (streamId == Guid.Empty)
        {
            throw new ArgumentException("Stream id must not be empty.", nameof(streamId));
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must not be negative.");
        }

        return new IpcLogCursor(new ParsedValue(
            Encode(streamId, sequence),
            streamId,
            sequence));
    }

    /// <summary> Attempts to parse one canonical cursor wire value. </summary>
    /// <param name="value"> The candidate wire value. </param>
    /// <param name="cursor"> The parsed cursor when successful; otherwise <see langword="null" />. </param>
    /// <returns><see langword="true" /> when <paramref name="value" /> is canonical; otherwise <see langword="false" />.</returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out IpcLogCursor? cursor)
    {
        if (!TryParseValue(value, out var parsedValue))
        {
            cursor = null;
            return false;
        }

        cursor = new IpcLogCursor(parsedValue);
        return true;
    }

    private static ParsedValue Parse (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!TryParseValue(value, out var parsedValue))
        {
            throw new ArgumentException(
                "Log cursor must use canonical lowercase <guid-n>:<non-negative-sequence> form with a non-empty stream id.",
                nameof(value));
        }

        return parsedValue;
    }

    private static bool TryParseValue (
        string? value,
        out ParsedValue parsedValue)
    {
        parsedValue = default;
        if (value == null
            || value.Length <= StreamIdTextLength + 1
            || value[StreamIdTextLength] != ':')
        {
            return false;
        }

        var streamIdText = value.AsSpan(0, StreamIdTextLength);
        for (var index = 0; index < streamIdText.Length; index++)
        {
            var character = streamIdText[index];
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        var sequenceText = value.AsSpan(StreamIdTextLength + 1);
        if (sequenceText.Length > 1 && sequenceText[0] == '0')
        {
            return false;
        }

        if (!Guid.TryParseExact(streamIdText, "N", out var streamId)
            || streamId == Guid.Empty
            || !long.TryParse(sequenceText, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence < 0)
        {
            return false;
        }

        parsedValue = new ParsedValue(value, streamId, sequence);
        return true;
    }

    private static string Encode (
        Guid streamId,
        long sequence)
    {
        var sequenceTextLength = GetNonNegativeInvariantInt64Length(sequence);
        var length = checked(StreamIdTextLength + 1 + sequenceTextLength);
        return string.Create(
            length,
            (StreamId: streamId, Sequence: sequence, SequenceTextLength: sequenceTextLength),
            static (destination, state) =>
            {
                if (!state.StreamId.TryFormat(destination, out var streamIdCharsWritten, "N")
                    || streamIdCharsWritten != StreamIdTextLength)
                {
                    throw new InvalidOperationException("Cursor buffer is too small for stream id formatting.");
                }

                destination[StreamIdTextLength] = ':';
                var sequenceDestination = destination[(StreamIdTextLength + 1)..];
                if (!state.Sequence.TryFormat(
                        sequenceDestination,
                        out var charsWritten,
                        provider: CultureInfo.InvariantCulture)
                    || charsWritten != state.SequenceTextLength)
                {
                    throw new InvalidOperationException("Cursor buffer is too small for sequence formatting.");
                }
            });
    }

    private static int GetNonNegativeInvariantInt64Length (long value)
    {
        var length = 1;
        while (value >= 10)
        {
            length++;
            value /= 10;
        }

        return length;
    }

    private readonly struct ParsedValue
    {
        internal ParsedValue (
            string value,
            Guid streamId,
            long sequence)
        {
            Value = value;
            StreamId = streamId;
            Sequence = sequence;
        }

        internal string Value { get; }

        internal Guid StreamId { get; }

        internal long Sequence { get; }
    }
}
