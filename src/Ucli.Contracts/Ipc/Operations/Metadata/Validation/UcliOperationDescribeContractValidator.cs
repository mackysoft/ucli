using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationDescribeContractValidator
{
    public static bool TryValidatePublicRawOpDescribeContract (
        UcliOperationDescribeContract? describeContract,
        string ownerName,
        out string errorMessage)
    {
        return TryValidatePublicRawOpDescribeContractCore(
            describeContract,
            operationKind: null,
            operationPolicy: null,
            ownerName,
            allowMayCreatePreviewState: false,
            out _,
            out errorMessage);
    }

    public static bool TryValidatePublicRawOpDescribeContract (
        UcliOperationDescribeContract? describeContract,
        UcliOperationKind? operationKind,
        OperationPolicy? operationPolicy,
        string ownerName,
        out string errorMessage)
    {
        return TryValidatePublicRawOpDescribeContractCore(
            describeContract,
            operationKind,
            operationPolicy,
            ownerName,
            allowMayCreatePreviewState: false,
            out _,
            out errorMessage);
    }

    public static bool TryValidatePublicRawOpDescribeContractAndDerivePolicy (
        UcliOperationDescribeContract? describeContract,
        UcliOperationKind? operationKind,
        string ownerName,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        return TryValidatePublicRawOpDescribeContractCore(
            describeContract,
            operationKind,
            operationPolicy: null,
            ownerName,
            allowMayCreatePreviewState: false,
            out derivedPolicy,
            out errorMessage);
    }

    public static bool TryValidateRegisteredOperationDescribeContractAndDerivePolicy (
        UcliOperationDescribeContract? describeContract,
        UcliOperationKind? operationKind,
        string ownerName,
        UcliOperationExposure exposure,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        return TryValidatePublicRawOpDescribeContractCore(
            describeContract,
            operationKind,
            operationPolicy: null,
            ownerName,
            allowMayCreatePreviewState: exposure == UcliOperationExposure.EditLoweringOnly,
            out derivedPolicy,
            out errorMessage);
    }

    public static bool TryValidateRegisteredOperationDescribeContract (
        UcliOperationDescribeContract? describeContract,
        UcliOperationKind? operationKind,
        OperationPolicy? operationPolicy,
        string ownerName,
        UcliOperationExposure exposure,
        out string errorMessage)
    {
        return TryValidatePublicRawOpDescribeContractCore(
            describeContract,
            operationKind,
            operationPolicy,
            ownerName,
            allowMayCreatePreviewState: exposure == UcliOperationExposure.EditLoweringOnly,
            out _,
            out errorMessage);
    }

    private static bool TryValidatePublicRawOpDescribeContractCore (
        UcliOperationDescribeContract? describeContract,
        UcliOperationKind? operationKind,
        OperationPolicy? operationPolicy,
        string ownerName,
        bool allowMayCreatePreviewState,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        derivedPolicy = OperationPolicy.Safe;

        if (describeContract == null
            || string.IsNullOrWhiteSpace(describeContract.Description))
        {
            errorMessage = $"{ownerName} has an invalid describe contract.";
            return false;
        }

        if (!TryValidateGeneratedContract(describeContract.ArgsContract, ownerName, "argsContract", out errorMessage)
            || (describeContract.ResultContract != null
                && !TryValidateGeneratedContract(
                    describeContract.ResultContract,
                    ownerName,
                    "resultContract",
                    out errorMessage))
            || !UcliOperationAssuranceContractValidator.TryValidate(
                describeContract.Assurance,
                operationKind,
                operationPolicy,
                describeContract.CodeContract,
                ownerName,
                allowMayCreatePreviewState,
                out derivedPolicy,
                out errorMessage)
            || !UcliOperationCodeContractValidator.TryValidate(
                describeContract.CodeContract,
                ownerName,
                out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateGeneratedContract (
        UcliOperationJsonContract? contract,
        string ownerName,
        string propertyName,
        out string errorMessage)
    {
        if (!contract.HasValue
            || !contract.Value.IsDefined)
        {
            errorMessage = $"{ownerName} has an invalid {propertyName}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
