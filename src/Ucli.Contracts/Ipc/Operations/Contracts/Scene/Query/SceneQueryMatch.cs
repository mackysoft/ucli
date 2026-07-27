using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

[Description("Single scene query match.")]
public sealed record SceneQueryMatch
{
    /// <summary> Initializes a scene-object or component match. </summary>
    /// <param name="kind"> The GameObject or component target kind. </param>
    /// <param name="hierarchyPath"> The non-null path of the matched GameObject. </param>
    /// <param name="componentType"> The component type for a component match; otherwise <see langword="null" />. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="kind" /> is neither <see cref="UcliOperationReferenceTargetKind.GameObject" /> nor <see cref="UcliOperationReferenceTargetKind.Component" />. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="hierarchyPath" /> is <see langword="null" />, or a component match has no <paramref name="componentType" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a GameObject match has a <paramref name="componentType" />. </exception>
    [JsonConstructor]
    public SceneQueryMatch (
        UcliOperationReferenceTargetKind kind,
        UnityHierarchyPath hierarchyPath,
        UnityComponentTypeId? componentType)
    {
        if (kind is not UcliOperationReferenceTargetKind.GameObject
            and not UcliOperationReferenceTargetKind.Component)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Scene query target kind must be GameObject or Component.");
        }

        Kind = kind;
        HierarchyPath = hierarchyPath ?? throw new ArgumentNullException(nameof(hierarchyPath));
        if (kind == UcliOperationReferenceTargetKind.Component && componentType == null)
        {
            throw new ArgumentNullException(nameof(componentType), "A component match requires a component type.");
        }

        if (kind == UcliOperationReferenceTargetKind.GameObject && componentType != null)
        {
            throw new ArgumentException("A GameObject match must not specify a component type.", nameof(componentType));
        }

        ComponentType = componentType;
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Matched target kind.")]
    public UcliOperationReferenceTargetKind Kind { get; private init; }

    [JsonInclude]
    [JsonRequired]
    [Description("Matched GameObject hierarchy path.")]
    public UnityHierarchyPath HierarchyPath { get; private init; }

    [Description("Matched component type identifier for component matches.")]
    public UnityComponentTypeId? ComponentType { get; }
}
