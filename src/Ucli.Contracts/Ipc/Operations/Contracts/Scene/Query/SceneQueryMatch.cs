using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single scene query match.")]
public sealed record SceneQueryMatch
{
    [JsonConstructor]
    public SceneQueryMatch (
        string kind,
        string hierarchyPath,
        string? componentType)
    {
        Kind = kind;
        HierarchyPath = hierarchyPath;
        ComponentType = componentType;
    }

    [UcliRequired]
    [UcliDescription("Matched target kind.")]
    public string Kind { get; init; }

    [UcliRequired]
    [UcliDescription("Matched GameObject hierarchy path.")]
    public string HierarchyPath { get; init; }

    [UcliDescription("Matched component type identifier for component matches.")]
    [UcliNullable]
    public string? ComponentType { get; init; }
}
