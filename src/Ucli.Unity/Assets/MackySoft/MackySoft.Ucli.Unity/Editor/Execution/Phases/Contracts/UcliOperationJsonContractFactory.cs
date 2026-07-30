using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Generates one operation describe contract from the effective Unity serializer contracts. </summary>
    internal static class UcliOperationJsonContractFactory
    {
        internal static UcliOperationDescribeContract CreateWithoutVerdict<TArgs, TResult> (
            string operationName,
            string description,
            UcliOperationAssuranceContract assurance,
            UcliOperationCodeContract? codeContract)
        {
            return UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
                contractGenerationResult: Generate<TArgs, TResult>(operationName),
                description: description,
                assurance: assurance,
                codeContract: codeContract);
        }

        internal static UcliOperationDescribeContract CreateJudging<TArgs, TResult> (
            string operationName,
            string description,
            UcliOperationVerdictContract verdictContract,
            UcliOperationAssuranceContract assurance,
            UcliOperationCodeContract? codeContract)
        {
            if (verdictContract == null)
            {
                throw new ArgumentNullException(nameof(verdictContract));
            }

            return UcliOperationDescribeContractBuilder.CreateJudging(
                contractGenerationResult: Generate<TArgs, TResult>(operationName),
                description: description,
                assurance: assurance,
                verdictContract: verdictContract,
                codeContract: codeContract);
        }

        private static UcliOperationJsonContractGenerationResult Generate<TArgs, TResult> (
            string operationName)
        {
            var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
            var result = UcliOperationJsonContractGenerator.Generate(
                operationName,
                serializerOptions.GetTypeInfo(typeof(TArgs)),
                typeof(TResult) == typeof(UcliNoResult)
                    ? null
                    : serializerOptions.GetTypeInfo(typeof(TResult)));
            EnsurePublicArgsContract<TArgs>(result);
            return result;
        }

        private static void EnsurePublicArgsContract<TArgs> (
            UcliOperationJsonContractGenerationResult result)
        {
            if (!UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(
                    result,
                    out var error))
            {
                throw new ArgumentException(error, nameof(TArgs));
            }
        }
    }
}
