using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

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
}
