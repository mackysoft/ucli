using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity asset GUID string used for exact asset resolution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity asset GUID string used for exact asset resolution.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.AssetGuid)]
public sealed record UnityAssetGuid : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityAssetGuid" /> class. </summary>
    /// <param name="value"> The Unity asset GUID string. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not a non-zero <c>N</c>-format GUID. </exception>
    [JsonConstructor]
    public UnityAssetGuid (string value)
        : this(Parse(value))
    {
    }

    private UnityAssetGuid (Guid guid)
        : base(guid.ToString("N"))
    {
    }

    /// <summary> Attempts to parse and canonicalize one Unity asset GUID. </summary>
    /// <param name="value"> The candidate 32-character hexadecimal <c>N</c>-format value. </param>
    /// <param name="assetGuid"> The typed asset GUID when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> is a non-zero <c>N</c>-format GUID; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityAssetGuid? assetGuid)
    {
        assetGuid = null;
        if (!Guid.TryParseExact(value, "N", out var guid) || guid == Guid.Empty)
        {
            return false;
        }

        assetGuid = new UnityAssetGuid(guid);
        return true;
    }

    private static Guid Parse (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (!Guid.TryParseExact(value, "N", out var guid) || guid == Guid.Empty)
        {
            throw new ArgumentException(
                "Unity asset GUID must be a non-zero 32-character hexadecimal N-format GUID.",
                nameof(value));
        }

        return guid;
    }
}
