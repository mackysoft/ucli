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
                destination.Add(IpcBatchmodeBootstrapArgumentNames.Target);
                destination.Add(IpcBatchmodeBootstrapTargetValues.Daemon);
                destination.Add(IpcDaemonBootstrapArgumentNames.RepositoryRoot);
                destination.Add(daemonArguments.RepositoryRoot);
                destination.Add(IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint);
                destination.Add(daemonArguments.ProjectFingerprint);
                destination.Add(IpcDaemonBootstrapArgumentNames.SessionPath);
                destination.Add(daemonArguments.SessionPath);
                destination.Add(IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc);
                destination.Add(daemonArguments.SessionIssuedAtUtc.ToString("O", CultureInfo.InvariantCulture));
                destination.Add(IpcEndpointBootstrapArgumentNames.TransportKind);
                destination.Add(daemonArguments.EndpointTransportKind);
                destination.Add(IpcEndpointBootstrapArgumentNames.Address);
                destination.Add(daemonArguments.EndpointAddress);
                return;

            case IpcOneshotBootstrapArguments oneshotArguments:
                destination.Add(IpcBatchmodeBootstrapArgumentNames.Target);
                destination.Add(IpcBatchmodeBootstrapTargetValues.Oneshot);
                destination.Add(IpcOneshotBootstrapArgumentNames.ParentProcessId);
                destination.Add(oneshotArguments.ParentProcessId.ToString(CultureInfo.InvariantCulture));
                destination.Add(IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint);
                destination.Add(oneshotArguments.ProjectFingerprint);
                destination.Add(IpcOneshotBootstrapArgumentNames.SessionToken);
                destination.Add(oneshotArguments.SessionToken);
                destination.Add(IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc);
                destination.Add(oneshotArguments.ExitDeadlineUtc.ToString("O", CultureInfo.InvariantCulture));
                destination.Add(IpcEndpointBootstrapArgumentNames.TransportKind);
                destination.Add(oneshotArguments.EndpointTransportKind);
                destination.Add(IpcEndpointBootstrapArgumentNames.Address);
                destination.Add(oneshotArguments.EndpointAddress);
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
        if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcDaemonBootstrapArgumentNames.RepositoryRoot, out var repositoryRoot)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, out var projectFingerprint)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcDaemonBootstrapArgumentNames.SessionPath, out var sessionPath)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, out var sessionIssuedAtUtcText)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcEndpointBootstrapArgumentNames.TransportKind, out var endpointTransportKind)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcEndpointBootstrapArgumentNames.Address, out var endpointAddress))
        {
            error = MissingRequiredArguments("uCLI daemon bootstrap arguments are missing.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot)
            || string.IsNullOrWhiteSpace(projectFingerprint)
            || string.IsNullOrWhiteSpace(sessionPath)
            || string.IsNullOrWhiteSpace(sessionIssuedAtUtcText)
            || string.IsNullOrWhiteSpace(endpointTransportKind)
            || string.IsNullOrWhiteSpace(endpointAddress))
        {
            error = EmptyRequiredValue("uCLI daemon bootstrap arguments must not be empty.");
            return false;
        }

        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(sessionIssuedAtUtcText, out var sessionIssuedAtUtc)
            || sessionIssuedAtUtc is not DateTimeOffset parsedSessionIssuedAtUtc)
        {
            error = EmptyRequiredValue("uCLI daemon bootstrap session issued-at timestamp must be a valid ISO 8601 timestamp with explicit timezone offset.");
            return false;
        }

        arguments = new IpcDaemonBootstrapArguments(
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            SessionPath: sessionPath,
            SessionIssuedAtUtc: parsedSessionIssuedAtUtc,
            EndpointTransportKind: endpointTransportKind,
            EndpointAddress: endpointAddress);
        error = IpcBatchmodeBootstrapParseError.None;
        return true;
    }

    private static bool TryParseOneshot (
        IReadOnlyList<string> args,
        out IpcBatchmodeBootstrapArguments arguments,
        out IpcBatchmodeBootstrapParseError error)
    {
        arguments = default!;
        if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcOneshotBootstrapArgumentNames.ParentProcessId, out var parentProcessIdText)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, out var projectFingerprint)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcOneshotBootstrapArgumentNames.SessionToken, out var sessionToken)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, out var exitDeadlineUtcText)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcEndpointBootstrapArgumentNames.TransportKind, out var endpointTransportKind)
            || !IpcBootstrapArgumentReader.TryGetArgumentValue(args, KnownArgumentNames, IpcEndpointBootstrapArgumentNames.Address, out var endpointAddress))
        {
            error = MissingRequiredArguments("uCLI oneshot bootstrap arguments are missing.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(parentProcessIdText)
            || string.IsNullOrWhiteSpace(projectFingerprint)
            || string.IsNullOrWhiteSpace(sessionToken)
            || string.IsNullOrWhiteSpace(exitDeadlineUtcText)
            || string.IsNullOrWhiteSpace(endpointTransportKind)
            || string.IsNullOrWhiteSpace(endpointAddress))
        {
            error = EmptyRequiredValue("uCLI oneshot bootstrap arguments must not be empty.");
            return false;
        }

        if (!int.TryParse(parentProcessIdText, NumberStyles.None, CultureInfo.InvariantCulture, out var parentProcessId)
            || parentProcessId <= 0)
        {
            error = EmptyRequiredValue("uCLI oneshot bootstrap parent process identifier must be a positive integer.");
            return false;
        }

        if (!IpcIso8601TimestampCodec.TryParseOptionalWithTimezoneOffset(exitDeadlineUtcText, out var exitDeadlineUtc)
            || exitDeadlineUtc is not DateTimeOffset parsedExitDeadlineUtc)
        {
            error = EmptyRequiredValue("uCLI oneshot bootstrap exit deadline timestamp must be a valid ISO 8601 timestamp with explicit timezone offset.");
            return false;
        }

        arguments = new IpcOneshotBootstrapArguments(
            parentProcessId,
            projectFingerprint,
            sessionToken,
            parsedExitDeadlineUtc,
            endpointTransportKind,
            endpointAddress);
        error = IpcBatchmodeBootstrapParseError.None;
        return true;
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
