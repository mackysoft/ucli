namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeTypeName
{
    public static string Get (Type type)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return type == typeof(void) ? "void" : (type.FullName ?? type.Name);
    }
}
