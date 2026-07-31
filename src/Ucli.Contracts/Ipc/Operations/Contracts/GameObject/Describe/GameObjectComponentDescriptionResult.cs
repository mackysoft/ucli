using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Component entry in a GameObject description.")]
public sealed record GameObjectComponentDescriptionResult
{
    /// <summary> Initializes a component description. </summary>
    /// <param name="typeName"> The component type identifier, or <see langword="null" /> for a missing script. </param>
    [JsonConstructor]
    public GameObjectComponentDescriptionResult (UnityComponentTypeId? typeName)
    {
        TypeName = typeName;
    }

    [Description("Component type name, or null when the component script is missing.")]
    public UnityComponentTypeId? TypeName { get; }
}
