namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates uCLI-specific public raw-operation registration rules. </summary>
public static class UcliOperationContractValidator
{
    /// <summary>
    /// Validates that one generated args contract does not expose reserved public raw-operation property names.
    /// </summary>
    /// <param name="operationContractGenerationResult">
    /// The authoritative uCLI aggregate generated from the effective public raw-operation serializer contract.
    /// </param>
    /// <param name="errorMessage"> The validation error message when validation fails. </param>
    /// <returns>
    /// <see langword="true" /> when the generated contract can be exposed through public raw-operation metadata;
    /// otherwise <see langword="false" />.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="operationContractGenerationResult" /> is <see langword="null" />.
    /// </exception>
    public static bool TryValidatePublicRawOpReservedProperties (
        UcliOperationJsonContractGenerationResult operationContractGenerationResult,
        out string errorMessage)
    {
        if (operationContractGenerationResult == null)
        {
            throw new ArgumentNullException(nameof(operationContractGenerationResult));
        }

        return UcliOperationPublicRawOpReservedPropertyValidator.TryValidate(
            operationContractGenerationResult.ArgsContractModel,
            out errorMessage);
    }
}
