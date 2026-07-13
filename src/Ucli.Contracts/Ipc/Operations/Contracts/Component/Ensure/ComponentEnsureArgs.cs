using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Component ensure operation arguments.")]
public sealed record ComponentEnsureArgs
{
    [JsonConstructor]
    public ComponentEnsureArgs (
        GameObjectReferenceArgs target,
        UnityComponentTypeId type)
    {
        Target = target;
        Type = type;
    }

    [UcliRequired]
    [UcliDescription("Target GameObject that should contain the component.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.ReferenceResolvable, TargetKind = UcliOperationReferenceTargetKind.GameObject)]
    public GameObjectReferenceArgs Target { get; init; }

    [UcliRequired]
    [UcliDescription("Component type identifier to ensure.")]
    public UnityComponentTypeId Type { get; init; }
}
