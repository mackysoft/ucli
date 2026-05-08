using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a persisted GUI daemon session registration. </summary>
    internal sealed record UnityGuiSessionRegistration (
        string SessionPath,
        DateTimeOffset IssuedAtUtc,
        IpcEndpoint Endpoint,
        bool CanShutdownProcess);
}
