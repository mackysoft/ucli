using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Decodes Unity GUI bootstrap command-line arguments. </summary>
public static class IpcGuiBootstrapArgumentsCodec
{
    private static readonly string[] KnownArgumentNames =
    {
        IpcGuiBootstrapArgumentNames.Target,
        IpcGuiBootstrapArgumentNames.OwnerProcessId,
        IpcGuiBootstrapArgumentNames.CanShutdownProcess,
    };

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

        if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcGuiBootstrapArgumentNames.Target, out var target))
        {
            if (IpcBootstrapArgumentReader.ContainsArgumentName(args, IpcGuiBootstrapArgumentNames.Target))
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

        if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcGuiBootstrapArgumentNames.OwnerProcessId, out var ownerProcessIdText)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcGuiBootstrapArgumentNames.CanShutdownProcess, out var canShutdownProcessText))
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

}
