using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes opaque log cursor values. </summary>
public static class IpcLogCursorCodec
{
    /// <summary> Encodes one stream identifier and sequence value to opaque cursor string. </summary>
    /// <param name="streamId"> The stream identifier. </param>
    /// <param name="sequence"> The sequence value. </param>
    /// <returns> The encoded cursor value. </returns>
    public static string Encode (
        string streamId,
        long sequence)
    {
        if (string.IsNullOrWhiteSpace(streamId))
        {
            throw new ArgumentException("streamId must not be empty.", nameof(streamId));
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "sequence must be non-negative.");
        }

        var sequenceTextLength = GetNonNegativeInt64TextLength(sequence);
        var length = checked(streamId.Length + 1 + sequenceTextLength);
        return string.Create(
            length,
            (StreamId: streamId, Sequence: sequence),
            static (destination, state) =>
            {
                state.StreamId.AsSpan().CopyTo(destination);
                destination[state.StreamId.Length] = ':';
                if (!state.Sequence.TryFormat(
                        destination[(state.StreamId.Length + 1)..],
                        out _,
                        provider: CultureInfo.InvariantCulture))
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
        out string streamId,
        out long sequence)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            streamId = string.Empty;
            sequence = default;
            return false;
        }

        var separatorIndex = cursor.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= cursor.Length - 1)
        {
            streamId = string.Empty;
            sequence = default;
            return false;
        }

        var parsedStreamId = cursor.AsSpan(0, separatorIndex);
        var sequenceText = cursor.AsSpan(separatorIndex + 1);
        if (IsWhiteSpace(parsedStreamId)
            || !long.TryParse(sequenceText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSequence)
            || parsedSequence < 0)
        {
            streamId = string.Empty;
            sequence = default;
            return false;
        }

        streamId = parsedStreamId.ToString();
        sequence = parsedSequence;
        return true;
    }

    private static int GetNonNegativeInt64TextLength (long value)
    {
        var length = 1;
        var remaining = value;
        while (remaining >= 10)
        {
            length++;
            remaining /= 10;
        }

        return length;
    }

    private static bool IsWhiteSpace (ReadOnlySpan<char> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (!char.IsWhiteSpace(value[i]))
            {
                return false;
            }
        }

        return true;
    }
}
