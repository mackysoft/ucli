using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Non-empty Unity hierarchy path prefix used to filter scene objects. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Non-empty Unity hierarchy path prefix used to filter scene objects.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.HierarchyPath)]
public sealed record UnityHierarchyPathPrefix : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityHierarchyPathPrefix" /> class. </summary>
    /// <param name="value"> The Unity hierarchy path prefix. </param>
    [JsonConstructor]
    public UnityHierarchyPathPrefix (string value)
        : base(value)
    {
    }

    /// <summary> Converts a string to a hierarchy path prefix contract value. </summary>
    /// <param name="value"> The Unity hierarchy path prefix. </param>
    /// <returns> The semantic hierarchy path prefix value. </returns>
    public static implicit operator UnityHierarchyPathPrefix (string value)
    {
        return new UnityHierarchyPathPrefix(value);
    }
}
