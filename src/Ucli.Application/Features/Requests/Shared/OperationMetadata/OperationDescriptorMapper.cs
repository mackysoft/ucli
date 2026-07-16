namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Converts catalog entries into validated operation descriptors. </summary>
internal static class OperationDescriptorMapper
{
    /// <summary> Maps one validated operation-entry collection into descriptor values. </summary>
    /// <param name="operations"> The validated operation-entry collection. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The mapped descriptor collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operations" /> is <see langword="null" />. </exception>
    public static IReadOnlyList<UcliOperationDescriptor> Map (
        IReadOnlyList<ValidatedOpsOperation> operations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var descriptors = new UcliOperationDescriptor[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var operation = operations[i];

            descriptors[i] = new UcliOperationDescriptor(
                Name: operation.Name,
                Kind: operation.Kind,
                Policy: operation.Policy,
                ArgsSchemaJson: operation.ArgsSchema.GetRawText(),
                ResultSchemaJson: operation.ResultSchema?.GetRawText(),
                Exposure: operation.Exposure);
        }

        return descriptors;
    }
}
