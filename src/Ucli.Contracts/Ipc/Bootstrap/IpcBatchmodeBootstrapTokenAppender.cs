using System.Globalization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class IpcBatchmodeBootstrapTokenAppender
{
    public static void AppendDaemon (
        IList<string> destination,
        IpcDaemonBootstrapArguments arguments)
    {
        Add(destination, IpcBatchmodeBootstrapArgumentNames.Target, ContractLiteralCodec.ToValue(IpcBootstrapTarget.Daemon));
        Add(destination, IpcDaemonBootstrapArgumentNames.RepositoryRoot, arguments.RepositoryRoot);
        Add(destination, IpcBatchmodeBootstrapArgumentNames.ProjectFingerprint, arguments.ProjectFingerprint.ToString());
        Add(destination, IpcDaemonBootstrapArgumentNames.SessionPath, arguments.SessionPath);
        Add(destination, IpcDaemonBootstrapArgumentNames.SessionGenerationId, arguments.SessionGenerationId.ToString("D"));
        Add(destination, IpcDaemonBootstrapArgumentNames.SessionIssuedAtUtc, ToIsoText(arguments.SessionIssuedAtUtc));
        AddEndpoint(destination, arguments.Endpoint);
    }

    public static void AppendOneshot (
        IList<string> destination,
        IpcOneshotBootstrapArguments arguments)
    {
        Add(destination, IpcBatchmodeBootstrapArgumentNames.Target, ContractLiteralCodec.ToValue(IpcBootstrapTarget.Oneshot));
        Add(destination, IpcOneshotBootstrapArgumentNames.BootstrapId, arguments.BootstrapId.ToString("D"));
    }

    private static void AddEndpoint (
        IList<string> destination,
        IpcEndpoint endpoint)
    {
        Add(destination, IpcEndpointBootstrapArgumentNames.TransportKind, ContractLiteralCodec.ToValue(endpoint.TransportKind));
        Add(destination, IpcEndpointBootstrapArgumentNames.Address, endpoint.Address);
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
