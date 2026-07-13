using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes opaque log cursor values. </summary>
internal static class IpcLogCursorCodec
{
    private const int StreamIdTextLength = 32;

    /// <summary> Encodes one stream identifier and sequence value to opaque cursor string. </summary>
    /// <param name="streamId"> The non-empty stream identifier. </param>
    /// <param name="sequence"> The sequence value. </param>
    /// <returns> The encoded cursor value. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="streamId" /> is empty. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="sequence" /> is negative. </exception>
    public static string Encode (
        Guid streamId,
        long sequence)
    {
        if (streamId == Guid.Empty)
        {
            throw new ArgumentException("Stream id must not be empty.", nameof(streamId));
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "sequence must be non-negative.");
        }

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

    /// <summary> Tries to parse one opaque cursor value. </summary>
    /// <param name="cursor"> The cursor value to parse. </param>
    /// <param name="streamId"> The parsed stream identifier on success. </param>
    /// <param name="sequence"> The parsed sequence value on success. </param>
    /// <returns> <see langword="true" /> when parsing succeeded; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? cursor,
        out Guid streamId,
        out long sequence)
    {
        streamId = default;
        sequence = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        var separatorIndex = cursor.LastIndexOf(':');
        if (separatorIndex != StreamIdTextLength || separatorIndex >= cursor.Length - 1)
        {
            return false;
        }

        var parsedStreamId = cursor.AsSpan(0, separatorIndex);
        var sequenceText = cursor.AsSpan(separatorIndex + 1);
        if (!Guid.TryParseExact(parsedStreamId, "N", out var parsedStreamIdValue)
            || parsedStreamIdValue == Guid.Empty
            || !long.TryParse(sequenceText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSequence)
            || parsedSequence < 0)
        {
            return false;
        }

        streamId = parsedStreamIdValue;
        sequence = parsedSequence;
        return true;
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
}
