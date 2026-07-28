using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Builds operation describe contracts from authoritative provider generation results. </summary>
public static class UcliOperationDescribeContractBuilder
{
    /// <summary>
    /// Creates an operation describe contract from an already generated JSON contract aggregate.
    /// </summary>
    /// <param name="contractGenerationResult">
    /// The authoritative args and optional result generation results for the operation.
    /// </param>
    /// <param name="description"> The operation purpose description. </param>
    /// <param name="assurance"> The operation assurance metadata. </param>
    /// <param name="codeContract"> The optional source-facing code contract. </param>
    /// <returns> The operation describe contract. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="contractGenerationResult" /> or <paramref name="assurance" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="description" /> is empty or whitespace.
    /// </exception>
    public static UcliOperationDescribeContract Create (
        UcliOperationJsonContractGenerationResult contractGenerationResult,
        string description,
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract = null)
    {
        if (contractGenerationResult == null)
        {
            throw new ArgumentNullException(nameof(contractGenerationResult));
        }

        ValidateDescribeMetadata(description, assurance);
        return new UcliOperationDescribeContract(
            description,
            contractGenerationResult.ArgsContract,
            contractGenerationResult.ResultContract,
            assurance,
            codeContract);
    }

    private static void ValidateDescribeMetadata (
        string description,
        UcliOperationAssuranceContract assurance)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Operation description must not be null, empty, or whitespace.",
                nameof(description));
        }

        if (assurance == null)
        {
            throw new ArgumentNullException(nameof(assurance));
        }
    }
}
