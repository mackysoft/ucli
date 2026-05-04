using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;

namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Converts catalog entries into validated operation descriptors. </summary>
internal static class OperationDescriptorMapper
{
    /// <summary> Maps one validated operation-entry collection into descriptor values. </summary>
    /// <param name="operations"> The validated operation-entry collection. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped descriptor collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operations" /> is <see langword="null" />. </exception>
    /// <exception cref="InvalidOperationException"> Thrown when any entry cannot be represented as one descriptor. </exception>
    public static IReadOnlyList<UcliOperationDescriptor> Map (
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var descriptors = new UcliOperationDescriptor[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var operation = operations[i];
            if (operation == null)
            {
                throw new InvalidOperationException("Operation catalog contains a null entry.");
            }

            if (!UcliOperationKindCodec.TryParse(operation.Kind, out var kind))
            {
                throw new InvalidOperationException(
                    $"Operation kind is invalid for '{operation.Name}'.");
            }

            if (!OperationPolicyCodec.TryParse(operation.Policy, out var policy))
            {
                throw new InvalidOperationException(
                    $"Operation policy is invalid for '{operation.Name}'.");
            }

            descriptors[i] = new UcliOperationDescriptor(
                Name: operation.Name!,
                Kind: kind,
                Policy: policy,
                ArgsSchemaJson: operation.ArgsSchemaJson!,
                ResultSchemaJson: operation.ResultSchemaJson);
        }

        return descriptors;
    }
}
