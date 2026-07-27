using System;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Generates one operation describe contract from the effective Unity serializer contracts. </summary>
    internal static class UcliOperationJsonContractFactory
    {
        internal static UcliOperationDescribeContract Create<TArgs, TResult> (
            string operationName,
            string description,
            UcliOperationAssuranceContract assurance,
            UcliOperationCodeContract? codeContract)
        {
            var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
            var result = UcliOperationJsonContractGenerator.Generate(
                operationName,
                serializerOptions.GetTypeInfo(typeof(TArgs)),
                typeof(TResult) == typeof(UcliNoResult)
                    ? null
                    : serializerOptions.GetTypeInfo(typeof(TResult)));
            EnsurePublicArgsContract<TArgs>(result);
            return UcliOperationDescribeContractBuilder.Create(
                result,
                description,
                assurance,
                codeContract);
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
