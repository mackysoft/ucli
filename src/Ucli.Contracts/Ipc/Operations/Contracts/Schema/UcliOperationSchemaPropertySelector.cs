using System.Reflection;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationSchemaPropertySelector
{
    public static IReadOnlyList<PropertyInfo> GetSchemaProperties (Type contractType)
    {
        return UcliOperationContractReflection.GetContractProperties(contractType)
            .Where(static property => !UcliRequestLocalAliasContractPolicy.IsInternalRequestLocalAliasBranchProperty(property))
            .ToArray();
    }
}
