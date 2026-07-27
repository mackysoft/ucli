using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Validates operation catalog entries loaded from persistent or live read-index sources. </summary>
internal static class IndexOperationCatalogContractValidator
{
    /// <summary> Projects one operation-entry collection shared by persisted and live ops catalog payloads. </summary>
    /// <param name="entries"> The operation-entry collection. </param>
    /// <param name="propertyName"> The property name used in validation errors. </param>
    /// <param name="allowEditLoweringOnlyEntries"> Whether edit-lowering-only entries are valid for request validation. </param>
    /// <param name="operations"> The validated typed operations on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the entry collection is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectOpsEntries (
        IReadOnlyList<IndexOpEntryJsonContract>? entries,
        string propertyName,
        bool allowEditLoweringOnlyEntries,
        [NotNullWhen(true)]
        out IReadOnlyList<ValidatedOpsOperation>? operations,
        out string? error)
    {
        operations = null;
        if (entries == null)
        {
            error = $"Required property '{propertyName}' is missing.";
            return false;
        }

        var operationNames = new HashSet<string>(StringComparer.Ordinal);
        var projectedOperations = new ValidatedOpsOperation[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!TryProjectOpsEntry(
                    entry,
                    i,
                    allowEditLoweringOnlyEntries,
                    out var operation,
                    out error))
            {
                return false;
            }

            if (!operationNames.Add(operation.Name))
            {
                error = $"Operation entry '{entry.Name}' is duplicated.";
                return false;
            }

            projectedOperations[i] = operation;
        }

        operations = Array.AsReadOnly(projectedOperations);
        error = null;
        return true;
    }

    /// <summary> Projects one operation entry shared by persisted detail and live catalog payloads. </summary>
    /// <param name="entry"> The operation entry. </param>
    /// <param name="index"> The entry index used in validation errors. </param>
    /// <param name="allowEditLoweringOnlyEntries"> Whether edit-lowering-only entries are valid for request validation. </param>
    /// <param name="operation"> The validated typed operation on success; otherwise <see langword="null" />. </param>
    /// <param name="error"> The validation error; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when the operation entry is valid; otherwise <see langword="false" />. </returns>
    internal static bool TryProjectOpsEntry (
        IndexOpEntryJsonContract? entry,
        int index,
        bool allowEditLoweringOnlyEntries,
        [NotNullWhen(true)]
        out ValidatedOpsOperation? operation,
        out string? error)
    {
        operation = null;
        error = null;
        if (entry == null
            || string.IsNullOrWhiteSpace(entry.Name)
            || !entry.Kind.HasValue
            || !TextVocabulary.IsDefined(entry.Kind.Value)
            || !entry.Policy.HasValue
            || !TextVocabulary.IsDefined(entry.Policy.Value)
            || !entry.PlayModeSupport.HasValue
            || !TextVocabulary.IsDefined(entry.PlayModeSupport.Value)
            || !TryResolveCatalogExposure(
                entry.Exposure,
                allowEditLoweringOnlyEntries,
                out var exposure,
                out error)
            || !TryValidateOpsDescribeContract(entry, exposure, out error))
        {
            error ??= $"Operation entry at index {index} is invalid.";
            return false;
        }

        operation = new ValidatedOpsOperation(
            entry,
            exposure);
        error = null;
        return true;
    }

    private static bool TryResolveCatalogExposure (
        UcliOperationExposure? exposureValue,
        bool allowEditLoweringOnlyEntries,
        out UcliOperationExposure exposure,
        out string? error)
    {
        if (!exposureValue.HasValue)
        {
            exposure = UcliOperationExposure.Public;
            error = null;
            return true;
        }

        exposure = exposureValue.Value;
        if (!TextVocabulary.IsDefined(exposure))
        {
            error = $"Unsupported operation exposure '{exposureValue}'.";
            return false;
        }

        if (exposure == UcliOperationExposure.Public)
        {
            error = null;
            return true;
        }

        if (exposure == UcliOperationExposure.EditLoweringOnly && allowEditLoweringOnlyEntries)
        {
            error = null;
            return true;
        }

        error = $"Operation exposure '{exposureValue}' is not allowed in this catalog.";
        return false;
    }

    private static bool TryValidateOpsDescribeContract (
        IndexOpEntryJsonContract entry,
        UcliOperationExposure exposure,
        out string? error)
    {
        var describeContract = new UcliOperationDescribeContract(
            entry.Description,
            entry.ArgsContract,
            entry.ResultContract,
            entry.Assurance,
            entry.CodeContract);
        var ownerName = $"Operation entry '{entry.Name}'";
        string inputError;
        bool describeContractValid;
        if (exposure == UcliOperationExposure.Public)
        {
            describeContractValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
                describeContract,
                entry.Kind,
                entry.Policy,
                ownerName,
                out inputError);
        }
        else
        {
            describeContractValid = UcliOperationDescribeContractValidator.TryValidateRegisteredOperationDescribeContract(
                describeContract,
                entry.Kind,
                entry.Policy,
                ownerName,
                exposure,
                out inputError);
        }

        if (!describeContractValid)
        {
            error = inputError;
            return false;
        }

        if (!OperationJsonContractAcceptanceValidator.TryValidate(
                entry.ArgsContract!.Value,
                ownerName,
                "argsContract",
                out error)
            || (entry.ResultContract != null
                && !OperationJsonContractAcceptanceValidator.TryValidate(
                    entry.ResultContract.Value,
                    ownerName,
                    "resultContract",
                    out error)))
        {
            return false;
        }

        error = null;
        return true;
    }
}
