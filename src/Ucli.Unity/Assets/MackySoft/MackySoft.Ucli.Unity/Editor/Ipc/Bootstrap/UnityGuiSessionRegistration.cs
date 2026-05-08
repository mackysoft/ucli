using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents a persisted GUI daemon session registration. </summary>
    internal sealed record UnityGuiSessionRegistration (
        string StorageRoot,
        string ProjectFingerprint,
        string SessionPath,
        string SessionToken,
        DateTimeOffset IssuedAtUtc,
        IpcEndpoint Endpoint,
        bool CanShutdownProcess);
}
