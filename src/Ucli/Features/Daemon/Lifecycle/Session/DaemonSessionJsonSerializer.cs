using System.Text.Json;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Session;

/// <summary> Implements JSON conversion for daemon session persistence models. </summary>
internal sealed class DaemonSessionJsonSerializer : IDaemonSessionSerializer
{
    /// <summary> Deserializes daemon session JSON text to model. </summary>
    /// <param name="json"> The daemon session JSON text. </param>
    /// <returns> The deserialized daemon session model. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is empty. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public DaemonSession Deserialize (string json)
    {
        var contract = DaemonSessionJsonContractSerializer.Deserialize(json)
            ?? throw new JsonException("Daemon session JSON is null.");

        return new DaemonSession(
            SchemaVersion: contract.SchemaVersion,
            SessionToken: StringValueNormalizer.TrimOrEmpty(contract.SessionToken),
            ProjectFingerprint: StringValueNormalizer.TrimOrEmpty(contract.ProjectFingerprint),
            IssuedAtUtc: contract.IssuedAtUtc,
            RuntimeKind: StringValueNormalizer.TrimOrEmpty(contract.RuntimeKind),
            OwnerKind: StringValueNormalizer.TrimOrEmpty(contract.OwnerKind),
            CanShutdownProcess: contract.CanShutdownProcess,
            EndpointTransportKind: StringValueNormalizer.TrimOrEmpty(contract.EndpointTransportKind),
            EndpointAddress: StringValueNormalizer.TrimOrEmpty(contract.EndpointAddress),
            ProcessId: contract.ProcessId,
            OwnerProcessId: contract.OwnerProcessId);
    }

    /// <summary> Serializes daemon session model to JSON text. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <returns> The serialized daemon session JSON text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public string Serialize (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var contract = new DaemonSessionJsonContract(
            SchemaVersion: session.SchemaVersion,
            SessionToken: session.SessionToken,
            ProjectFingerprint: session.ProjectFingerprint,
            IssuedAtUtc: session.IssuedAtUtc,
            RuntimeKind: session.RuntimeKind,
            OwnerKind: session.OwnerKind,
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: session.EndpointTransportKind,
            EndpointAddress: session.EndpointAddress,
            ProcessId: session.ProcessId,
            OwnerProcessId: session.OwnerProcessId);

        return DaemonSessionJsonContractSerializer.Serialize(contract);
    }
}