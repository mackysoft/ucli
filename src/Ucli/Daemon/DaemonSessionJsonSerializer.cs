using System.Text.Json;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Daemon;

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