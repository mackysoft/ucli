using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Builds operation describe contracts from authoritative provider generation results. </summary>
public static class UcliOperationDescribeContractBuilder
{
    /// <summary>
    /// Creates a non-judging operation describe contract from an already generated JSON contract aggregate.
    /// </summary>
    /// <param name="contractGenerationResult">
    /// The authoritative args and optional result generation results for the operation.
    /// </param>
    /// <param name="description"> The operation purpose description. </param>
    /// <param name="assurance"> The operation assurance metadata. </param>
    /// <param name="codeContract"> The optional source-facing code contract. </param>
    /// <returns> The non-judging operation describe contract. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="contractGenerationResult" /> or <paramref name="assurance" /> is
    /// <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="description" /> is empty or whitespace.
    /// </exception>
    public static UcliOperationDescribeContract CreateWithoutVerdict (
        UcliOperationJsonContractGenerationResult contractGenerationResult,
        string description,
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract)
    {
        return CreateCore(
            contractGenerationResult,
            description,
            assurance,
            verdictContract: null,
            codeContract);
    }

    /// <summary>
    /// Creates a judging operation describe contract from an already generated JSON contract aggregate.
    /// </summary>
    /// <param name="contractGenerationResult">
    /// The authoritative args and result generation results for the operation.
    /// </param>
    /// <param name="description"> The operation purpose description. </param>
    /// <param name="assurance"> The operation assurance metadata. </param>
    /// <param name="verdictContract"> The condition judged from a successful Call result. </param>
    /// <param name="codeContract"> The optional source-facing code contract. </param>
    /// <returns> The judging operation describe contract. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="contractGenerationResult" />, <paramref name="assurance" />, or
    /// <paramref name="verdictContract" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="description" /> is empty or whitespace.
    /// </exception>
    public static UcliOperationDescribeContract CreateJudging (
        UcliOperationJsonContractGenerationResult contractGenerationResult,
        string description,
        UcliOperationAssuranceContract assurance,
        UcliOperationVerdictContract verdictContract,
        UcliOperationCodeContract? codeContract)
    {
        if (verdictContract == null)
        {
            throw new ArgumentNullException(nameof(verdictContract));
        }

        if (contractGenerationResult == null)
        {
            throw new ArgumentNullException(nameof(contractGenerationResult));
        }

        if (contractGenerationResult.ResultContract == null)
        {
            throw new ArgumentException(
                "A judging operation must declare a generated result contract.",
                nameof(contractGenerationResult));
        }

        return CreateCore(
            contractGenerationResult,
            description,
            assurance,
            verdictContract,
            codeContract);
    }

    private static UcliOperationDescribeContract CreateCore (
        UcliOperationJsonContractGenerationResult contractGenerationResult,
        string description,
        UcliOperationAssuranceContract assurance,
        UcliOperationVerdictContract? verdictContract,
        UcliOperationCodeContract? codeContract)
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
            verdictContract,
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
