using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Carries the two explicit Program authorization inputs and their canonical digest. </summary>
public sealed record IpcProgramEffectiveAuthorizationSnapshot
{
    private static readonly JsonWriterOptions DigestWriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    [JsonConstructor]
    public IpcProgramEffectiveAuthorizationSnapshot (bool allowDangerous, bool allowPlayMode, Sha256Digest digest)
    {
        AllowDangerous = allowDangerous;
        AllowPlayMode = allowPlayMode;
        Digest = digest ?? throw new ArgumentNullException(nameof(digest));
        if (Digest != ComputeDigest(allowDangerous, allowPlayMode))
        {
            throw new ArgumentException("Authorization snapshot digest does not match its effective permissions.", nameof(digest));
        }
    }

    [JsonInclude]
    [JsonRequired]
    public bool AllowDangerous { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public bool AllowPlayMode { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public Sha256Digest Digest { get; private init; }

    public static Sha256Digest ComputeDigest (bool allowDangerous, bool allowPlayMode)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, DigestWriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("allowDangerous", allowDangerous);
            writer.WriteBoolean("allowPlayMode", allowPlayMode);
            writer.WriteEndObject();
        }

        return Sha256Digest.Compute(buffer.WrittenSpan);
    }
}

/// <summary> Carries every effective configuration value that can influence a Program Request. </summary>
public sealed record IpcProgramEffectiveConfigurationSnapshot
{
    private static readonly JsonWriterOptions DigestWriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    [JsonConstructor]
    public IpcProgramEffectiveConfigurationSnapshot (
        int schemaVersion,
        string operationPolicy,
        string planTokenMode,
        string readIndexDefaultMode,
        IReadOnlyList<string> operationAllowlist,
        int ipcDefaultTimeoutMilliseconds,
        IReadOnlyDictionary<string, int> ipcTimeoutMillisecondsByCommand,
        Sha256Digest digest)
    {
        if (schemaVersion < 1 || string.IsNullOrWhiteSpace(operationPolicy) || string.IsNullOrWhiteSpace(planTokenMode)
            || string.IsNullOrWhiteSpace(readIndexDefaultMode) || operationAllowlist is null || ipcDefaultTimeoutMilliseconds < 1
            || ipcTimeoutMillisecondsByCommand is null || digest is null
            || operationAllowlist.Any(string.IsNullOrWhiteSpace)
            || ipcTimeoutMillisecondsByCommand.Any(static entry => string.IsNullOrWhiteSpace(entry.Key) || entry.Value < 1))
        {
            throw new ArgumentException("Program configuration snapshot must contain complete effective values.");
        }

        SchemaVersion = schemaVersion;
        OperationPolicy = operationPolicy;
        PlanTokenMode = planTokenMode;
        ReadIndexDefaultMode = readIndexDefaultMode;
        OperationAllowlist = Array.AsReadOnly(operationAllowlist.ToArray());
        IpcDefaultTimeoutMilliseconds = ipcDefaultTimeoutMilliseconds;
        IpcTimeoutMillisecondsByCommand = new Dictionary<string, int>(ipcTimeoutMillisecondsByCommand, StringComparer.Ordinal);
        Digest = digest;
        if (Digest != ComputeDigest(
                SchemaVersion, OperationPolicy, PlanTokenMode, ReadIndexDefaultMode, OperationAllowlist,
                IpcDefaultTimeoutMilliseconds, IpcTimeoutMillisecondsByCommand))
        {
            throw new ArgumentException("Program configuration snapshot digest does not match its effective values.", nameof(digest));
        }
    }

    [JsonInclude]
    [JsonRequired]
    public int SchemaVersion { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public string OperationPolicy { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public string PlanTokenMode { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public string ReadIndexDefaultMode { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<string> OperationAllowlist { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public int IpcDefaultTimeoutMilliseconds { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public IReadOnlyDictionary<string, int> IpcTimeoutMillisecondsByCommand { get; private init; }
    [JsonInclude]
    [JsonRequired]
    public Sha256Digest Digest { get; private init; }

    public static Sha256Digest ComputeDigest (
        int schemaVersion,
        string operationPolicy,
        string planTokenMode,
        string readIndexDefaultMode,
        IReadOnlyList<string> operationAllowlist,
        int ipcDefaultTimeoutMilliseconds,
        IReadOnlyDictionary<string, int> ipcTimeoutMillisecondsByCommand)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, DigestWriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("ipcDefaultTimeoutMilliseconds", ipcDefaultTimeoutMilliseconds);
            writer.WritePropertyName("ipcTimeoutMillisecondsByCommand");
            writer.WriteStartObject();
            foreach (var entry in ipcTimeoutMillisecondsByCommand.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WriteNumber(entry.Key, entry.Value);
            }
            writer.WriteEndObject();
            writer.WritePropertyName("operationAllowlist");
            writer.WriteStartArray();
            foreach (var entry in operationAllowlist)
            {
                writer.WriteStringValue(entry);
            }
            writer.WriteEndArray();
            writer.WriteString("operationPolicy", operationPolicy);
            writer.WriteString("planTokenMode", planTokenMode);
            writer.WriteString("readIndexDefaultMode", readIndexDefaultMode);
            writer.WriteNumber("schemaVersion", schemaVersion);
            writer.WriteEndObject();
        }

        return Sha256Digest.Compute(buffer.WrittenSpan);
    }
}
