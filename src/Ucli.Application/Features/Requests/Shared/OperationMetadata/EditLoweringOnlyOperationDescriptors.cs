using MackySoft.Ucli.Application.Shared.Execution.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Provides validation-only descriptors for primitives reachable only through edit lowering. </summary>
internal static class EditLoweringOnlyOperationDescriptors
{
    private const string AuthorizationOnlyArgsSchemaJson = """{"type":"object"}""";

    /// <summary> Gets the validation-only edit-lowering descriptors. </summary>
    public static IReadOnlyList<UcliOperationDescriptor> All { get; } =
        EditLoweringOnlyPrimitiveOperationNames.All
            .Select(Create)
            .ToArray();

    /// <summary> Appends missing edit-lowering-only descriptors to one request validation catalog projection. </summary>
    /// <param name="operations"> The public operation descriptors loaded from source or read-index metadata. </param>
    /// <returns> A validation catalog that can authorize public edit lowering without exposing hidden primitives in public ops catalog output. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operations" /> is <see langword="null" />. </exception>
    public static IReadOnlyList<UcliOperationDescriptor> AppendMissingTo (IReadOnlyList<UcliOperationDescriptor> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationNames = new HashSet<string>(operations.Count + All.Count, StringComparer.Ordinal);
        for (var i = 0; i < operations.Count; i++)
        {
            var operation = operations[i];
            if (operation is not null)
            {
                operationNames.Add(operation.Name);
            }
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

        var mergedOperations = new List<UcliOperationDescriptor>(operations.Count + missingCount);
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

    private static UcliOperationDescriptor Create (string operationName)
    {
        // NOTE: Request static validation uses these descriptors only for exposure and authorization.
        // Public raw argument validation never uses this non-public overlay.
        return new UcliOperationDescriptor(
            Name: operationName,
            Kind: UcliOperationKind.Mutation,
            Policy: OperationPolicy.Advanced,
            ArgsSchemaJson: AuthorizationOnlyArgsSchemaJson,
            ResultSchemaJson: null,
            Exposure: UcliOperationExposure.EditLoweringOnly);
    }
}
