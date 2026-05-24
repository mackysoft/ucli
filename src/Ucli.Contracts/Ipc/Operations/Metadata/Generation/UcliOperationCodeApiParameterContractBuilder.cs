using System.Reflection;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeApiParameterContractBuilder
{
    public static IReadOnlyList<UcliCodeApiParameterContract> Create (MethodBase method)
    {
        var parameters = method.GetParameters();
        if (parameters.Length == 0)
        {
            return Array.Empty<UcliCodeApiParameterContract>();
        }

        var contracts = new UcliCodeApiParameterContract[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            contracts[i] = CreateOne(parameters[i]);
        }

        return contracts;
    }

    private static UcliCodeApiParameterContract CreateOne (ParameterInfo parameter)
    {
        return new UcliCodeApiParameterContract(
            parameter.Name,
            UcliOperationCodeTypeName.Get(parameter.ParameterType),
            UcliOperationCodeDescriptionReader.Get(parameter));
    }
}
