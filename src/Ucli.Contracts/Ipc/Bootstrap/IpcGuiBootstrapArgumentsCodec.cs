using System.Globalization;
using MackySoft.Ucli.Contracts.Text;

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

    /// <summary> Appends GUI bootstrap argument token pairs to destination list. </summary>
    /// <param name="destination"> The destination token list. </param>
    /// <param name="arguments"> The GUI bootstrap argument payload. </param>
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
        destination.Add(TextVocabulary.GetText(IpcBootstrapTarget.Daemon));
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
        if (!TryReadTarget(args, out error))
        {
            return false;
        }

        if (!TryReadRequiredValues(args!, out var ownerProcessIdText, out var canShutdownProcessText, out error))
        {
            return false;
        }

        if (!TryParseOwnerProcessId(ownerProcessIdText, out var ownerProcessId, out error))
        {
            return false;
        }

        if (!TryParseCanShutdownProcess(canShutdownProcessText, out var canShutdownProcess, out error))
        {
            return false;
        }

        arguments = new IpcGuiBootstrapArguments(ownerProcessId, canShutdownProcess);
        error = IpcGuiBootstrapParseError.None;
        return true;
    }

    private static bool TryReadTarget (
        IReadOnlyList<string>? args,
        out IpcGuiBootstrapParseError error)
    {
        if (args == null)
        {
            error = MissingTarget();
            return false;
        }

        if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcGuiBootstrapArgumentNames.Target, out var target))
        {
            return TryHandleMissingTargetValue(args, out error);
        }

        return TryValidateTarget(target, out error);
    }

    private static bool TryReadRequiredValues (
        IReadOnlyList<string> args,
        out string ownerProcessIdText,
        out string canShutdownProcessText,
        out IpcGuiBootstrapParseError error)
    {
        ownerProcessIdText = string.Empty;
        canShutdownProcessText = string.Empty;
        if (IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcGuiBootstrapArgumentNames.OwnerProcessId, out ownerProcessIdText)
            && IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcGuiBootstrapArgumentNames.CanShutdownProcess, out canShutdownProcessText))
        {
            error = IpcGuiBootstrapParseError.None;
            return true;
        }

        error = new IpcGuiBootstrapParseError(
            IpcGuiBootstrapParseErrorKind.MissingRequiredArguments,
            "uCLI GUI bootstrap arguments are missing.");
        return false;
    }

    private static bool TryParseOwnerProcessId (
        string text,
        out int ownerProcessId,
        out IpcGuiBootstrapParseError error)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ownerProcessId)
            && ownerProcessId > 0)
        {
            error = IpcGuiBootstrapParseError.None;
            return true;
        }

        error = new IpcGuiBootstrapParseError(
            IpcGuiBootstrapParseErrorKind.InvalidRequiredValue,
            "uCLI GUI bootstrap owner process identifier must be a positive integer.");
        return false;
    }

    private static bool TryParseCanShutdownProcess (
        string text,
        out bool canShutdownProcess,
        out IpcGuiBootstrapParseError error)
    {
        if (bool.TryParse(text, out canShutdownProcess))
        {
            error = IpcGuiBootstrapParseError.None;
            return true;
        }

        error = new IpcGuiBootstrapParseError(
            IpcGuiBootstrapParseErrorKind.InvalidRequiredValue,
            "uCLI GUI bootstrap canShutdownProcess value must be true or false.");
        return false;
    }

    private static bool TryHandleMissingTargetValue (
        IReadOnlyList<string> args,
        out IpcGuiBootstrapParseError error)
    {
        if (IpcBootstrapArgumentReader.ContainsArgumentName(args, IpcGuiBootstrapArgumentNames.Target))
        {
            error = new IpcGuiBootstrapParseError(
                IpcGuiBootstrapParseErrorKind.InvalidTarget,
                "uCLI GUI bootstrap target value is missing.");
            return false;
        }

        error = MissingTarget();
        return false;
    }

    private static bool TryValidateTarget (
        string target,
        out IpcGuiBootstrapParseError error)
    {
        if (TextVocabulary.TryGetValue<IpcBootstrapTarget>(target, out var bootstrapTarget)
            && bootstrapTarget == IpcBootstrapTarget.Daemon)
        {
            error = IpcGuiBootstrapParseError.None;
            return true;
        }

        error = new IpcGuiBootstrapParseError(
            IpcGuiBootstrapParseErrorKind.InvalidTarget,
            $"uCLI GUI bootstrap target is invalid. Actual: {target}");
        return false;
    }

    private static IpcGuiBootstrapParseError MissingTarget ()
    {
        return new IpcGuiBootstrapParseError(
            IpcGuiBootstrapParseErrorKind.MissingTarget,
            "uCLI GUI bootstrap target is missing.");
    }
}
