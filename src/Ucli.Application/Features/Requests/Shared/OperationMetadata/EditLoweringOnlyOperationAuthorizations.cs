using MackySoft.Ucli.Application.Shared.Execution.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Provides authorization facts for primitives reachable only through edit lowering. </summary>
internal static class EditLoweringOnlyOperationAuthorizations
{
    /// <summary> Gets the edit-lowering authorization facts. </summary>
    public static IReadOnlyList<UcliOperationAuthorizationDescriptor> All { get; } =
        EditLoweringOnlyPrimitiveOperationNames.All
            .Select(Create)
            .ToArray();

    /// <summary> Appends missing edit-lowering-only facts to one authorization catalog. </summary>
    /// <param name="operations"> The authorization facts projected from public operation descriptors. </param>
    /// <returns> Authorization facts that include every primitive required by edit lowering. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operations" /> is <see langword="null" />. </exception>
    public static IReadOnlyList<UcliOperationAuthorizationDescriptor> AppendMissingTo (
        IReadOnlyList<UcliOperationAuthorizationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationNames = new HashSet<string>(operations.Count + All.Count, StringComparer.Ordinal);
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            operationNames.Add(operation.Name);
        }

        var missingCount = 0;
        for (var i = 0; i < All.Count; i++)
        {
            if (!operationNames.Contains(All[i].Name))
            {
                missingCount++;
            }
        }

        if (missingCount == 0)
        {
            return operations;
        }

        var mergedOperations =
            new List<UcliOperationAuthorizationDescriptor>(operations.Count + missingCount);
        for (var i = 0; i < operations.Count; i++)
        {
            mergedOperations.Add(operations[i]);
        }

        for (var i = 0; i < All.Count; i++)
        {
            var descriptor = All[i];
            if (operationNames.Add(descriptor.Name))
            {
                mergedOperations.Add(descriptor);
            }
        }

        return mergedOperations;
    }

    private static UcliOperationAuthorizationDescriptor Create (string operationName)
    {
        return new UcliOperationAuthorizationDescriptor(
            Name: operationName,
            Policy: OperationPolicy.Advanced,
            Exposure: UcliOperationExposure.EditLoweringOnly);
    }
}
