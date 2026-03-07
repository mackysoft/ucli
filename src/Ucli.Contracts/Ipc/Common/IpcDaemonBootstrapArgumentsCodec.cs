namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Encodes and decodes Unity daemon bootstrap command-line arguments. </summary>
public static class IpcDaemonBootstrapArgumentsCodec
{
    /// <summary> Appends daemon bootstrap argument token pairs to destination list. </summary>
    /// <param name="destination"> The destination token list. </param>
    /// <param name="arguments"> The bootstrap argument payload. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="destination" /> or <paramref name="arguments" /> is <see langword="null" />. </exception>
    public static void AppendTokens (
        IList<string> destination,
        IpcDaemonBootstrapArguments arguments)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        if (arguments == null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        destination.Add(IpcDaemonBootstrapArgumentNames.RepositoryRoot);
        destination.Add(arguments.RepositoryRoot);
        destination.Add(IpcDaemonBootstrapArgumentNames.ProjectFingerprint);
        destination.Add(arguments.ProjectFingerprint);
        destination.Add(IpcDaemonBootstrapArgumentNames.SessionPath);
        destination.Add(arguments.SessionPath);
        destination.Add(IpcDaemonBootstrapArgumentNames.EndpointTransportKind);
        destination.Add(arguments.EndpointTransportKind);
        destination.Add(IpcDaemonBootstrapArgumentNames.EndpointAddress);
        destination.Add(arguments.EndpointAddress);
    }

    /// <summary> Tries to parse daemon bootstrap argument pairs from command-line token list. </summary>
    /// <param name="args"> The command-line token list. </param>
    /// <param name="arguments"> The parsed bootstrap arguments on success. </param>
    /// <param name="error"> The parse error on failure. </param>
    /// <returns> <see langword="true" /> when parse succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        IReadOnlyList<string>? args,
        out IpcDaemonBootstrapArguments arguments,
        out IpcDaemonBootstrapParseError error)
    {
        arguments = default!;
        if (args == null)
        {
            error = MissingRequiredArguments();
            return false;
        }

        if (!TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.RepositoryRoot, out var repositoryRoot)
            || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.ProjectFingerprint, out var projectFingerprint)
            || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.SessionPath, out var sessionPath)
            || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.EndpointTransportKind, out var endpointTransportKind)
            || !TryGetArgumentValue(args, IpcDaemonBootstrapArgumentNames.EndpointAddress, out var endpointAddress))
        {
            error = MissingRequiredArguments();
            return false;
        }

        if (string.IsNullOrWhiteSpace(repositoryRoot)
            || string.IsNullOrWhiteSpace(projectFingerprint)
            || string.IsNullOrWhiteSpace(sessionPath)
            || string.IsNullOrWhiteSpace(endpointTransportKind)
            || string.IsNullOrWhiteSpace(endpointAddress))
        {
            error = EmptyRequiredValue();
            return false;
        }

        arguments = new IpcDaemonBootstrapArguments(
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            SessionPath: sessionPath,
            EndpointTransportKind: endpointTransportKind,
            EndpointAddress: endpointAddress);
        error = IpcDaemonBootstrapParseError.None;
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

    private static bool IsKnownArgumentName (string token)
    {
        return string.Equals(token, IpcDaemonBootstrapArgumentNames.RepositoryRoot, StringComparison.Ordinal)
            || string.Equals(token, IpcDaemonBootstrapArgumentNames.ProjectFingerprint, StringComparison.Ordinal)
            || string.Equals(token, IpcDaemonBootstrapArgumentNames.SessionPath, StringComparison.Ordinal)
            || string.Equals(token, IpcDaemonBootstrapArgumentNames.EndpointTransportKind, StringComparison.Ordinal)
            || string.Equals(token, IpcDaemonBootstrapArgumentNames.EndpointAddress, StringComparison.Ordinal);
    }

    private static IpcDaemonBootstrapParseError MissingRequiredArguments ()
    {
        return new IpcDaemonBootstrapParseError(
            IpcDaemonBootstrapParseErrorKind.MissingRequiredArguments,
            "uCLI daemon bootstrap arguments are missing.");
    }

    private static IpcDaemonBootstrapParseError EmptyRequiredValue ()
    {
        return new IpcDaemonBootstrapParseError(
            IpcDaemonBootstrapParseErrorKind.EmptyRequiredValue,
            "uCLI daemon bootstrap arguments must not be empty.");
    }
}