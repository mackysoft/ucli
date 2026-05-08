using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes Unity GUI bootstrap command-line arguments. </summary>
public static class IpcGuiBootstrapArgumentsCodec
{
    private static readonly string[] KnownArgumentNames =
    {
        IpcGuiBootstrapArgumentNames.Target,
        IpcGuiBootstrapArgumentNames.OwnerProcessId,
        IpcGuiBootstrapArgumentNames.CanShutdownProcess,
    };

    /// <summary> Appends GUI bootstrap argument token pairs to destination list. </summary>
    /// <param name="destination"> The destination token list. </param>
    /// <param name="arguments"> The GUI bootstrap arguments. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="destination" /> or <paramref name="arguments" /> is <see langword="null" />. </exception>
    public static void AppendTokens (
        IList<string> destination,
        IpcGuiBootstrapArguments arguments)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        destination.Add(IpcGuiBootstrapArgumentNames.Target);
        destination.Add(IpcGuiBootstrapTargetValues.Daemon);
        destination.Add(IpcGuiBootstrapArgumentNames.OwnerProcessId);
        destination.Add(arguments.OwnerProcessId.ToString(CultureInfo.InvariantCulture));
        destination.Add(IpcGuiBootstrapArgumentNames.CanShutdownProcess);
        destination.Add(arguments.CanShutdownProcess ? "true" : "false");
    }

    /// <summary> Tries to parse GUI bootstrap argument pairs from command-line token list. </summary>
    /// <param name="args"> The command-line token list. </param>
    /// <param name="arguments"> The parsed GUI bootstrap arguments on success. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        IReadOnlyList<string>? args,
        out IpcGuiBootstrapArguments arguments,
        out IpcGuiBootstrapParseError error)
    {
        arguments = default!;
        if (args == null)
        {
            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.MissingTarget,
                "uCLI GUI bootstrap target is missing.");
            return false;
        }

        if (!TryGetArgumentValue(args, IpcGuiBootstrapArgumentNames.Target, out var target))
        {
            if (ContainsArgumentName(args, IpcGuiBootstrapArgumentNames.Target))
            {
                error = new IpcGuiBootstrapParseError(
                    IpcGuiBootstrapParseErrorKind.InvalidTarget,
                    "uCLI GUI bootstrap target value is missing.");
                return false;
            }

            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.MissingTarget,
                "uCLI GUI bootstrap target is missing.");
            return false;
        }

        if (!string.Equals(target, IpcGuiBootstrapTargetValues.Daemon, StringComparison.Ordinal))
        {
            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.InvalidTarget,
                $"uCLI GUI bootstrap target is invalid. Actual: {target}");
            return false;
        }

        if (!TryGetArgumentValue(args, IpcGuiBootstrapArgumentNames.OwnerProcessId, out var ownerProcessIdText)
            || !TryGetArgumentValue(args, IpcGuiBootstrapArgumentNames.CanShutdownProcess, out var canShutdownProcessText))
        {
            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.MissingRequiredArguments,
                "uCLI GUI bootstrap arguments are missing.");
            return false;
        }

        if (!int.TryParse(ownerProcessIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var ownerProcessId)
            || ownerProcessId <= 0)
        {
            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.InvalidRequiredValue,
                "uCLI GUI bootstrap owner process identifier must be a positive integer.");
            return false;
        }

        if (!bool.TryParse(canShutdownProcessText, out var canShutdownProcess))
        {
            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.InvalidRequiredValue,
                "uCLI GUI bootstrap canShutdownProcess value must be true or false.");
            return false;
        }

        arguments = new IpcGuiBootstrapArguments(ownerProcessId, canShutdownProcess);
        error = IpcGuiBootstrapParseError.None;
        return true;
    }

    private static bool TryGetArgumentValue (
        IReadOnlyList<string> args,
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
            if (IsKnownArgumentName(nextToken))
            {
                continue;
            }

            value = nextToken;
            return true;
        }

        return false;
    }

    private static bool ContainsArgumentName (
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

    private static bool IsKnownArgumentName (string token)
    {
        for (var i = 0; i < KnownArgumentNames.Length; i++)
        {
            if (string.Equals(token, KnownArgumentNames[i], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
