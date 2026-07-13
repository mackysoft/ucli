using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity GlobalObjectId string used for exact object resolution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity GlobalObjectId string used for exact object resolution.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.GlobalObjectId)]
public sealed record UnityGlobalObjectId : UcliStringValue
{
    private const int AssetGuidTextLength = 32;
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
        : base(parsedValue.CanonicalValue)
    {
        Kind = parsedValue.Kind;
        AssetGuid = parsedValue.AssetGuid;
        TargetObjectId = parsedValue.TargetObjectId;
        TargetPrefabId = parsedValue.TargetPrefabId;
    }

    /// <summary> Gets the object category encoded by this identifier. </summary>
    public UnityGlobalObjectIdKind Kind { get; }

    /// <summary> Gets the non-empty Unity asset GUID encoded by this identifier. </summary>
    public UnityAssetGuid AssetGuid { get; }

    /// <summary> Gets the object identifier within the asset or scene. </summary>
    public ulong TargetObjectId { get; }

    /// <summary> Gets the corresponding prefab object identifier, or zero when no prefab object is encoded. </summary>
    public ulong TargetPrefabId { get; }

    /// <summary> Attempts to parse and canonicalize one non-null Unity GlobalObjectId. </summary>
    /// <param name="value"> The candidate Unity GlobalObjectId text. </param>
    /// <param name="globalObjectId"> The canonical typed identifier when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value has the supported non-null V1 structure; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
    {
        globalObjectId = null;
        if (value == null)
        {
            return false;
        }

        try
        {
            globalObjectId = new UnityGlobalObjectId(value);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static ParsedValue Parse (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw CreateInvalidValueException();
        }

        var identifierTypeEnd = value.IndexOf('-', Prefix.Length);
        var assetGuidEnd = identifierTypeEnd < 0 ? -1 : value.IndexOf('-', identifierTypeEnd + 1);
        var targetObjectIdEnd = assetGuidEnd < 0 ? -1 : value.IndexOf('-', assetGuidEnd + 1);
        if (identifierTypeEnd < 0
            || assetGuidEnd < 0
            || targetObjectIdEnd < 0
            || value.IndexOf('-', targetObjectIdEnd + 1) >= 0)
        {
            throw CreateInvalidValueException();
        }

        if (!int.TryParse(
                value.AsSpan(Prefix.Length, identifierTypeEnd - Prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var identifierType)
            || !TryGetKind(identifierType, out var kind))
        {
            throw CreateInvalidValueException();
        }

        var assetGuidText = value.Substring(identifierTypeEnd + 1, assetGuidEnd - identifierTypeEnd - 1);
        if (!UnityAssetGuid.TryParse(assetGuidText, out var assetGuid)
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
            throw CreateInvalidValueException();
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
            (IdentifierType: identifierType, AssetGuid: assetGuid.Value, TargetObjectId: targetObjectId, TargetPrefabId: targetPrefabId),
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
                state.AssetGuid.AsSpan().CopyTo(destination[offset..]);
                offset += AssetGuidTextLength;
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

        return new ParsedValue(
            canonicalValue,
            kind,
            assetGuid,
            targetObjectId,
            targetPrefabId);
    }

    private static bool TryGetKind (
        int value,
        out UnityGlobalObjectIdKind kind)
    {
        switch (value)
        {
            case (int)UnityGlobalObjectIdKind.ImportedAsset:
                kind = UnityGlobalObjectIdKind.ImportedAsset;
                return true;
            case (int)UnityGlobalObjectIdKind.SceneObject:
                kind = UnityGlobalObjectIdKind.SceneObject;
                return true;
            case (int)UnityGlobalObjectIdKind.SourceAsset:
                kind = UnityGlobalObjectIdKind.SourceAsset;
                return true;
            case (int)UnityGlobalObjectIdKind.BuiltInAsset:
                kind = UnityGlobalObjectIdKind.BuiltInAsset;
                return true;
            default:
                kind = default;
                return false;
        }
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

    private readonly record struct ParsedValue (
        string CanonicalValue,
        UnityGlobalObjectIdKind Kind,
        UnityAssetGuid AssetGuid,
        ulong TargetObjectId,
        ulong TargetPrefabId);
}
