using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts operation input constraint kinds between enum and contract literals. </summary>
public static class UcliOperationInputConstraintKindCodec
{
    private static readonly (UcliOperationInputConstraintKind Value, string Literal)[] Mappings =
    {
        (UcliOperationInputConstraintKind.NonEmpty, UcliOperationInputConstraintKindValues.NonEmpty),
        (UcliOperationInputConstraintKind.Range, UcliOperationInputConstraintKindValues.Range),
        (UcliOperationInputConstraintKind.ProjectRelativePath, UcliOperationInputConstraintKindValues.ProjectRelativePath),
        (UcliOperationInputConstraintKind.AssetExists, UcliOperationInputConstraintKindValues.AssetExists),
        (UcliOperationInputConstraintKind.AssetCreatable, UcliOperationInputConstraintKindValues.AssetCreatable),
        (UcliOperationInputConstraintKind.GlobalObjectId, UcliOperationInputConstraintKindValues.GlobalObjectId),
        (UcliOperationInputConstraintKind.HierarchyPath, UcliOperationInputConstraintKindValues.HierarchyPath),
        (UcliOperationInputConstraintKind.ReferenceResolvable, UcliOperationInputConstraintKindValues.ReferenceResolvable),
        (UcliOperationInputConstraintKind.TypeExists, UcliOperationInputConstraintKindValues.TypeExists),
        (UcliOperationInputConstraintKind.TypeAssignableTo, UcliOperationInputConstraintKindValues.TypeAssignableTo),
        (UcliOperationInputConstraintKind.SerializedProperty, UcliOperationInputConstraintKindValues.SerializedProperty),
        (UcliOperationInputConstraintKind.AssetGuid, UcliOperationInputConstraintKindValues.AssetGuid),
        (UcliOperationInputConstraintKind.Cursor, UcliOperationInputConstraintKindValues.Cursor),
    };

    /// <summary> Converts one constraint kind enum value to its contract literal. </summary>
    /// <param name="kind"> The constraint kind enum value. </param>
    /// <returns> The contract literal value. </returns>
    public static string ToValue (UcliOperationInputConstraintKind kind)
    {
        return LiteralCodecUtilities.ToValue(
            kind,
            Mappings,
            nameof(kind),
            "Unsupported operation input constraint kind.");
    }
}
