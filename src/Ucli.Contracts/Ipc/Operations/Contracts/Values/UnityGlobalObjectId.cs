using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity GlobalObjectId string used for exact object resolution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity GlobalObjectId string used for exact object resolution.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.GlobalObjectId)]
public sealed class UnityGlobalObjectId : UcliStringValue
{
    private const int AssetGuidTextLength = 32;
    private const int ImportedAssetIdentifierType = 1;
    private const int SceneObjectIdentifierType = 2;
    private const int SourceAssetIdentifierType = 3;
    private const int BuiltInAssetIdentifierType = 4;
    private const string Prefix = "GlobalObjectId_V1-";

    /// <summary> Initializes a new instance of the <see cref="UnityGlobalObjectId" /> class. </summary>
    /// <param name="value"> The Unity GlobalObjectId string. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a supported non-null V1 GlobalObjectId. </exception>
    [JsonConstructor]
    public UnityGlobalObjectId (string value)
        : this(Parse(value))
    {
    }

    private UnityGlobalObjectId (ParsedValue parsedValue)
        : base(parsedValue.Value)
    {
    }

    /// <summary> Attempts to parse and canonicalize one non-null Unity GlobalObjectId. </summary>
    /// <param name="value"> The candidate Unity GlobalObjectId text. </param>
    /// <param name="globalObjectId"> The canonical typed identifier when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value has the supported non-null V1 structure; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
    {
        globalObjectId = null;
        if (!TryParseCore(value, out var parsedValue))
        {
            return false;
        }

        globalObjectId = new UnityGlobalObjectId(parsedValue);
        return true;
    }

    private static ParsedValue Parse (string? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!TryParseCore(value, out var parsedValue))
        {
            throw CreateInvalidValueException();
        }

        return parsedValue;
    }

    private static bool TryParseCore (
        string? value,
        out ParsedValue parsedValue)
    {
        parsedValue = default;
        if (value == null
            || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var identifierTypeEnd = value.IndexOf('-', Prefix.Length);
        var assetGuidEnd = identifierTypeEnd < 0 ? -1 : value.IndexOf('-', identifierTypeEnd + 1);
        var targetObjectIdEnd = assetGuidEnd < 0 ? -1 : value.IndexOf('-', assetGuidEnd + 1);
        if (identifierTypeEnd < 0
            || assetGuidEnd < 0
            || targetObjectIdEnd < 0
            || value.IndexOf('-', targetObjectIdEnd + 1) >= 0)
        {
            return false;
        }

        if (!int.TryParse(
                value.AsSpan(Prefix.Length, identifierTypeEnd - Prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var identifierType)
            || !IsSupportedIdentifierType(identifierType))
        {
            return false;
        }

        var assetGuidText = value.AsSpan(identifierTypeEnd + 1, assetGuidEnd - identifierTypeEnd - 1);
        if (!Guid.TryParseExact(assetGuidText, "N", out var assetGuid)
            || assetGuid == Guid.Empty
            || !ulong.TryParse(
                value.AsSpan(assetGuidEnd + 1, targetObjectIdEnd - assetGuidEnd - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var targetObjectId)
            || !ulong.TryParse(
                value.AsSpan(targetObjectIdEnd + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var targetPrefabId))
        {
            return false;
        }

        var canonicalValueLength = checked(
            Prefix.Length
            + GetInvariantDecimalLength((ulong)identifierType)
            + 1
            + AssetGuidTextLength
            + 1
            + GetInvariantDecimalLength(targetObjectId)
            + 1
            + GetInvariantDecimalLength(targetPrefabId));
        var canonicalValue = string.Create(
            canonicalValueLength,
            (IdentifierType: identifierType, AssetGuid: assetGuid, TargetObjectId: targetObjectId, TargetPrefabId: targetPrefabId),
            static (destination, state) =>
            {
                Prefix.AsSpan().CopyTo(destination);
                var offset = Prefix.Length;

                if (!state.IdentifierType.TryFormat(
                        destination[offset..],
                        out var identifierTypeCharsWritten,
                        provider: CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException("GlobalObjectId buffer is too small for identifier type formatting.");
                }

                offset += identifierTypeCharsWritten;
                destination[offset++] = '-';
                if (!state.AssetGuid.TryFormat(
                        destination[offset..],
                        out var assetGuidCharsWritten,
                        "N")
                    || assetGuidCharsWritten != AssetGuidTextLength)
                {
                    throw new InvalidOperationException("GlobalObjectId buffer is too small for asset GUID formatting.");
                }

                offset += assetGuidCharsWritten;
                destination[offset++] = '-';

                if (!state.TargetObjectId.TryFormat(
                        destination[offset..],
                        out var targetObjectIdCharsWritten,
                        provider: CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException("GlobalObjectId buffer is too small for target object id formatting.");
                }

                offset += targetObjectIdCharsWritten;
                destination[offset++] = '-';

                if (!state.TargetPrefabId.TryFormat(
                        destination[offset..],
                        out var targetPrefabIdCharsWritten,
                        provider: CultureInfo.InvariantCulture))
                {
                    throw new InvalidOperationException("GlobalObjectId buffer is too small for target prefab id formatting.");
                }

                offset += targetPrefabIdCharsWritten;
                if (offset != destination.Length)
                {
                    throw new InvalidOperationException("GlobalObjectId canonical length calculation is inconsistent with formatting.");
                }
            });

        parsedValue = new ParsedValue(canonicalValue);
        return true;
    }

    private static bool IsSupportedIdentifierType (int value)
    {
        return value switch
        {
            ImportedAssetIdentifierType
                or SceneObjectIdentifierType
                or SourceAssetIdentifierType
                or BuiltInAssetIdentifierType => true,
            _ => false,
        };
    }

    private static int GetInvariantDecimalLength (ulong value)
    {
        var length = 1;
        while (value >= 10)
        {
            value /= 10;
            length++;
        }

        return length;
    }

    private static ArgumentException CreateInvalidValueException ()
    {
        return new ArgumentException(
            "Unity GlobalObjectId must use 'GlobalObjectId_V1-{type}-{assetGuid}-{targetObjectId}-{targetPrefabId}' with a supported non-null type, a non-zero 32-character hexadecimal asset GUID, and unsigned 64-bit object identifiers.",
            "value");
    }

    private readonly record struct ParsedValue (string Value);

}
