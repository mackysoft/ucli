namespace MackySoft.Ucli.Contracts.Ipc;

internal static class IpcBatchmodeBootstrapArgumentReader
{
    private static readonly string[] DaemonRequiredNames =
    {
        IpcDaemonBootstrapArgumentNames.RepositoryRoot,
        IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint,
        IpcDaemonBootstrapArgumentNames.SessionPath,
        IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc,
        IpcEndpointBootstrapArgumentNames.TransportKind,
        IpcEndpointBootstrapArgumentNames.Address,
    };

    private static readonly string[] OneshotRequiredNames =
    {
        IpcOneshotBootstrapArgumentNames.ParentProcessId,
        IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint,
        IpcOneshotBootstrapArgumentNames.SessionToken,
        IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc,
        IpcEndpointBootstrapArgumentNames.TransportKind,
        IpcEndpointBootstrapArgumentNames.Address,
    };

    public static bool TryReadDaemon (
        IReadOnlyList<string> args,
        IReadOnlyList<string> knownArgumentNames,
        out DaemonValues values,
        out IpcBatchmodeBootstrapParseError error)
    {
        if (!TryReadRequiredValues(args, knownArgumentNames, DaemonRequiredNames, "uCLI daemon bootstrap arguments are missing.", "uCLI daemon bootstrap arguments must not be empty.", out var rawValues, out error))
        {
            values = default;
            return false;
        }

        values = new DaemonValues(rawValues);
        return true;
    }

    public static bool TryReadOneshot (
        IReadOnlyList<string> args,
        IReadOnlyList<string> knownArgumentNames,
        out OneshotValues values,
        out IpcBatchmodeBootstrapParseError error)
    {
        if (!TryReadRequiredValues(args, knownArgumentNames, OneshotRequiredNames, "uCLI oneshot bootstrap arguments are missing.", "uCLI oneshot bootstrap arguments must not be empty.", out var rawValues, out error))
        {
            values = default;
            return false;
        }

        values = new OneshotValues(rawValues);
        return true;
    }

    private static bool TryReadRequiredValues (
        IReadOnlyList<string> args,
        IReadOnlyList<string> knownArgumentNames,
        IReadOnlyList<string> requiredNames,
        string missingMessage,
        string emptyMessage,
        out string[] values,
        out IpcBatchmodeBootstrapParseError error)
    {
        values = new string[requiredNames.Count];
        for (var i = 0; i < requiredNames.Count; i++)
        {
            if (!IpcBootstrapArgumentReader.TryGetArgumentValue(args, knownArgumentNames, requiredNames[i], out values[i]))
            {
                error = CreateError(IpcBatchmodeBootstrapParseErrorKind.MissingRequiredArguments, missingMessage);
                return false;
            }
        }

        if (HasEmptyValue(values))
        {
            error = CreateError(IpcBatchmodeBootstrapParseErrorKind.EmptyRequiredValue, emptyMessage);
            return false;
        }

        error = IpcBatchmodeBootstrapParseError.None;
        return true;
    }

    private static bool HasEmptyValue (IReadOnlyList<string> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static IpcBatchmodeBootstrapParseError CreateError (
        IpcBatchmodeBootstrapParseErrorKind kind,
        string message)
    {
        return new IpcBatchmodeBootstrapParseError(kind, message);
    }

    public readonly struct DaemonValues
    {
        public DaemonValues (IReadOnlyList<string> values)
        {
            RepositoryRoot = values[0];
            ProjectFingerprint = values[1];
            SessionPath = values[2];
            SessionIssuedAtUtcText = values[3];
            EndpointTransportKind = values[4];
            EndpointAddress = values[5];
        }

        public string RepositoryRoot { get; }

        public string ProjectFingerprint { get; }

        public string SessionPath { get; }

        public string SessionIssuedAtUtcText { get; }

        public string EndpointTransportKind { get; }

        public string EndpointAddress { get; }
    }

    public readonly struct OneshotValues
    {
        public OneshotValues (IReadOnlyList<string> values)
        {
            ParentProcessIdText = values[0];
            ProjectFingerprint = values[1];
            SessionToken = values[2];
            ExitDeadlineUtcText = values[3];
            EndpointTransportKind = values[4];
            EndpointAddress = values[5];
        }

        public string ParentProcessIdText { get; }

        public string ProjectFingerprint { get; }

        public string SessionToken { get; }

        public string ExitDeadlineUtcText { get; }

        public string EndpointTransportKind { get; }

        public string EndpointAddress { get; }
    }
}
