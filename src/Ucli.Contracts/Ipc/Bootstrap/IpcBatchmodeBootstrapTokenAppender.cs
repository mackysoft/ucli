using System.Globalization;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class IpcBatchmodeBootstrapTokenAppender
{
    public static void AppendDaemon (
        IList<string> destination,
        IpcDaemonBootstrapArguments arguments)
    {
        Add(destination, IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Daemon);
        Add(destination, IpcDaemonBootstrapArgumentNames.RepositoryRoot, arguments.RepositoryRoot);
        Add(destination, IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, arguments.ProjectFingerprint);
        Add(destination, IpcDaemonBootstrapArgumentNames.SessionPath, arguments.SessionPath);
        Add(destination, IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, ToIsoText(arguments.SessionIssuedAtUtc));
        AddEndpoint(destination, arguments.EndpointTransportKind, arguments.EndpointAddress);
    }

    public static void AppendOneshot (
        IList<string> destination,
        IpcOneshotBootstrapArguments arguments)
    {
        Add(destination, IpcBatchmodeBootstrapArgumentNames.Target, IpcBatchmodeBootstrapTargetValues.Oneshot);
        Add(destination, IpcOneshotBootstrapArgumentNames.ParentProcessId, arguments.ParentProcessId.ToString(CultureInfo.InvariantCulture));
        Add(destination, IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, arguments.ProjectFingerprint);
        Add(destination, IpcOneshotBootstrapArgumentNames.SessionToken, arguments.SessionToken);
        Add(destination, IpcOneshotBootstrapArgumentNames.ExitDeadlineUtc, ToIsoText(arguments.ExitDeadlineUtc));
        AddEndpoint(destination, arguments.EndpointTransportKind, arguments.EndpointAddress);
    }

    private static void AddEndpoint (
        IList<string> destination,
        string transportKind,
        string address)
    {
        Add(destination, IpcEndpointBootstrapArgumentNames.TransportKind, transportKind);
        Add(destination, IpcEndpointBootstrapArgumentNames.Address, address);
    }

    private static void Add (
        IList<string> destination,
        string name,
        string value)
    {
        destination.Add(name);
        destination.Add(value);
    }

    private static string ToIsoText (DateTimeOffset value)
    {
        return value.ToString("O", CultureInfo.InvariantCulture);
    }
}
