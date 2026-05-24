using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates source-facing operation code metadata. </summary>
internal static class UcliOperationCodeContractValidator
{
    public static bool TryValidate (
        UcliOperationCodeContract? codeContract,
        string ownerName,
        out string errorMessage)
    {
        if (codeContract == null)
        {
            errorMessage = string.Empty;
            return true;
        }

        if (!TryValidateRoot(codeContract, ownerName, out errorMessage)
            || !TryValidateEntryPointParameters(codeContract, ownerName, out errorMessage)
            || !TryValidateSourceForms(codeContract, ownerName, out errorMessage)
            || !TryValidateApiTypes(codeContract, ownerName, out errorMessage))
        {
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateRoot (
        UcliOperationCodeContract codeContract,
        string ownerName,
        out string errorMessage)
    {
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

        return TryValidateLanguage(codeContract.Language, ownerName, out errorMessage);
    }

    private static bool TryValidateLanguage (
        string language,
        string ownerName,
        out string errorMessage)
    {
        if (!string.Equals(language, "csharp", StringComparison.Ordinal))
        {
            errorMessage = $"{ownerName} has an unsupported codeContract language.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateEntryPointParameters (
        UcliOperationCodeContract codeContract,
        string ownerName,
        out string errorMessage)
    {
        for (var parameterTypeIndex = 0; parameterTypeIndex < codeContract.EntryPoint!.ParameterTypes!.Count; parameterTypeIndex++)
        {
            if (string.IsNullOrWhiteSpace(codeContract.EntryPoint.ParameterTypes[parameterTypeIndex]))
            {
                errorMessage = $"{ownerName} has an invalid codeContract entry point parameter type.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateSourceForms (
        UcliOperationCodeContract codeContract,
        string ownerName,
        out string errorMessage)
    {
        for (var sourceFormIndex = 0; sourceFormIndex < codeContract.SourceForms!.Count; sourceFormIndex++)
        {
            if (!TryValidateSourceForm(codeContract.SourceForms[sourceFormIndex], sourceFormIndex, ownerName, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateSourceForm (
        UcliCodeSourceFormContract? sourceForm,
        int sourceFormIndex,
        string ownerName,
        out string errorMessage)
    {
        if (sourceForm == null || string.IsNullOrWhiteSpace(sourceForm.Kind) || string.IsNullOrWhiteSpace(sourceForm.Description))
        {
            errorMessage = $"{ownerName} has an invalid codeContract source form at index {sourceFormIndex}.";
            return false;
        }

        return TryValidateSourceFormKind(sourceForm.Kind, sourceFormIndex, ownerName, out errorMessage);
    }

    private static bool TryValidateSourceFormKind (
        string kind,
        int sourceFormIndex,
        string ownerName,
        out string errorMessage)
    {
        if (kind is CsEvalSourceKindValues.CompilationUnit or CsEvalSourceKindValues.Snippet)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"{ownerName} has an unsupported codeContract source form at index {sourceFormIndex}.";
        return false;
    }

    private static bool TryValidateApiTypes (
        UcliOperationCodeContract codeContract,
        string ownerName,
        out string errorMessage)
    {
        for (var typeIndex = 0; typeIndex < codeContract.ApiTypes!.Count; typeIndex++)
        {
            if (!TryValidateCodeApiType(codeContract.ApiTypes[typeIndex], typeIndex, ownerName, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
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

        return TryValidateApiMembers(apiType, ownerName, out errorMessage);
    }

    private static bool TryValidateApiMembers (
        UcliCodeApiTypeContract apiType,
        string ownerName,
        out string errorMessage)
    {
        var memberNames = new HashSet<string>(StringComparer.Ordinal);
        for (var memberIndex = 0; memberIndex < apiType.Members!.Count; memberIndex++)
        {
            if (!UcliOperationCodeApiMemberValidator.TryValidate(apiType.Members[memberIndex], memberIndex, ownerName, memberNames, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }
}
