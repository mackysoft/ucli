using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Single scene query match.")]
public sealed record SceneQueryMatch
{
    [JsonConstructor]
    public SceneQueryMatch (
        string kind,
        UnityHierarchyPath hierarchyPath,
        UnityComponentTypeId? componentType)
    {
        Kind = kind;
        HierarchyPath = hierarchyPath;
        ComponentType = componentType;
    }

    public SceneQueryMatch (
        string kind,
        string hierarchyPath,
        string? componentType)
        : this(
            kind,
            new UnityHierarchyPath(hierarchyPath),
            componentType == null ? null : new UnityComponentTypeId(componentType))
    {
    }

    [UcliRequired]
    [UcliDescription("Matched target kind.")]
    public string Kind { get; init; }

    [UcliRequired]
    [UcliDescription("Matched GameObject hierarchy path.")]
    public UnityHierarchyPath HierarchyPath { get; init; }

    [UcliDescription("Matched component type identifier for component matches.")]
    [UcliJsonAllowNull]
    public UnityComponentTypeId? ComponentType { get; init; }
}
