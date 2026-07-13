using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Common.Projection;

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
            EditorMode: ContractLiteralCodec.ToValue(session.EditorMode),
            OwnerKind: ContractLiteralCodec.ToValue(session.OwnerKind),
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: ContractLiteralCodec.ToValue(session.Endpoint.TransportKind),
            EndpointAddress: session.Endpoint.Address,
            ProcessId: session.ProcessId,
            ProcessStartedAtUtc: session.ProcessStartedAtUtc,
            OwnerProcessId: session.OwnerProcessId);
    }
}
