namespace MackySoft.Ucli.Contracts.Ipc;

internal static class IpcBootstrapArgumentReader
{
    public static bool TryGetArgumentValue (
        IReadOnlyList<string> args,
        IReadOnlyList<string> knownArgumentNames,
        string argumentName,
        out string value)
    {
        value = string.Empty;
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (!string.Equals(args[i], argumentName, StringComparison.Ordinal))
            {
                continue;
            }

            var nextToken = args[i + 1];
            if (IsKnownArgumentName(knownArgumentNames, nextToken))
            {
                continue;
            }

            value = nextToken;
            return true;
        }

        return false;
    }

    public static bool ContainsArgumentName (
        IReadOnlyList<string> args,
        string argumentName)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsKnownArgumentName (
        IReadOnlyList<string> knownArgumentNames,
        string token)
    {
        for (var i = 0; i < knownArgumentNames.Count; i++)
        {
            if (string.Equals(token, knownArgumentNames[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
