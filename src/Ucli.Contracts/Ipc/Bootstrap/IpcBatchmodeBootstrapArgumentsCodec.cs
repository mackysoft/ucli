using MackySoft.Ucli.Contracts.Text;

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
        IpcDaemonBootstrapArgumentNames.SessionGenerationId,
        IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc,
        IpcEndpointBootstrapArgumentNames.TransportKind,
        IpcEndpointBootstrapArgumentNames.Address,
        IpcOneshotBootstrapArgumentNames.BootstrapId,
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

        if (!ContractLiteralCodec.TryParse<IpcBootstrapTarget>(target, out var bootstrapTarget))
        {
            error = InvalidTarget(target);
            return false;
        }

        switch (bootstrapTarget)
        {
            case IpcBootstrapTarget.Daemon:
                return TryParseDaemon(args, out arguments, out error);
            case IpcBootstrapTarget.Oneshot:
                return TryParseOneshot(args, out arguments, out error);
            default:
                throw new InvalidOperationException($"Unsupported IPC bootstrap target: {bootstrapTarget}.");
        }
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

        if (!TryParseProjectFingerprint(values.ProjectFingerprint, out var projectFingerprint, out error))
        {
            return false;
        }

        if (!Guid.TryParseExact(values.SessionGenerationIdText, "D", out var sessionGenerationId)
            || sessionGenerationId == Guid.Empty)
        {
            error = new IpcBatchmodeBootstrapParseError(
                IpcBatchmodeBootstrapParseErrorKind.InvalidSessionGenerationId,
                "uCLI daemon bootstrap session generation identifier must be a non-empty GUID in D format.");
            return false;
        }

        if (!TryParseTimestamp(values.SessionIssuedAtUtcText, "uCLI daemon bootstrap session issued-at timestamp must be a valid ISO 8601 UTC timestamp.", out var sessionIssuedAtUtc, out error))
        {
            return false;
        }

        if (!TryParseEndpoint(values.EndpointTransportKind, values.EndpointAddress, out var endpoint, out error))
        {
            return false;
        }

        arguments = new IpcDaemonBootstrapArguments(
            RepositoryRoot: values.RepositoryRoot,
            ProjectFingerprint: projectFingerprint,
            SessionPath: values.SessionPath,
            SessionGenerationId: sessionGenerationId,
            SessionIssuedAtUtc: sessionIssuedAtUtc,
            Endpoint: endpoint);
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

        if (!Guid.TryParseExact(values.BootstrapIdText, "D", out var bootstrapId)
            || bootstrapId == Guid.Empty)
        {
            error = new IpcBatchmodeBootstrapParseError(
                IpcBatchmodeBootstrapParseErrorKind.InvalidBootstrapId,
                "uCLI oneshot bootstrap identifier must be a non-empty GUID in D format.");
            return false;
        }

        arguments = new IpcOneshotBootstrapArguments(bootstrapId);
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
            && parsed is DateTimeOffset { Offset: { Ticks: 0 } } timestamp
            && timestamp != default)
        {
            value = timestamp;
            error = IpcBatchmodeBootstrapParseError.None;
            return true;
        }

        value = default;
        error = EmptyRequiredValue(errorMessage);
        return false;
    }

    private static bool TryParseEndpoint (
        string transportKindText,
        string address,
        out IpcEndpoint endpoint,
        out IpcBatchmodeBootstrapParseError error)
    {
        endpoint = default!;
        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(transportKindText, out var transportKind))
        {
            error = new IpcBatchmodeBootstrapParseError(
                IpcBatchmodeBootstrapParseErrorKind.InvalidEndpointTransportKind,
                "uCLI batchmode bootstrap endpoint transport kind is unsupported.");
            return false;
        }

        try
        {
            endpoint = new IpcEndpoint(transportKind, address);
            error = IpcBatchmodeBootstrapParseError.None;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = new IpcBatchmodeBootstrapParseError(
                IpcBatchmodeBootstrapParseErrorKind.InvalidEndpointAddress,
                exception.Message);
            return false;
        }
    }

    private static bool TryParseProjectFingerprint (
        string text,
        out ProjectFingerprint value,
        out IpcBatchmodeBootstrapParseError error)
    {
        if (ProjectFingerprint.TryParse(text, out var fingerprint))
        {
            value = fingerprint;
            error = IpcBatchmodeBootstrapParseError.None;
            return true;
        }

        value = default!;
        error = new IpcBatchmodeBootstrapParseError(
            IpcBatchmodeBootstrapParseErrorKind.InvalidProjectFingerprint,
            "uCLI batchmode bootstrap project fingerprint must be exactly 64 lowercase hexadecimal SHA-256 characters.");
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
