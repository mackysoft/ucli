using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity hierarchy path inside a selected scene or prefab. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity hierarchy path inside a selected scene or prefab.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.HierarchyPath)]
public sealed record UnityHierarchyPath : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityHierarchyPath" /> class. </summary>
    /// <param name="value"> The Unity hierarchy path. </param>
    [JsonConstructor]
    public UnityHierarchyPath (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a hierarchy path contract value. </summary>
    /// <param name="value"> The Unity hierarchy path. </param>
    /// <returns> The semantic hierarchy path value. </returns>
    public static implicit operator UnityHierarchyPath (string value)
    {
        return new UnityHierarchyPath(value);
    }
}
