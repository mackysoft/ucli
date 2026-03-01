using System.Text.Json;

namespace MackySoft.Ucli.Daemon;

/// <summary> Implements JSON conversion for daemon session persistence models. </summary>
internal sealed class DaemonSessionJsonSerializer : IDaemonSessionSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary> Deserializes daemon session JSON text to model. </summary>
    /// <param name="json"> The daemon session JSON text. </param>
    /// <returns> The deserialized daemon session model. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is empty. </exception>
    /// <exception cref="JsonException"> Thrown when JSON schema is invalid. </exception>
    public DaemonSession Deserialize (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var document = JsonSerializer.Deserialize<DaemonSessionDocument>(json, SerializerOptions)
            ?? throw new JsonException("Daemon session JSON is null.");

        return new DaemonSession(
            SchemaVersion: document.SchemaVersion,
            SessionToken: document.SessionToken?.Trim() ?? string.Empty,
            ProjectFingerprint: document.ProjectFingerprint?.Trim() ?? string.Empty,
            IssuedAtUtc: document.IssuedAtUtc,
            RuntimeKind: document.RuntimeKind?.Trim() ?? string.Empty,
            OwnerKind: document.OwnerKind?.Trim() ?? string.Empty,
            CanShutdownProcess: document.CanShutdownProcess,
            EndpointTransportKind: document.EndpointTransportKind?.Trim() ?? string.Empty,
            EndpointAddress: document.EndpointAddress?.Trim() ?? string.Empty,
            ProcessId: document.ProcessId);
    }

    /// <summary> Serializes daemon session model to JSON text. </summary>
    /// <param name="session"> The daemon session model. </param>
    /// <returns> The serialized daemon session JSON text. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="session" /> is <see langword="null" />. </exception>
    public string Serialize (DaemonSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var document = new DaemonSessionDocument(
            SchemaVersion: session.SchemaVersion,
            SessionToken: session.SessionToken,
            ProjectFingerprint: session.ProjectFingerprint,
            IssuedAtUtc: session.IssuedAtUtc,
            RuntimeKind: session.RuntimeKind,
            OwnerKind: session.OwnerKind,
            CanShutdownProcess: session.CanShutdownProcess,
            EndpointTransportKind: session.EndpointTransportKind,
            EndpointAddress: session.EndpointAddress,
            ProcessId: session.ProcessId);

        return JsonSerializer.Serialize(document, SerializerOptions);
    }

    /// <summary> Represents persisted daemon session JSON shape. </summary>
    /// <param name="SchemaVersion"> The schema version value. </param>
    /// <param name="SessionToken"> The daemon session token. </param>
    /// <param name="ProjectFingerprint"> The project fingerprint value. </param>
    /// <param name="IssuedAtUtc"> The issued-at timestamp. </param>
    /// <param name="RuntimeKind"> The daemon runtime kind value. </param>
    /// <param name="OwnerKind"> The daemon owner kind value. </param>
    /// <param name="CanShutdownProcess"> Whether daemon process shutdown is allowed. </param>
    /// <param name="EndpointTransportKind"> The endpoint transport kind value. </param>
    /// <param name="EndpointAddress"> The endpoint address value. </param>
    /// <param name="ProcessId"> The process identifier value. </param>
    private sealed record DaemonSessionDocument (
        int SchemaVersion,
        string? SessionToken,
        string? ProjectFingerprint,
        DateTimeOffset IssuedAtUtc,
        string? RuntimeKind,
        string? OwnerKind,
        bool CanShutdownProcess,
        string? EndpointTransportKind,
        string? EndpointAddress,
        int? ProcessId);
}