using System.Reflection;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeDescriptionReader
{
    public static string Get (MemberInfo member)
    {
        return member.GetCustomAttribute<UcliDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Code contract member '{member.Name}' must declare {nameof(UcliDescriptionAttribute)}.");
    }

    public static string Get (ParameterInfo parameter)
    {
        return parameter.GetCustomAttribute<UcliDescriptionAttribute>()?.Description
            ?? throw new InvalidOperationException($"Code contract parameter '{parameter.Name}' must declare {nameof(UcliDescriptionAttribute)}.");
    }
}
