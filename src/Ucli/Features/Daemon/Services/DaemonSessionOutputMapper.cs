namespace MackySoft.Ucli.Features.Daemon.Services;

/// <summary> Implements conversion from daemon session domain model to daemon command session payload model. </summary>
internal sealed class DaemonSessionOutputMapper : IDaemonSessionOutputMapper
{
    /// <summary> Converts one daemon session domain model to daemon command session payload model. </summary>
    /// <param name="session"> The daemon session domain model. </param>
    /// <returns> The daemon command session payload model. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public DaemonSessionOutput ToOutput (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new DaemonSessionOutput(
            ProjectFingerprint: session.ProjectFingerprint,
            IssuedAtUtc: session.IssuedAtUtc,
            RuntimeKind: session.RuntimeKind,
            OwnerKind: session.OwnerKind,
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: session.EndpointTransportKind,
            EndpointAddress: session.EndpointAddress,
            ProcessId: session.ProcessId,
            OwnerProcessId: session.OwnerProcessId);
    }
}