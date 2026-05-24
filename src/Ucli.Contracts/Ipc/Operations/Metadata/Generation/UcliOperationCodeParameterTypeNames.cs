namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeParameterTypeNames
{
    public static IReadOnlyList<string> Create (IReadOnlyList<Type> parameterTypes)
    {
        var names = new string[parameterTypes.Count];
        for (var i = 0; i < parameterTypes.Count; i++)
        {
            names[i] = UcliOperationCodeTypeName.Get(parameterTypes[i]);
        }

        return names;
    }
}
