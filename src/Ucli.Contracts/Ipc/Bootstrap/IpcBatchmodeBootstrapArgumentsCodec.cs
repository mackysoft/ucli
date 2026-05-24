using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes Unity batchmode bootstrap command-line arguments. </summary>
public static class IpcBatchmodeBootstrapArgumentsCodec
{
    private static readonly string[] KnownArgumentNames =
    {
        IpcBatchmodeBootstrapArgumentNames.Target,
        IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint,
        IpcDaemonBootstrapArgumentNames.RepositoryRoot,
        IpcDaemonBootstrapArgumentNames.SessionPath,
        IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc,
        IpcEndpointBootstrapArgumentNames.TransportKind,
        IpcEndpointBootstrapArgumentNames.Address,
        IpcOneshotBootstrapArgumentNames.ParentProcessId,
        IpcOneshotBootstrapArgumentNames.SessionToken,
        IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc,
    };

    /// <summary> Appends batchmode bootstrap argument token pairs to destination list. </summary>
    /// <param name="destination"> The destination token list. </param>
    /// <param name="arguments"> The bootstrap argument payload. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="destination" /> or <paramref name="arguments" /> is <see langword="null" />. </exception>
    public static void AppendTokens (
        IList<string> destination,
        IpcBatchmodeBootstrapArguments arguments)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        switch (arguments)
        {
            case IpcDaemonBootstrapArguments daemonArguments:
                IpcBatchmodeBootstrapTokenAppender.AppendDaemon(destination, daemonArguments);
                return;

            case IpcOneshotBootstrapArguments oneshotArguments:
                IpcBatchmodeBootstrapTokenAppender.AppendOneshot(destination, oneshotArguments);
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(arguments), arguments, null);
        }
    }

    /// <summary> Tries to parse batchmode bootstrap argument pairs from command-line token list. </summary>
    /// <param name="args"> The command-line token list. </param>
    /// <param name="arguments"> The parsed bootstrap arguments on success. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        IReadOnlyList<string>? args,
        out IpcBatchmodeBootstrapArguments arguments,
        out IpcBatchmodeBootstrapParseError error)
    {
        arguments = default!;
        if (args == null)
        {
            error = MissingTarget();
            return false;
        }

        if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcBatchmodeBootstrapArgumentNames.Target, out var target))
        {
            error = MissingTarget();
            return false;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            error = EmptyRequiredValue("uCLI batchmode bootstrap target must not be empty.");
            return false;
        }

        if (string.Equals(target, IpcBatchmodeBootstrapTargetValues.Daemon, StringComparison.Ordinal))
        {
            return TryParseDaemon(args, out arguments, out error);
        }

        if (string.Equals(target, IpcBatchmodeBootstrapTargetValues.Oneshot, StringComparison.Ordinal))
        {
            return TryParseOneshot(args, out arguments, out error);
        }

        error = InvalidTarget(target);
        return false;
    }

    private static bool TryParseDaemon (
        IReadOnlyList<string> args,
        out IpcBatchmodeBootstrapArguments arguments,
        out IpcBatchmodeBootstrapParseError error)
    {
        arguments = default!;
        if (!IpcBatchmodeBootstrapArgumentReader.TryReadDaemon(args, KnownArgumentNames, out var values, out error))
        {
            return false;
        }

        if (!TryParseTimestamp(values.SessionIssuedAtUtcText, "uCLI daemon bootstrap session issued-at timestamp must be a valid ISO 8601 timestamp with explicit timezone offset.", out var sessionIssuedAtUtc, out error))
        {
            return false;
        }

        arguments = new IpcDaemonBootstrapArguments(
            RepositoryRoot: values.RepositoryRoot,
            ProjectFingerprint: values.ProjectFingerprint,
            SessionPath: values.SessionPath,
            SessionIssuedAtUtc: sessionIssuedAtUtc,
            EndpointTransportKind: values.EndpointTransportKind,
            EndpointAddress: values.EndpointAddress);
        error = IpcBatchmodeBootstrapParseError.None;
        return true;
    }

    private static bool TryParseOneshot (
        IReadOnlyList<string> args,
        out IpcBatchmodeBootstrapArguments arguments,
        out IpcBatchmodeBootstrapParseError error)
    {
        arguments = default!;
        if (!IpcBatchmodeBootstrapArgumentReader.TryReadOneshot(args, KnownArgumentNames, out var values, out error))
        {
            return false;
        }

        if (!TryParsePositiveInt32(values.ParentProcessIdText, "uCLI oneshot bootstrap parent process identifier must be a positive integer.", out var parentProcessId, out error))
        {
            return false;
        }

        if (!TryParseTimestamp(values.ExitDeadlineUtcText, "uCLI oneshot bootstrap exit deadline timestamp must be a valid ISO 8601 timestamp with explicit timezone offset.", out var exitDeadlineUtc, out error))
        {
            return false;
        }

        arguments = new IpcOneshotBootstrapArguments(
            parentProcessId,
            values.ProjectFingerprint,
            values.SessionToken,
            exitDeadlineUtc,
            values.EndpointTransportKind,
            values.EndpointAddress);
        error = IpcBatchmodeBootstrapParseError.None;
        return true;
    }

    private static bool TryParseTimestamp (
        string text,
        string errorMessage,
        out DateTimeOffset value,
        out IpcBatchmodeBootstrapParseError error)
    {
        if (IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(text, out var parsed)
            && parsed is DateTimeOffset timestamp)
        {
            value = timestamp;
            error = IpcBatchmodeBootstrapParseError.None;
            return true;
        }

        value = default;
        error = EmptyRequiredValue(errorMessage);
        return false;
    }

    private static bool TryParsePositiveInt32 (
        string text,
        string errorMessage,
        out int value,
        out IpcBatchmodeBootstrapParseError error)
    {
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0)
        {
            error = IpcBatchmodeBootstrapParseError.None;
            return true;
        }

        value = 0;
        error = EmptyRequiredValue(errorMessage);
        return false;
    }

    private static IpcBatchmodeBootstrapParseError MissingTarget ()
    {
        return new IpcBatchmodeBootstrapParseError(
            IpcBatchmodeBootstrapParseErrorKind.MissingTarget,
            "uCLI batchmode bootstrap target is missing.");
    }

    private static IpcBatchmodeBootstrapParseError InvalidTarget (string target)
    {
        return new IpcBatchmodeBootstrapParseError(
            IpcBatchmodeBootstrapParseErrorKind.InvalidTarget,
            $"uCLI batchmode bootstrap target is invalid. Actual: {target}");
    }

    private static IpcBatchmodeBootstrapParseError MissingRequiredArguments (string message)
    {
        return new IpcBatchmodeBootstrapParseError(
            IpcBatchmodeBootstrapParseErrorKind.MissingRequiredArguments,
            message);
    }

    private static IpcBatchmodeBootstrapParseError EmptyRequiredValue (string message)
    {
        return new IpcBatchmodeBootstrapParseError(
            IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue,
            message);
    }
}
