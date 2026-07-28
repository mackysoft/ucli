using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Reads and writes the canonical UTC timestamp used by immutable artifact references. </summary>
internal sealed class ArtifactPublicationTimeJsonConverter : JsonConverter<DateTimeOffset>
{
    internal const int CanonicalTextLength = 28;

    internal const string CanonicalTextPattern =
        "^(((000[1-9]|00[1-9][0-9]|0[1-9][0-9]{2}|[1-9][0-9]{3})-((0[13578]|1[02])-(0[1-9]|[12][0-9]|3[01])|(0[469]|11)-(0[1-9]|[12][0-9]|30)|02-(0[1-9]|1[0-9]|2[0-8])))|(([0-9]{2}(0[48]|[2468][048]|[13579][26])|(0[48]|[2468][048]|[13579][26])00)-02-29))T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]\\.[0-9]{7}Z$(?![\\s\\S])";

    private const string CanonicalTextFormat =
        "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    private static readonly Regex CanonicalTextExpression = new(
        CanonicalTextPattern,
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    /// <inheritdoc />
    public override DateTimeOffset Read (
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Artifact publication time must be a JSON string.");
        }

        var text = reader.GetString();
        if (!RegexPatternUtilities.TryIsMatch(
                text!,
                CanonicalTextExpression,
                out var isMatch)
            || !isMatch
            || !DateTimeOffset.TryParseExact(
                text,
                CanonicalTextFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            throw new JsonException(
                "Artifact publication time must use the canonical UTC timestamp form.");
        }

        return value;
    }

    /// <inheritdoc />
    public override void Write (
        Utf8JsonWriter writer,
        DateTimeOffset value,
        JsonSerializerOptions options)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new JsonException("Artifact publication time must use the UTC offset.");
        }

        writer.WriteStringValue(value.ToString(
            CanonicalTextFormat,
            CultureInfo.InvariantCulture));
    }
}
