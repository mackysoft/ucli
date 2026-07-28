using System.Reflection;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeDescriptionReader
{
    public static string Get (MemberInfo member)
    {
        return member.GetCustomAttribute<UcliCodeDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Code contract member '{member.Name}' must declare {nameof(UcliCodeDescriptionAttribute)}.");
    }

    public static string Get (ParameterInfo parameter)
    {
        return parameter.GetCustomAttribute<UcliCodeDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Code contract parameter '{parameter.Name}' must declare {nameof(UcliCodeDescriptionAttribute)}.");
    }
}
