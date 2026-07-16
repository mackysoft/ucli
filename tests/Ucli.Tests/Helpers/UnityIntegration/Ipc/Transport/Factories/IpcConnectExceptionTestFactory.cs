using System.Net.Sockets;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal static class IpcConnectExceptionTestFactory
{
    public static IpcConnectException FromSocketError (SocketError socketError)
    {
        return new IpcConnectException(
            "IPC connection failed before the request was sent.",
            new SocketException((int)socketError));
    }
}
