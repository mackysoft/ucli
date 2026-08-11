using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("A missing script component slot confirmed in one saved asset.")]
public sealed record MissingScriptSlot
{
    [JsonConstructor]
    public MissingScriptSlot (
        UnityAssetPath assetPath,
        UnityHierarchyPath hierarchyPath,
        int componentIndex)
    {
        if (componentIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentIndex), componentIndex, "Missing script component index must not be negative.");
        }

        AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
        HierarchyPath = hierarchyPath ?? throw new ArgumentNullException(nameof(hierarchyPath));
        ComponentIndex = componentIndex;
    }

    [JsonInclude]
    [JsonRequired]
    public UnityAssetPath AssetPath { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public UnityHierarchyPath HierarchyPath { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [UcliInt32Minimum(0)]
    public int ComponentIndex { get; private init; }
}
