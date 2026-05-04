using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Shared.Execution.ReadIndex;

/// <summary> Validates read-index catalog contracts loaded from persistent storage. </summary>
internal static class IndexCatalogContractValidator
{
    private const int SupportedSchemaVersion = 1;

    /// <summary> Validates one <c>ops.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidOpsCatalog (IndexOpsCatalogJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateOpsEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one operation-entry collection shared by persisted and live ops catalog payloads. </summary>
    /// <param name="entries"> The operation-entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateOpsEntries (
        IReadOnlyList<IndexOpEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!TryValidateOpsEntry(entry, i, out error))
            {
                return false;
            }

            if (!operationNames.Add(entry!.Name!))
            {
                error = $"Operation entry '{entry.Name}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateOpsEntry (
        IndexOpEntryJsonContract? entry,
        int index,
        out string? error)
    {
        error = null;
        if (entry == null
            || string.IsNullOrWhiteSpace(entry.Name)
            || !UcliOperationKindCodec.TryParse(entry.Kind, out _)
            || !OperationPolicyCodec.TryParse(entry.Policy, out _)
            || !IsValidSchemaObject(entry.ArgsSchemaJson)
            || !IsValidOptionalSchemaObject(entry.ResultSchemaJson)
            || !TryValidateOpsDescribeContract(entry, out error))
        {
            error ??= $"Operation entry at index {index} is invalid.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOpsDescribeContract (
        IndexOpEntryJsonContract entry,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            error = $"Operation entry '{entry.Name}' is missing description.";
            return false;
        }

        if (!TryValidateOperationInputs(entry.Inputs, entry.Name!, out error)
            || !TryValidateOperationResultContract(entry, out error)
            || !TryValidateOperationAssurance(entry.Assurance, entry.Name!, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOperationInputs (
        IReadOnlyList<UcliOperationInputContract>? inputs,
        string operationName,
        out string? error)
    {
        if (inputs == null)
        {
            error = $"Operation entry '{operationName}' is missing inputs.";
            return false;
        }

        var inputNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];
            if (input == null
                || string.IsNullOrWhiteSpace(input.Name)
                || string.IsNullOrWhiteSpace(input.Description)
                || !IsSupportedInputValueType(input.ValueType)
                || (input.ArgsPath != null && !IsValidInputArgsPath(input.ArgsPath))
                || input.Constraints == null
                || !inputNames.Add(input.Name))
            {
                error = $"Operation entry '{operationName}' has an invalid input at index {i}.";
                return false;
            }

            if (!TryValidateInputConstraints(input.Constraints, operationName, out error)
                || !TryValidateInputVariants(
                    input.Variants,
                    operationName,
                    input.ArgsPath ?? ("$." + input.Name),
                    out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateInputVariants (
        IReadOnlyList<UcliOperationInputVariantContract>? variants,
        string operationName,
        string inputArgsPath,
        out string? error)
    {
        if (variants == null)
        {
            error = null;
            return true;
        }

        var variantNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < variants.Count; i++)
        {
            var variant = variants[i];
            if (variant == null
                || string.IsNullOrWhiteSpace(variant.Name)
                || string.IsNullOrWhiteSpace(variant.Description)
                || variant.Fields == null
                || variant.Fields.Count == 0
                || !variantNames.Add(variant.Name))
            {
                error = $"Operation entry '{operationName}' has an invalid input variant at index {i}.";
                return false;
            }

            if (!TryValidateInputVariantFields(variant.Fields, operationName, inputArgsPath, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateInputVariantFields (
        IReadOnlyList<UcliOperationInputVariantFieldContract> fields,
        string operationName,
        string inputArgsPath,
        out string? error)
    {
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            if (field == null
                || string.IsNullOrWhiteSpace(field.Name)
                || string.IsNullOrWhiteSpace(field.Description)
                || !IsValidArgsPath(field.ArgsPath)
                || !IsVariantArgsPathWithinInput(field.ArgsPath!, inputArgsPath)
                || field.Constraints == null
                || !fieldNames.Add(field.Name))
            {
                error = $"Operation entry '{operationName}' has an invalid input variant field at index {i}.";
                return false;
            }

            if (!TryValidateInputConstraints(field.Constraints, operationName, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsVariantArgsPathWithinInput (
        string variantArgsPath,
        string inputArgsPath)
    {
        return string.Equals(inputArgsPath, "$", StringComparison.Ordinal)
            || string.Equals(variantArgsPath, inputArgsPath, StringComparison.Ordinal)
            || variantArgsPath.StartsWith(inputArgsPath + ".", StringComparison.Ordinal);
    }

    private static bool TryValidateInputConstraints (
        IReadOnlyList<UcliOperationInputConstraintContract> constraints,
        string operationName,
        out string? error)
    {
        for (var i = 0; i < constraints.Count; i++)
        {
            if (!TryValidateInputConstraint(constraints[i], out error))
            {
                error = $"Operation entry '{operationName}' has an invalid input constraint at index {i}. {error}";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateInputConstraint (
        UcliOperationInputConstraintContract? constraint,
        out string? error)
    {
        if (constraint == null
            || string.IsNullOrWhiteSpace(constraint.Kind))
        {
            error = "Constraint kind is missing.";
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
                return TryValidateNoConstraintParameters(constraint, out error);

            case UcliOperationInputConstraintKindValues.Range:
                return TryValidateRangeConstraint(constraint, out error);

            case UcliOperationInputConstraintKindValues.AssetExists:
            case UcliOperationInputConstraintKindValues.AssetCreatable:
                return TryValidateAssetKindConstraint(constraint, out error);

            case UcliOperationInputConstraintKindValues.ReferenceResolvable:
                return TryValidateReferenceTargetKindConstraint(constraint, out error);

            case UcliOperationInputConstraintKindValues.TypeAssignableTo:
                return TryValidateTypeKindConstraint(constraint, out error);

            case UcliOperationInputConstraintKindValues.SerializedProperty:
                return TryValidateSerializedPropertyConstraint(constraint, out error);

            default:
                error = $"Unsupported constraint kind '{constraint.Kind}'.";
                return false;
        }
    }

    private static bool TryValidateNoConstraintParameters (
        UcliOperationInputConstraintContract constraint,
        out string? error)
    {
        if (constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            error = "Constraint has unsupported parameters.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateRangeConstraint (
        UcliOperationInputConstraintContract constraint,
        out string? error)
    {
        if (constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || (constraint.Min == null && constraint.Max == null))
        {
            error = "Range constraint must only define min, max, or both.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateAssetKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string? error)
    {
        if (!IsSupportedAssetKind(constraint.AssetKind)
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            error = "Asset constraint must define one supported asset kind.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateReferenceTargetKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string? error)
    {
        if (!IsSupportedReferenceTargetKind(constraint.TargetKind)
            || constraint.AssetKind != null
            || constraint.TypeKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            error = "Reference constraint must define one supported target kind.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateTypeKindConstraint (
        UcliOperationInputConstraintContract constraint,
        out string? error)
    {
        if (!IsSupportedTypeKind(constraint.TypeKind)
            || constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.Access != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            error = "Type assignability constraint must define one supported type kind.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateSerializedPropertyConstraint (
        UcliOperationInputConstraintContract constraint,
        out string? error)
    {
        if (!IsSupportedSerializedPropertyAccess(constraint.Access)
            || constraint.AssetKind != null
            || constraint.TargetKind != null
            || constraint.TypeKind != null
            || constraint.Min != null
            || constraint.Max != null)
        {
            error = "Serialized-property constraint must define one supported access value.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOperationResultContract (
        IndexOpEntryJsonContract entry,
        out string? error)
    {
        var resultContract = entry.ResultContract;
        if (resultContract == null
            || string.IsNullOrWhiteSpace(resultContract.ResultType)
            || string.IsNullOrWhiteSpace(resultContract.Description))
        {
            error = $"Operation entry '{entry.Name}' has an invalid resultContract.";
            return false;
        }

        if (resultContract.Emitted)
        {
            if (entry.ResultSchemaJson == null
                || string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal))
            {
                error = $"Operation entry '{entry.Name}' has an inconsistent emitted result contract.";
                return false;
            }

            error = null;
            return true;
        }

        if (entry.ResultSchemaJson != null
            || !string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal))
        {
            error = $"Operation entry '{entry.Name}' has an inconsistent no-result contract.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateOperationAssurance (
        UcliOperationAssuranceContract? assurance,
        string operationName,
        out string? error)
    {
        if (assurance == null
            || assurance.SideEffects == null
            || assurance.TouchedKinds == null
            || !IsSupportedPlanMode(assurance.PlanMode))
        {
            error = $"Operation entry '{operationName}' has invalid assurance metadata.";
            return false;
        }

        for (var i = 0; i < assurance.SideEffects.Count; i++)
        {
            if (!IsSupportedSideEffect(assurance.SideEffects[i]))
            {
                error = $"Operation entry '{operationName}' has an unsupported side effect.";
                return false;
            }
        }

        for (var i = 0; i < assurance.TouchedKinds.Count; i++)
        {
            if (!IsSupportedTouchedKind(assurance.TouchedKinds[i]))
            {
                error = $"Operation entry '{operationName}' has an unsupported touched kind.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>types.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidTypesCatalog (IndexTypesCatalogJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            || contract.Entries == null)
        {
            return false;
        }

        var typeIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < contract.Entries.Count; i++)
        {
            var entry = contract.Entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || string.IsNullOrWhiteSpace(entry.DisplayName)
                || string.IsNullOrWhiteSpace(entry.AssemblyName)
                || entry.Flags == null)
            {
                return false;
            }

            if (!typeIds.Add(entry.TypeId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary> Validates one <c>schemas.catalog.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidSchemasCatalog (IndexSchemasCatalogJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash)
            || contract.Entries == null)
        {
            return false;
        }

        var schemaKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < contract.Entries.Count; i++)
        {
            var entry = contract.Entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.SchemaKey)
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || string.IsNullOrWhiteSpace(entry.DisplayName)
                || !IndexSchemaKindCodec.TryParse(entry.Kind, out var schemaKind)
                || entry.Properties == null)
            {
                return false;
            }

            var expectedSchemaKey = $"{IndexSchemaKindCodec.ToValue(schemaKind)}:{entry.TypeId}";
            if (!string.Equals(entry.SchemaKey, expectedSchemaKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (!schemaKeys.Add(entry.SchemaKey))
            {
                return false;
            }

            for (var propertyIndex = 0; propertyIndex < entry.Properties.Count; propertyIndex++)
            {
                var property = entry.Properties[propertyIndex];
                if (property == null
                    || string.IsNullOrWhiteSpace(property.Path)
                    || string.IsNullOrWhiteSpace(property.DeclaredTypeId)
                    || !IndexPropertyTypeCodec.TryParse(property.PropertyType, out _))
                {
                    return false;
                }

                if (property.IsArray)
                {
                    if (string.IsNullOrWhiteSpace(property.ElementTypeId))
                    {
                        return false;
                    }
                }
                else if (!string.IsNullOrEmpty(property.ElementTypeId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary> Validates one <c>asset-search.lookup.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidAssetSearchLookup (IndexAssetSearchLookupJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateAssetSearchEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one asset-search lookup entry collection shared by persisted and live payloads. </summary>
    /// <param name="entries"> The entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateAssetSearchEntries (
        IReadOnlyList<IndexAssetSearchEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var assetPaths = new HashSet<string>(StringComparer.Ordinal);
        var assetGuids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.AssetPath)
                || entry.AssetGuid == null
                || (entry.AssetGuid.Length > 0 && string.IsNullOrWhiteSpace(entry.AssetGuid))
                || string.IsNullOrWhiteSpace(entry.Name)
                || string.IsNullOrWhiteSpace(entry.TypeId)
                || entry.SearchTypeIds == null
                || entry.SearchTypeIds.Count == 0)
            {
                error = $"Asset-search entry at index {i} is invalid.";
                return false;
            }

            for (var searchTypeIndex = 0; searchTypeIndex < entry.SearchTypeIds.Count; searchTypeIndex++)
            {
                if (string.IsNullOrWhiteSpace(entry.SearchTypeIds[searchTypeIndex]))
                {
                    error = $"Asset-search entry at index {i} contains an invalid searchTypeIds value.";
                    return false;
                }
            }

            if (!assetPaths.Add(entry.AssetPath))
            {
                error = $"Asset-search entry '{entry.AssetPath}' is duplicated.";
                return false;
            }

            if (entry.AssetGuid.Length > 0
                && !assetGuids.Add(entry.AssetGuid))
            {
                error = $"Asset-search assetGuid '{entry.AssetGuid}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>guid-path.lookup.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidGuidPathLookup (IndexGuidPathLookupJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateGuidPathEntries(contract.Entries, "entries", out _);
    }

    /// <summary> Validates one GUID-path lookup entry collection shared by persisted and live payloads. </summary>
    /// <param name="entries"> The entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateGuidPathEntries (
        IReadOnlyList<IndexGuidPathEntryJsonContract>? entries,
        string propertyName,
        out string? error)
    {
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var assetPaths = new HashSet<string>(StringComparer.Ordinal);
        var assetGuids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.AssetGuid)
                || string.IsNullOrWhiteSpace(entry.AssetPath))
            {
                error = $"Guid-path entry at index {i} is invalid.";
                return false;
            }

            if (!assetGuids.Add(entry.AssetGuid))
            {
                error = $"Guid-path assetGuid '{entry.AssetGuid}' is duplicated.";
                return false;
            }

            if (!assetPaths.Add(entry.AssetPath))
            {
                error = $"Guid-path entry '{entry.AssetPath}' is duplicated.";
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>scene-tree-lite/&lt;sceneKey&gt;.lookup.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidSceneTreeLiteLookup (IndexSceneTreeLiteLookupJsonContract contract)
    {
        if (!IsSupportedSchemaVersion(contract.SchemaVersion)
            || string.IsNullOrWhiteSpace(contract.ScenePath)
            || string.IsNullOrWhiteSpace(contract.SourceInputsHash))
        {
            return false;
        }

        return TryValidateSceneTreeLiteNodes(contract.Roots, "roots", out _);
    }

    /// <summary> Validates one scene-tree-lite node collection shared by persisted and live payloads. </summary>
    /// <param name="nodes"> The node collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the node collection is valid; otherwise <see langword="false" />. </returns>
    public static bool TryValidateSceneTreeLiteNodes (
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? nodes,
        string propertyName,
        out string? error)
    {
        if (nodes == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (!TryValidateSceneTreeLiteNode(nodes[i], $"{propertyName}[{i}]", out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    /// <summary> Validates one <c>inputs/manifest.json</c> contract instance. </summary>
    /// <param name="contract"> The contract instance. </param>
    /// <returns> <see langword="true" /> when contract shape is valid; otherwise <see langword="false" />. </returns>
    public static bool IsValidInputsManifest (IndexInputsManifestJsonContract contract)
    {
        return IsSupportedSchemaVersion(contract.SchemaVersion)
            && !string.IsNullOrWhiteSpace(contract.ScriptAssembliesHash)
            && !string.IsNullOrWhiteSpace(contract.PackagesManifestHash)
            && !string.IsNullOrWhiteSpace(contract.PackagesLockHash)
            && !string.IsNullOrWhiteSpace(contract.AssemblyDefinitionHash)
            && !string.IsNullOrWhiteSpace(contract.AssetsContentHash)
            && !string.IsNullOrWhiteSpace(contract.AssetSearchHash)
            && !string.IsNullOrWhiteSpace(contract.GuidPathHash)
            && !string.IsNullOrWhiteSpace(contract.CombinedHash);
    }

    private static bool IsSupportedSchemaVersion (int schemaVersion)
    {
        return schemaVersion == SupportedSchemaVersion;
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
            || !argsPath.StartsWith("$.", StringComparison.Ordinal))
        {
            return false;
        }

        var parts = argsPath.Substring(2).Split('.');
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i]))
            {
                return false;
            }
        }

        return true;
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

    private static bool IsSupportedSideEffect (string? sideEffect)
    {
        switch (sideEffect)
        {
            case UcliOperationSideEffectValues.OpensSceneInEditor:
            case UcliOperationSideEffectValues.OpensPrefabStage:
            case UcliOperationSideEffectValues.RefreshesAssetDatabase:
            case UcliOperationSideEffectValues.WritesAsset:
            case UcliOperationSideEffectValues.WritesScene:
            case UcliOperationSideEffectValues.WritesPrefab:
            case UcliOperationSideEffectValues.WritesProjectSettings:
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

    private static bool TryValidateSceneTreeLiteNode (
        IndexSceneTreeLiteNodeJsonContract? node,
        string propertyName,
        out string? error)
    {
        if (node == null
            || node.Name == null
            || node.GlobalObjectId == null
            || (node.GlobalObjectId.Length > 0 && string.IsNullOrWhiteSpace(node.GlobalObjectId))
            || node.Children == null)
        {
            error = $"Scene-tree-lite node '{propertyName}' is invalid.";
            return false;
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            if (!TryValidateSceneTreeLiteNode(node.Children[i], $"{propertyName}.children[{i}]", out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsValidSchemaObject (string? json)
    {
        return IndexJsonSchemaSubsetValidator.IsValidObjectSchema(json);
    }

    private static bool IsValidOptionalSchemaObject (string? json)
    {
        return json == null || IsValidSchemaObject(json);
    }
}
