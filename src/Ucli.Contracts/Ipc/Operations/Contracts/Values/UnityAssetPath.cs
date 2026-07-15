using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Project-relative path to a Unity asset. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Project-relative path to a Unity asset.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.ProjectRelativePath)]
public sealed class UnityAssetPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityAssetPath" /> class. </summary>
    /// <param name="value"> The project-relative asset path. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> does not identify an <c>Assets/</c> descendant. </exception>
    [JsonConstructor]
    public UnityAssetPath (string value)
        : this(new ValidatedPath(UnityAssetPathContract.NormalizeAssetsDescendantPathOrThrow(value)))
    {
    }

    private UnityAssetPath (ValidatedPath path)
        : base(path.Value)
    {
    }

    /// <summary> Attempts to parse and normalize one Unity asset path. </summary>
    /// <param name="value"> The candidate project-relative path. </param>
    /// <param name="path"> The normalized typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the value identifies an <c>Assets/</c> descendant; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityAssetPath? path)
    {
        path = null;
        if (!UnityAssetPathContract.TryNormalizeAssetsDescendantPath(value, out var normalizedPath))
        {
            return false;
        }

        path = new UnityAssetPath(new ValidatedPath(normalizedPath));
        return true;
    }

    /// <summary> Attempts to parse one already-normalized persisted Unity asset path. </summary>
    internal static bool TryParseCanonical (
        string? value,
        [NotNullWhen(true)] out UnityAssetPath? path)
    {
        path = null;
        if (!UnityAssetPathContract.IsNormalizedAssetsDescendantPath(value))
        {
            return false;
        }

        path = new UnityAssetPath(new ValidatedPath(value));
        return true;
    }

    private readonly struct ValidatedPath
    {
        public ValidatedPath (string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
