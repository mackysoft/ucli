using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationDescribeContractValidator
{
    private const string SupportedCodeLanguageCSharp = "csharp";

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
            || !TryValidateAssurance(
                describeContract.Assurance,
                operationKind,
                operationPolicy,
                describeContract.CodeContract,
                ownerName,
                allowMayCreatePreviewState,
                out derivedPolicy,
                out errorMessage)
            || !TryValidateCodeContract(describeContract.CodeContract, ownerName, out errorMessage))
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
            if (!TryValidateInputConstraint(constraints[constraintIndex], out var constraintError))
            {
                errorMessage = $"{ownerName} has an invalid input constraint at index {constraintIndex}. {constraintError}";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateInputConstraint (
        UcliOperationInputConstraintContract? constraint,
        out string errorMessage)
    {
        if (constraint == null
            || string.IsNullOrWhiteSpace(constraint.Kind))
        {
            errorMessage = "Constraint kind is missing.";
            return false;
        }

        switch (constraint.Kind)
        {
            case UcliOperationInputConstraintKindValues.NonEmpty:
            case UcliOperationInputConstraintKindValues.ProjectRelativePath:
            case UcliOperationInputConstraintKindValues.GlobalObjectId:
            case UcliOperationInputConstraintKindValues.HierarchyPath:
            case UcliOperationInputConstraintKindValues.TypeExists:
            case UcliOperationInputConstraintKindValues.AssetGuid:
            case UcliOperationInputConstraintKindValues.Cursor:
                return TryValidateNoConstraintParameters(constraint, out errorMessage);

            case UcliOperationInputConstraintKindValues.Range:
                return TryValidateRangeConstraint(constraint, out errorMessage);

            case UcliOperationInputConstraintKindValues.AssetExists:
            case UcliOperationInputConstraintKindValues.AssetCreatable:
                return TryValidateAssetKindConstraint(constraint, out errorMessage);

            case UcliOperationInputConstraintKindValues.ReferenceResolvable:
                return TryValidateReferenceTargetKindConstraint(constraint, out errorMessage);

            case UcliOperationInputConstraintKindValues.TypeAssignableTo:
                return TryValidateTypeKindConstraint(constraint, out errorMessage);

            case UcliOperationInputConstraintKindValues.SerializedProperty:
                return TryValidateSerializedPropertyConstraint(constraint, out errorMessage);

            default:
                errorMessage = $"Unsupported constraint kind '{constraint.Kind}'.";
                return false;
        }
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

    private static bool TryValidateAssurance (
        UcliOperationAssuranceContract? assurance,
        string? operationKind,
        string? operationPolicy,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        bool allowMayCreatePreviewState,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        derivedPolicy = OperationPolicy.Safe;

        if (assurance == null
            || assurance.SideEffects == null
            || assurance.TouchedKinds == null
            || assurance.DangerousNotes == null
            || !IsSupportedPlanMode(assurance.PlanMode)
            || string.IsNullOrWhiteSpace(assurance.PlanSemantics)
            || string.IsNullOrWhiteSpace(assurance.CallSemantics)
            || string.IsNullOrWhiteSpace(assurance.TouchedContract)
            || string.IsNullOrWhiteSpace(assurance.ReadPostconditionContract)
            || string.IsNullOrWhiteSpace(assurance.FailureSemantics))
        {
            errorMessage = $"{ownerName} has invalid assurance metadata.";
            return false;
        }

        if (!allowMayCreatePreviewState
            && string.Equals(assurance.PlanMode, UcliOperationPlanModeValues.MayCreatePreviewState, StringComparison.Ordinal))
        {
            errorMessage = $"{ownerName} public raw assurance metadata must not use planMode '{UcliOperationPlanModeValues.MayCreatePreviewState}'.";
            return false;
        }

        for (var i = 0; i < assurance.SideEffects.Count; i++)
        {
            var sideEffect = assurance.SideEffects[i];
            if (!UcliOperationSideEffectDescriptors.TryGetMinimumPolicy(sideEffect, out _))
            {
                errorMessage = $"{ownerName} has an unsupported side effect '{sideEffect}'.";
                return false;
            }
        }

        for (var i = 0; i < assurance.TouchedKinds.Count; i++)
        {
            var touchedKind = assurance.TouchedKinds[i];
            if (!IsSupportedTouchedKind(touchedKind))
            {
                errorMessage = $"{ownerName} has an unsupported touched kind '{touchedKind}'.";
                return false;
            }
        }

        for (var i = 0; i < assurance.DangerousNotes.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(assurance.DangerousNotes[i]))
            {
                errorMessage = $"{ownerName} has an invalid dangerous note.";
                return false;
            }
        }

        if (!TryValidateAssuranceConsistency(assurance, operationKind, operationPolicy, codeContract, ownerName, out derivedPolicy, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateAssuranceConsistency (
        UcliOperationAssuranceContract assurance,
        string? operationKind,
        string? operationPolicy,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out OperationPolicy derivedPolicy,
        out string errorMessage)
    {
        derivedPolicy = OperationPolicy.Safe;

        if (operationKind != null)
        {
            if (!UcliOperationKindCodec.TryParse(operationKind, out var parsedKind))
            {
                errorMessage = $"{ownerName} has unsupported operation kind metadata.";
                return false;
            }

            if (parsedKind == UcliOperationKind.Query
                && (codeContract != null || assurance.MayDirty || assurance.MayPersist || assurance.TouchedKinds!.Count != 0 || !HasOnlyQuerySideEffects(assurance.SideEffects!)))
            {
                errorMessage = $"{ownerName} has query assurance metadata with mutation or side-effect risk.";
                return false;
            }
        }

        if (!TryValidateAssuranceProjectionAndConstraints(assurance, ownerName, out errorMessage))
        {
            return false;
        }

        if (!TryValidateCodeContractPolicyFact(assurance, codeContract, ownerName, out errorMessage))
        {
            return false;
        }

        if (!UcliOperationPolicyDeriver.TryDerive(assurance, codeContract, out derivedPolicy))
        {
            errorMessage = $"{ownerName} has invalid policy derivation metadata.";
            return false;
        }

        if (operationPolicy != null)
        {
            if (!OperationPolicyCodec.TryParse(operationPolicy, out var parsedPolicy))
            {
                errorMessage = $"{ownerName} has unsupported operation policy metadata.";
                return false;
            }

            if (parsedPolicy != derivedPolicy)
            {
                errorMessage = $"{ownerName} policy '{operationPolicy}' does not match derived policy '{OperationPolicyCodec.ToValue(derivedPolicy)}'.";
                return false;
            }
        }

        if (derivedPolicy != OperationPolicy.Safe && assurance.DangerousNotes!.Count == 0)
        {
            errorMessage = $"{ownerName} must declare dangerousNotes for advanced or dangerous policy.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateCodeContractPolicyFact (
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (codeContract == null || ContainsSideEffect(assurance.SideEffects!, UcliOperationSideEffectValues.ArbitrarySourceExecution))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"{ownerName} codeContract requires assurance.sideEffects to include '{UcliOperationSideEffectValues.ArbitrarySourceExecution}'.";
        return false;
    }

    private static bool HasOnlyQuerySideEffects (IReadOnlyList<string> sideEffects)
    {
        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (!UcliOperationSideEffectDescriptors.IsAllowedForQueryOperation(sideEffects[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateAssuranceProjectionAndConstraints (
        UcliOperationAssuranceContract assurance,
        string ownerName,
        out string errorMessage)
    {
        if (!UcliOperationSideEffectDescriptors.TryDeriveAssuranceProjection(assurance.SideEffects, out var derivedMayDirty, out var derivedMayPersist))
        {
            errorMessage = $"{ownerName} has invalid side-effect projection metadata.";
            return false;
        }

        if (assurance.MayDirty != derivedMayDirty)
        {
            errorMessage = $"{ownerName} assurance.mayDirty does not match derived projection '{FormatBoolean(derivedMayDirty)}'.";
            return false;
        }

        if (assurance.MayPersist != derivedMayPersist)
        {
            errorMessage = $"{ownerName} assurance.mayPersist does not match derived projection '{FormatBoolean(derivedMayPersist)}'.";
            return false;
        }

        if (derivedMayPersist && assurance.TouchedKinds!.Count == 0)
        {
            errorMessage = $"{ownerName} assurance.mayPersist requires assurance.touchedKinds to be non-empty.";
            return false;
        }

        for (var i = 0; i < assurance.SideEffects!.Count; i++)
        {
            var sideEffect = assurance.SideEffects[i];
            if (!UcliOperationSideEffectDescriptors.TryGetDescriptor(sideEffect, out var descriptor))
            {
                errorMessage = $"{ownerName} has an unsupported side effect '{sideEffect}'.";
                return false;
            }

            for (var j = 0; j < descriptor.RequiredTouchedKinds.Count; j++)
            {
                var requiredTouchedKind = descriptor.RequiredTouchedKinds[j];
                if (!ContainsTouchedKind(assurance.TouchedKinds!, requiredTouchedKind))
                {
                    errorMessage = $"{ownerName} side effect '{sideEffect}' requires assurance.touchedKinds to include '{requiredTouchedKind}'.";
                    return false;
                }
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static string FormatBoolean (bool value)
    {
        return value ? "true" : "false";
    }

    private static bool ContainsTouchedKind (
        IReadOnlyList<string> touchedKinds,
        string requiredTouchedKind)
    {
        for (var i = 0; i < touchedKinds.Count; i++)
        {
            if (string.Equals(touchedKinds[i], requiredTouchedKind, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsSideEffect (
        IReadOnlyList<string> sideEffects,
        string expectedSideEffect)
    {
        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (string.Equals(sideEffects[i], expectedSideEffect, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryValidateCodeContract (
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (codeContract == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(codeContract.Language)
            || codeContract.EntryPoint == null
            || string.IsNullOrWhiteSpace(codeContract.EntryPoint.Signature)
            || string.IsNullOrWhiteSpace(codeContract.EntryPoint.MatchRule)
            || codeContract.EntryPoint.ParameterTypes == null
            || string.IsNullOrWhiteSpace(codeContract.EntryPoint.ReturnValue)
            || codeContract.SourceForms == null
            || codeContract.SourceForms.Count == 0
            || codeContract.ApiTypes == null)
        {
            errorMessage = $"{ownerName} has invalid codeContract metadata.";
            return false;
        }

        if (!IsSupportedCodeLanguage(codeContract.Language))
        {
            errorMessage = $"{ownerName} has an unsupported codeContract language.";
            return false;
        }

        for (var parameterTypeIndex = 0; parameterTypeIndex < codeContract.EntryPoint.ParameterTypes.Count; parameterTypeIndex++)
        {
            if (string.IsNullOrWhiteSpace(codeContract.EntryPoint.ParameterTypes[parameterTypeIndex]))
            {
                errorMessage = $"{ownerName} has an invalid codeContract entry point parameter type.";
                return false;
            }
        }

        for (var sourceFormIndex = 0; sourceFormIndex < codeContract.SourceForms.Count; sourceFormIndex++)
        {
            var sourceForm = codeContract.SourceForms[sourceFormIndex];
            if (sourceForm == null
                || string.IsNullOrWhiteSpace(sourceForm.Kind)
                || string.IsNullOrWhiteSpace(sourceForm.Description))
            {
                errorMessage = $"{ownerName} has an invalid codeContract source form at index {sourceFormIndex}.";
                return false;
            }

            if (!IsSupportedCodeSourceFormKind(sourceForm.Kind))
            {
                errorMessage = $"{ownerName} has an unsupported codeContract source form at index {sourceFormIndex}.";
                return false;
            }
        }

        for (var typeIndex = 0; typeIndex < codeContract.ApiTypes.Count; typeIndex++)
        {
            if (!TryValidateCodeApiType(codeContract.ApiTypes[typeIndex], typeIndex, ownerName, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool IsSupportedCodeLanguage (string language)
    {
        return string.Equals(language, SupportedCodeLanguageCSharp, StringComparison.Ordinal);
    }

    private static bool IsSupportedCodeSourceFormKind (string kind)
    {
        return kind switch
        {
            CsEvalSourceKindValues.CompilationUnit => true,
            CsEvalSourceKindValues.Snippet => true,
            _ => false,
        };
    }

    private static bool TryValidateCodeApiType (
        UcliCodeApiTypeContract? apiType,
        int typeIndex,
        string ownerName,
        out string errorMessage)
    {
        if (apiType == null
            || string.IsNullOrWhiteSpace(apiType.Name)
            || string.IsNullOrWhiteSpace(apiType.FullName)
            || string.IsNullOrWhiteSpace(apiType.Description)
            || apiType.Members == null)
        {
            errorMessage = $"{ownerName} has an invalid codeContract api type at index {typeIndex}.";
            return false;
        }

        var memberNames = new HashSet<string>(StringComparer.Ordinal);
        for (var memberIndex = 0; memberIndex < apiType.Members.Count; memberIndex++)
        {
            if (!TryValidateCodeApiMember(apiType.Members[memberIndex], memberIndex, ownerName, memberNames, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateCodeApiMember (
        UcliCodeApiMemberContract? member,
        int memberIndex,
        string ownerName,
        HashSet<string> memberNames,
        out string errorMessage)
    {
        if (member == null
            || string.IsNullOrWhiteSpace(member.Kind)
            || string.IsNullOrWhiteSpace(member.Name)
            || string.IsNullOrWhiteSpace(member.Description)
            || !memberNames.Add(member.Name + ":" + member.Kind))
        {
            errorMessage = $"{ownerName} has an invalid codeContract api member at index {memberIndex}.";
            return false;
        }

        if (member.Kind == UcliCodeApiMemberKindValues.Property)
        {
            if (string.IsNullOrWhiteSpace(member.Type)
                || member.ReturnType != null
                || member.Parameters == null
                || member.Parameters.Count != 0)
            {
                errorMessage = $"{ownerName} has an invalid codeContract property member at index {memberIndex}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        if (member.Kind != UcliCodeApiMemberKindValues.Method
            || member.Type != null
            || string.IsNullOrWhiteSpace(member.ReturnType)
            || member.Parameters == null)
        {
            errorMessage = $"{ownerName} has an invalid codeContract method member at index {memberIndex}.";
            return false;
        }

        for (var parameterIndex = 0; parameterIndex < member.Parameters.Count; parameterIndex++)
        {
            var parameter = member.Parameters[parameterIndex];
            if (parameter == null
                || string.IsNullOrWhiteSpace(parameter.Name)
                || string.IsNullOrWhiteSpace(parameter.Type)
                || string.IsNullOrWhiteSpace(parameter.Description))
            {
                errorMessage = $"{ownerName} has an invalid codeContract method parameter at index {parameterIndex}.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateNoConstraintParameters (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Constraint has unsupported parameters.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRangeConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || (constraint.Min == null && constraint.Max == null))
        {
            errorMessage = "Range constraint must only define min, max, or both.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateAssetKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!IsSupportedAssetKind(constraint.AssetKind)
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Asset constraint must define one supported asset kind.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateReferenceTargetKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!IsSupportedReferenceTargetKind(constraint.TargetKind)
            || constraint.AssetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Reference constraint must define one supported target kind.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateTypeKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!IsSupportedTypeKind(constraint.TypeKind)
            || constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Type assignability constraint must define one supported type kind.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateSerializedPropertyConstraint (
        UcliOperationInputConstraintContract constraint,
        out string errorMessage)
    {
        if (!IsSupportedSerializedPropertyAccess(constraint.Access)
            || constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            errorMessage = "Serialized-property constraint must define one supported access value.";
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

    private static bool IsSupportedAssetKind (string? assetKind)
    {
        switch (assetKind)
        {
            case UcliOperationAssetKindValues.Asset:
            case UcliOperationAssetKindValues.Prefab:
            case UcliOperationAssetKindValues.ProjectSettings:
            case UcliOperationAssetKindValues.Scene:
                return true;

            default:
                return false;
        }
    }

    private static bool IsSupportedReferenceTargetKind (string? targetKind)
    {
        switch (targetKind)
        {
            case UcliOperationReferenceTargetKindValues.Asset:
            case UcliOperationReferenceTargetKindValues.Component:
            case UcliOperationReferenceTargetKindValues.GameObject:
                return true;

            default:
                return false;
        }
    }

    private static bool IsSupportedTypeKind (string? typeKind)
    {
        return string.Equals(typeKind, UcliOperationTypeKindValues.Component, StringComparison.Ordinal);
    }

    private static bool IsSupportedSerializedPropertyAccess (string? access)
    {
        return string.Equals(access, UcliOperationSerializedPropertyAccessValues.Write, StringComparison.Ordinal);
    }

    private static bool IsSupportedPlanMode (string? planMode)
    {
        switch (planMode)
        {
            case UcliOperationPlanModeValues.ValidationOnly:
            case UcliOperationPlanModeValues.ObservesLiveUnity:
            case UcliOperationPlanModeValues.MayCreatePreviewState:
                return true;

            default:
                return false;
        }
    }

    private static bool IsSupportedTouchedKind (string? touchedKind)
    {
        switch (touchedKind)
        {
            case IpcExecuteTouchedResourceKindNames.Scene:
            case IpcExecuteTouchedResourceKindNames.Prefab:
            case IpcExecuteTouchedResourceKindNames.Asset:
            case IpcExecuteTouchedResourceKindNames.ProjectSettings:
                return true;

            default:
                return false;
        }
    }
}
