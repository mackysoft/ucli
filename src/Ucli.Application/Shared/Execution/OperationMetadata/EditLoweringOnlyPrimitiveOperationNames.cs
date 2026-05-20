using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.OperationMetadata;

/// <summary> Defines primitive operation names that are reachable only through public edit lowering. </summary>
internal static class EditLoweringOnlyPrimitiveOperationNames
{
    /// <summary> Gets the edit-lowering-only primitive operation names. </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        UcliPrimitiveOperationNames.AssetCreate,
        UcliPrimitiveOperationNames.AssetSet,
        UcliPrimitiveOperationNames.GoCreate,
        UcliPrimitiveOperationNames.CompEnsure,
        UcliPrimitiveOperationNames.CompSet,
        UcliPrimitiveOperationNames.PrefabCreate,
    ];

    /// <summary> Determines whether the operation name is an edit-lowering-only primitive. </summary>
    /// <param name="operationName"> The operation name to check. </param>
    /// <returns> <see langword="true" /> when the operation is not part of the public raw projection; otherwise <see langword="false" />. </returns>
    public static bool Contains (string? operationName)
    {
        if (string.IsNullOrWhiteSpace(operationName))
        {
            return false;
        }

        for (var i = 0; i < All.Count; i++)
        {
            if (string.Equals(operationName, All[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
