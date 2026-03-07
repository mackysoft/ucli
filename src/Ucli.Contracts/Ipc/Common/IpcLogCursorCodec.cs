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

        return string.Concat(streamId, ":", sequence.ToString(CultureInfo.InvariantCulture));
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

        var parsedStreamId = cursor.Substring(0, separatorIndex);
        var sequenceText = cursor.Substring(separatorIndex + 1);
        if (string.IsNullOrWhiteSpace(parsedStreamId)
            || !long.TryParse(sequenceText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSequence)
            || parsedSequence < 0)
        {
            streamId = string.Empty;
            sequence = default;
            return false;
        }

        streamId = parsedStreamId;
        sequence = parsedSequence;
        return true;
    }
}