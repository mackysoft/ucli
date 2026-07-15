using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a Unity hierarchy path inside a selected scene or prefab. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity hierarchy path inside a selected scene or prefab.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.HierarchyPath)]
public sealed class UnityHierarchyPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityHierarchyPath" /> class. </summary>
    /// <param name="value"> The Unity hierarchy path. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> has outer whitespace or an empty hierarchy segment. </exception>
    [JsonConstructor]
    public UnityHierarchyPath (string value)
        : base(UnityHierarchyPathContract.Validate(value))
    {
    }

    /// <summary> Attempts to parse one slash-separated Unity hierarchy path. </summary>
    /// <param name="value"> The candidate hierarchy path. </param>
    /// <param name="path"> The typed path when parsing succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when every hierarchy segment is non-empty; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        [NotNullWhen(true)] out UnityHierarchyPath? path)
    {
        path = null;
        if (!UnityHierarchyPathContract.TryValidate(value, out var validatedValue))
        {
            return false;
        }

        path = new UnityHierarchyPath(validatedValue);
        return true;
    }
}
