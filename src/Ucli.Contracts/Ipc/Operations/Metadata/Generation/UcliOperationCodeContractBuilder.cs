using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Builds source-facing code contracts from documented public API types. </summary>
public static class UcliOperationCodeContractBuilder
{
    /// <summary> Creates one C# code contract. </summary>
    public static UcliOperationCodeContract CreateCSharp (
        string entryPointSignature,
        string entryPointMatchRule,
        bool requiredStatic,
        IReadOnlyList<Type> parameterTypes,
        string returnValue,
        IReadOnlyList<UcliCodeSourceFormContract> sourceForms,
        IReadOnlyList<Type> apiTypes)
    {
        UcliOperationCodeContractArguments.ThrowIfInvalid(entryPointSignature, entryPointMatchRule, parameterTypes, returnValue, sourceForms, apiTypes);

        return new UcliOperationCodeContract(
            UcliCodeLanguage.CSharp,
            new UcliCodeEntryPointContract(
                entryPointSignature,
                entryPointMatchRule,
                requiredStatic,
                UcliOperationCodeParameterTypeNames.Create(parameterTypes),
                returnValue),
            UcliOperationCodeSourceFormContractBuilder.Create(sourceForms),
            UcliOperationCodeApiTypeContractBuilder.Create(apiTypes));
    }
}
