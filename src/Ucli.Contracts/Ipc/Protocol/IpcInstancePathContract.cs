namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Validates RFC 6901 instance paths exposed by IPC diagnostics. </summary>
internal static class IpcInstancePathContract
{
    public static string Require (
        string value,
        string parameterName)
    {
        return RequireOptional(value, parameterName)!;
    }

    public static string? RequireOptional (
        string? value,
        string parameterName)
    {
        if (value == null)
        {
            return null;
        }
        if (value.Length == 0 || value[0] != '/')
        {
            throw new ArgumentException("Instance path must be a non-root RFC 6901 JSON Pointer.", parameterName);
        }

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '~')
            {
                continue;
            }
            if (index + 1 >= value.Length || value[index + 1] is not ('0' or '1'))
            {
                throw new ArgumentException("Instance path contains an invalid RFC 6901 escape.", parameterName);
            }

            index++;
        }

        return value;
    }
}
