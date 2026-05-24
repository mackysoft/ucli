using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationDescribeContractValidator
{
    private const int MaxArgsPathLength = 256;

    private const int MaxArgsPathSegmentCount = 16;

    /// <summary> Validates one public raw-operation describe contract without an external operation kind or policy. </summary>
    /// <param name="describeContract"> The describe contract to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="ownerName"> The non-empty diagnostic owner name used in validation messages. </param>
    /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
    /// <returns> <see langword="true" /> when the contract is valid for a public raw operation; otherwise <see langword="false" />. </returns>
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

    /// <summary> Validates one public raw-operation describe contract against an optional operation kind and policy. </summary>
    /// <param name="describeContract"> The describe contract to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="operationKind"> The optional operation kind that assurance metadata must match when specified. </param>
    /// <param name="operationPolicy"> The optional operation policy that assurance metadata must match when specified. </param>
    /// <param name="ownerName"> The non-empty diagnostic owner name used in validation messages. </param>
    /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
    /// <returns> <see langword="true" /> when the contract is valid for a public raw operation; otherwise <see langword="false" />. </returns>
    public static bool TryValidatePublicRawOpDescribeContract (
        UcliOperationDescribeContract? describeContract,
        string? operationKind,
        string? operationPolicy,
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

    /// <summary> Validates one public raw-operation describe contract and derives its operation policy from assurance metadata. </summary>
    /// <param name="describeContract"> The describe contract to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="operationKind"> The optional operation kind that assurance metadata must match when specified. </param>
    /// <param name="ownerName"> The non-empty diagnostic owner name used in validation messages. </param>
    /// <param name="derivedPolicy"> The policy derived from assurance metadata when validation succeeds; otherwise <see cref="OperationPolicy.Safe" />. </param>
    /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
    /// <returns> <see langword="true" /> when the contract is valid for a public raw operation; otherwise <see langword="false" />. </returns>
    public static bool TryValidatePublicRawOpDescribeContractAndDerivePolicy (
        UcliOperationDescribeContract? describeContract,
        string? operationKind,
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

    /// <summary> Validates one registered operation describe contract and derives its operation policy from assurance metadata. </summary>
    /// <param name="describeContract"> The describe contract to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="operationKind"> The optional operation kind that assurance metadata must match when specified. </param>
    /// <param name="ownerName"> The non-empty diagnostic owner name used in validation messages. </param>
    /// <param name="exposure"> The operation exposure that determines whether preview-state plan mode is allowed. </param>
    /// <param name="derivedPolicy"> The policy derived from assurance metadata when validation succeeds; otherwise <see cref="OperationPolicy.Safe" />. </param>
    /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
    /// <returns> <see langword="true" /> when the contract is valid for the registered exposure; otherwise <see langword="false" />. </returns>
    public static bool TryValidateRegisteredOperationDescribeContractAndDerivePolicy (
        UcliOperationDescribeContract? describeContract,
        string? operationKind,
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
            allowMayCreatePreviewState: CanUseEditLoweringOnlyPlanModes(exposure),
            out derivedPolicy,
            out errorMessage);
    }

    /// <summary> Validates one registered operation describe contract against its kind, policy, and exposure. </summary>
    /// <param name="describeContract"> The describe contract to validate. A <see langword="null" /> value is invalid. </param>
    /// <param name="operationKind"> The optional operation kind that assurance metadata must match when specified. </param>
    /// <param name="operationPolicy"> The optional operation policy that assurance metadata must match when specified. </param>
    /// <param name="ownerName"> The non-empty diagnostic owner name used in validation messages. </param>
    /// <param name="exposure"> The operation exposure that determines whether preview-state plan mode is allowed. </param>
    /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
    /// <returns> <see langword="true" /> when the contract is valid for the registered exposure; otherwise <see langword="false" />. </returns>
    public static bool TryValidateRegisteredOperationDescribeContract (
        UcliOperationDescribeContract? describeContract,
        string? operationKind,
        string? operationPolicy,
        string ownerName,
        UcliOperationExposure exposure,
        out string errorMessage)
    {
        return TryValidatePublicRawOpDescribeContractCore(
            describeContract,
            operationKind,
            operationPolicy,
            ownerName,
            allowMayCreatePreviewState: CanUseEditLoweringOnlyPlanModes(exposure),
            out _,
            out errorMessage);
    }

    private static bool CanUseEditLoweringOnlyPlanModes (UcliOperationExposure exposure)
    {
        return exposure == UcliOperationExposure.EditLoweringOnly;
    }

    private static bool TryValidatePublicRawOpDescribeContractCore (
        UcliOperationDescribeContract? describeContract,
        string? operationKind,
        string? operationPolicy,
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

        if (!TryValidatePublicRawOpInputs(describeContract.Inputs, ownerName, out errorMessage)
            || !TryValidateResultContract(describeContract.ResultContract, ownerName, out errorMessage)
            || !UcliOperationAssuranceContractValidator.TryValidate(
                describeContract.Assurance,
                operationKind,
                operationPolicy,
                describeContract.CodeContract,
                ownerName,
                allowMayCreatePreviewState,
                out derivedPolicy,
                out errorMessage)
            || !UcliOperationCodeContractValidator.TryValidate(describeContract.CodeContract, ownerName, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary> Validates public raw-operation input contracts. </summary>
    /// <param name="inputs"> The input contract collection. A <see langword="null" /> value is invalid; an empty collection is valid. </param>
    /// <param name="ownerName"> The non-empty diagnostic owner name used in validation messages. </param>
    /// <param name="errorMessage"> The validation error when the method returns <see langword="false" />; otherwise an empty string. </param>
    /// <returns> <see langword="true" /> when every input contract is valid and input names are unique; otherwise <see langword="false" />. </returns>
    public static bool TryValidatePublicRawOpInputs (
        IReadOnlyList<UcliOperationInputContract>? inputs,
        string ownerName,
        out string errorMessage)
    {
        if (inputs == null)
        {
            errorMessage = $"{ownerName} is missing inputs.";
            return false;
        }

        var inputNames = new HashSet<string>(StringComparer.Ordinal);
        for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
        {
            var input = inputs[inputIndex];
            if (!TryValidateInput(input, inputIndex, ownerName, inputNames, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateInput (
        UcliOperationInputContract? input,
        int inputIndex,
        string ownerName,
        HashSet<string> inputNames,
        out string errorMessage)
    {
        if (input == null
            || string.IsNullOrWhiteSpace(input.Name)
            || string.IsNullOrWhiteSpace(input.Description)
            || !IsSupportedInputValueType(input.ValueType)
            || !IsValidArgsPathSegment(input.Name)
            || UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(input.Name)
            || input.Constraints == null
            || !inputNames.Add(input.Name))
        {
            errorMessage = $"{ownerName} has an invalid input at index {inputIndex}.";
            return false;
        }

        if (!TryResolveInputArgsPath(input, ownerName, out var inputArgsPath, out errorMessage))
        {
            return false;
        }

        if (!TryValidateInputConstraints(input.Constraints, ownerName, out errorMessage)
            || !TryValidateInputVariants(input.Variants, ownerName, inputArgsPath, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryResolveInputArgsPath (
        UcliOperationInputContract input,
        string ownerName,
        out string inputArgsPath,
        out string errorMessage)
    {
        inputArgsPath = input.ArgsPath ?? ("$." + input.Name);
        if (!IsValidInputArgsPath(inputArgsPath))
        {
            errorMessage = $"{ownerName} input '{input.Name}' has an invalid argsPath.";
            return false;
        }

        if (UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasArgsPath(inputArgsPath))
        {
            errorMessage = $"{ownerName} input '{input.Name}' must not expose request-local alias args path '{inputArgsPath}'.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateInputVariants (
        IReadOnlyList<UcliOperationInputVariantContract>? variants,
        string ownerName,
        string inputArgsPath,
        out string errorMessage)
    {
        if (variants == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        var variantNames = new HashSet<string>(StringComparer.Ordinal);
        for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
        {
            var variant = variants[variantIndex];
            if (variant == null
                || string.IsNullOrWhiteSpace(variant.Name)
                || string.IsNullOrWhiteSpace(variant.Description)
                || variant.Fields == null
                || variant.Fields.Count == 0
                || !variantNames.Add(variant.Name))
            {
                errorMessage = $"{ownerName} has an invalid input variant at index {variantIndex}.";
                return false;
            }

            if (!TryValidateInputVariantFields(variant, ownerName, inputArgsPath, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateInputVariantFields (
        UcliOperationInputVariantContract variant,
        string ownerName,
        string inputArgsPath,
        out string errorMessage)
    {
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var fieldArgsPaths = new HashSet<string>(StringComparer.Ordinal);
        var fields = variant.Fields!;
        for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
        {
            var field = fields[fieldIndex];
            if (!TryValidateInputVariantField(field, fieldIndex, variant, ownerName, inputArgsPath, fieldNames, fieldArgsPaths, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateInputVariantField (
        UcliOperationInputVariantFieldContract? field,
        int fieldIndex,
        UcliOperationInputVariantContract variant,
        string ownerName,
        string inputArgsPath,
        HashSet<string> fieldNames,
        HashSet<string> fieldArgsPaths,
        out string errorMessage)
    {
        if (field == null)
        {
            errorMessage = $"{ownerName} variant '{variant.Name}' field at index {fieldIndex} must not be null.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(field.Name)
            || string.IsNullOrWhiteSpace(field.Description)
            || !IsValidArgsPathSegment(field.Name)
            || UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasPropertyName(field.Name)
            || !IsValidArgsPath(field.ArgsPath)
            || UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasArgsPath(field.ArgsPath)
            || !IsVariantFieldArgsPathWithinInput(field.ArgsPath!, inputArgsPath)
            || !IsArgsPathLeafSegment(field.ArgsPath!, field.Name)
            || field.Constraints == null
            || !fieldNames.Add(field.Name)
            || !fieldArgsPaths.Add(field.ArgsPath!))
        {
            errorMessage = $"{ownerName} has an invalid input variant field at index {fieldIndex}.";
            return false;
        }

        if (!TryValidateInputConstraints(field.Constraints, ownerName, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsVariantFieldArgsPathWithinInput (
        string variantFieldArgsPath,
        string inputArgsPath)
    {
        return string.Equals(inputArgsPath, "$", StringComparison.Ordinal)
            || string.Equals(variantFieldArgsPath, inputArgsPath, StringComparison.Ordinal)
            || variantFieldArgsPath.StartsWith(inputArgsPath + ".", StringComparison.Ordinal);
    }

    private static bool IsArgsPathLeafSegment (
        string argsPath,
        string fieldName)
    {
        var separatorIndex = argsPath.LastIndexOf('.');
        return separatorIndex >= 0
            && string.Equals(argsPath.Substring(separatorIndex + 1), fieldName, StringComparison.Ordinal);
    }

    private static bool TryValidateInputConstraints (
        IReadOnlyList<UcliOperationInputConstraintContract> constraints,
        string ownerName,
        out string errorMessage)
    {
        for (var constraintIndex = 0; constraintIndex < constraints.Count; constraintIndex++)
        {
            if (!UcliOperationInputConstraintValidator.TryValidate(constraints[constraintIndex], out var constraintError))
            {
                errorMessage = $"{ownerName} has an invalid input constraint at index {constraintIndex}. {constraintError}";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateResultContract (
        UcliOperationResultContract? resultContract,
        string ownerName,
        out string errorMessage)
    {
        if (resultContract == null
            || string.IsNullOrWhiteSpace(resultContract.ResultType)
            || string.IsNullOrWhiteSpace(resultContract.Description))
        {
            errorMessage = $"{ownerName} has an invalid resultContract.";
            return false;
        }

        if (resultContract.Emitted)
        {
            if (string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal))
            {
                errorMessage = $"{ownerName} has an inconsistent emitted resultContract.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        if (!string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal))
        {
            errorMessage = $"{ownerName} has an inconsistent no-result contract.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsSupportedInputValueType (string? valueType)
    {
        switch (valueType)
        {
            case "string":
            case "boolean":
            case "integer":
            case "number":
            case "object":
            case "array":
                return true;

            default:
                return false;
        }
    }

    private static bool IsValidArgsPath (string? argsPath)
    {
        if (string.IsNullOrWhiteSpace(argsPath)
            || argsPath.Length > MaxArgsPathLength
            || !argsPath.StartsWith("$.", StringComparison.Ordinal))
        {
            return false;
        }

        var segmentStart = 2;
        var segmentCount = 1;
        for (var i = 2; i <= argsPath.Length; i++)
        {
            if (i < argsPath.Length && argsPath[i] != '.')
            {
                continue;
            }

            if (!IsValidArgsPathSegment(argsPath, segmentStart, i - segmentStart))
            {
                return false;
            }

            if (i < argsPath.Length)
            {
                segmentCount++;
                if (segmentCount > MaxArgsPathSegmentCount)
                {
                    return false;
                }

                segmentStart = i + 1;
            }
        }

        return true;
    }

    private static bool IsValidArgsPathSegment (string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return IsValidArgsPathSegment(value, 0, value.Length);
    }

    private static bool IsValidArgsPathSegment (
        string argsPath,
        int start,
        int length)
    {
        if (length == 0)
        {
            return false;
        }

        for (var i = start; i < start + length; i++)
        {
            var c = argsPath[i];
            if (!IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit (char c)
    {
        return (c >= 'A' && c <= 'Z')
            || (c >= 'a' && c <= 'z')
            || (c >= '0' && c <= '9');
    }

    private static bool IsValidInputArgsPath (string argsPath)
    {
        return string.Equals(argsPath, "$", StringComparison.Ordinal)
            || IsValidArgsPath(argsPath);
    }

}
