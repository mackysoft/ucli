using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates one code metadata API member entry. </summary>
internal static class UcliOperationCodeApiMemberValidator
{
    public static bool TryValidate (
        UcliCodeApiMemberContract? member,
        int memberIndex,
        string ownerName,
        HashSet<string> memberNames,
        out string errorMessage)
    {
        if (!TryValidateCommon(member, memberIndex, ownerName, memberNames, out errorMessage))
        {
            return false;
        }

        return member!.Kind == UcliCodeApiMemberKindValues.Property
            ? TryValidateProperty(member, memberIndex, ownerName, out errorMessage)
            : TryValidateMethod(member, memberIndex, ownerName, out errorMessage);
    }

    private static bool TryValidateCommon (
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

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateProperty (
        UcliCodeApiMemberContract member,
        int memberIndex,
        string ownerName,
        out string errorMessage)
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

    private static bool TryValidateMethod (
        UcliCodeApiMemberContract member,
        int memberIndex,
        string ownerName,
        out string errorMessage)
    {
        if (member.Kind != UcliCodeApiMemberKindValues.Method
            || member.Type != null
            || string.IsNullOrWhiteSpace(member.ReturnType)
            || member.Parameters == null)
        {
            errorMessage = $"{ownerName} has an invalid codeContract method member at index {memberIndex}.";
            return false;
        }

        return TryValidateParameters(member, ownerName, out errorMessage);
    }

    private static bool TryValidateParameters (
        UcliCodeApiMemberContract member,
        string ownerName,
        out string errorMessage)
    {
        for (var parameterIndex = 0; parameterIndex < member.Parameters!.Count; parameterIndex++)
        {
            if (!TryValidateParameter(member.Parameters[parameterIndex], parameterIndex, ownerName, out errorMessage))
            {
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private static bool TryValidateParameter (
        UcliCodeApiParameterContract? parameter,
        int parameterIndex,
        string ownerName,
        out string errorMessage)
    {
        if (parameter == null
            || string.IsNullOrWhiteSpace(parameter.Name)
            || string.IsNullOrWhiteSpace(parameter.Type)
            || string.IsNullOrWhiteSpace(parameter.Description))
        {
            errorMessage = $"{ownerName} has an invalid codeContract method parameter at index {parameterIndex}.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
