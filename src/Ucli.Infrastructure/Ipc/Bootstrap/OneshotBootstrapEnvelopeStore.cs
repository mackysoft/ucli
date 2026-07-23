using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Persists secret-bearing oneshot bootstrap generations behind non-secret GUID references. </summary>
internal static class OneshotBootstrapEnvelopeStore
{
    private const int MaximumEnvelopeBytes = 16 * 1024;

    private const int MaximumMaintenanceFiles = 128;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions DeserializeOptions = CreateSerializerOptions(writeIndented: false);

    private static readonly JsonSerializerOptions SerializeOptions = CreateSerializerOptions(writeIndented: true);

    /// <summary> Creates one envelope file without replacing an existing generation. </summary>
    public static void Create (
        AbsolutePath storageRoot,
        IpcOneshotBootstrapEnvelope envelope)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }
        var directoryPath = UcliStoragePathResolver.ResolveOneshotBootstrapDirectory(
            storageRoot,
            envelope.ProjectFingerprint);
        var envelopePath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            envelope.ProjectFingerprint,
            envelope.BootstrapId);
        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        CleanupExpiredCore(directoryPath, envelope.ProjectFingerprint, DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(ToContract(envelope), SerializeOptions) + Environment.NewLine;
        var payload = StrictUtf8.GetBytes(json);
        if (payload.Length > MaximumEnvelopeBytes)
        {
            throw new InvalidOperationException("Oneshot bootstrap envelope exceeds its storage limit.");
        }

        AbsolutePath? temporaryPath = null;
        try
        {
            using (var stream = FileUtilities.OpenAtomicWriteTemporaryFileInDirectory(
                       directoryPath,
                       out temporaryPath))
            {
                stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }

            FileSystemAccessBoundary.EnsureSecureFile(temporaryPath);
            // NOTE: The same-directory move preserves the secured file node and is deliberately the final operation.
            // A failure must not be reported after the secret-bearing envelope has become publicly addressable.
            File.Move(temporaryPath.Value, envelopePath.Value);
        }
        catch
        {
            if (temporaryPath is not null)
            {
                TryDeleteFile(temporaryPath);
            }

            throw;
        }
    }

    /// <summary> Reads and validates the generation referenced by one bootstrap identifier. </summary>
    public static IpcOneshotBootstrapEnvelope Read (
        AbsolutePath storageRoot,
        ProjectFingerprint expectedProjectFingerprint,
        Guid bootstrapId,
        DateTimeOffset nowUtc)
    {
        if (expectedProjectFingerprint == null)
        {
            throw new ArgumentNullException(nameof(expectedProjectFingerprint));
        }
        RequireUtcTimestamp(nowUtc, nameof(nowUtc));
        var directoryPath = UcliStoragePathResolver.ResolveOneshotBootstrapDirectory(
            storageRoot,
            expectedProjectFingerprint);
        var envelopePath = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            expectedProjectFingerprint,
            bootstrapId);
        if (!Directory.Exists(directoryPath.Value))
        {
            throw new FileNotFoundException(
                "Oneshot bootstrap envelope directory was not found.",
                envelopePath.Value);
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
        var envelope = ReadEnvelope(envelopePath);
        if (envelope.BootstrapId != bootstrapId)
        {
            throw new InvalidDataException("Oneshot bootstrap envelope identifier does not match its storage reference.");
        }

        if (envelope.ProjectFingerprint != expectedProjectFingerprint)
        {
            throw new InvalidDataException("Oneshot bootstrap envelope project fingerprint does not match the current project.");
        }

        if (envelope.CreatedAtUtc > nowUtc)
        {
            throw new InvalidDataException("Oneshot bootstrap envelope creation time is in the future.");
        }

        if (envelope.ExitDeadlineUtc <= nowUtc)
        {
            throw new InvalidDataException("Oneshot bootstrap envelope has expired.");
        }

        var expectedEndpoint = UcliIpcEndpointResolver.ResolveDaemonEndpoint(
            storageRoot,
            expectedProjectFingerprint);
        if (envelope.Endpoint != expectedEndpoint.Contract)
        {
            throw new InvalidDataException("Oneshot bootstrap envelope endpoint does not match the current project endpoint.");
        }

        if (!ProcessLivenessProbe.IsSameProcess(envelope.ParentProcess))
        {
            throw new InvalidDataException("Oneshot bootstrap parent process generation is no longer alive.");
        }

        return envelope;
    }

    /// <summary> Deletes a file only while its complete immutable generation still matches the expected owner. </summary>
    public static bool TryDeleteIfOwned (
        AbsolutePath storageRoot,
        IpcOneshotBootstrapEnvelope expectedEnvelope)
    {
        if (expectedEnvelope == null)
        {
            throw new ArgumentNullException(nameof(expectedEnvelope));
        }
        var path = UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            expectedEnvelope.ProjectFingerprint,
            expectedEnvelope.BootstrapId);
        try
        {
            if (!File.Exists(path.Value))
            {
                return false;
            }

            var actualEnvelope = ReadEnvelope(path);
            if (actualEnvelope != expectedEnvelope)
            {
                return false;
            }

            File.Delete(path.Value);
            TryDeleteEmptyDirectory(
                UcliStoragePathResolver.ResolveOneshotBootstrapDirectory(
                    storageRoot,
                    expectedEnvelope.ProjectFingerprint));
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IpcOneshotBootstrapEnvelope ReadEnvelope (AbsolutePath path)
    {
        var attributes = File.GetAttributes(path.Value);
        if (!FileSystemNodeClassifier.IsRegularFile(path, attributes))
        {
            throw new InvalidDataException("Oneshot bootstrap envelope must be a regular file.");
        }

        string json;
        using (var stream = new FileStream(
                   path.Value,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            if (stream.Length <= 0 || stream.Length > MaximumEnvelopeBytes)
            {
                throw new InvalidDataException("Oneshot bootstrap envelope size is invalid.");
            }

            using var reader = new StreamReader(
                stream,
                StrictUtf8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: false);
            try
            {
                json = reader.ReadToEnd();
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException(
                    "Oneshot bootstrap envelope must contain valid UTF-8 text.",
                    exception);
            }
        }

        try
        {
            var contract = JsonSerializer.Deserialize<EnvelopeContract>(json, DeserializeOptions)
                ?? throw new InvalidDataException("Oneshot bootstrap envelope JSON root must not be null.");
            return FromContract(contract);
        }
        catch (Exception exception) when (exception is JsonException
            or ArgumentException
            or NotSupportedException)
        {
            throw new InvalidDataException("Oneshot bootstrap envelope contract is invalid.", exception);
        }
    }

    private static IpcOneshotBootstrapEnvelope FromContract (EnvelopeContract contract)
    {
        if (contract.ParentProcess is null
            || contract.ProjectFingerprint is null
            || !IpcSessionToken.TryParse(contract.SessionToken, out var sessionToken)
            || contract.EndpointTransportKind is null
            || string.IsNullOrWhiteSpace(contract.EndpointAddress))
        {
            throw new InvalidDataException("Oneshot bootstrap envelope is missing a required value.");
        }

        return new IpcOneshotBootstrapEnvelope(
            contract.BootstrapId,
            contract.ParentProcess,
            contract.ProjectFingerprint,
            sessionToken,
            contract.CreatedAtUtc,
            contract.ExitDeadlineUtc,
            new IpcEndpoint(contract.EndpointTransportKind.Value, contract.EndpointAddress));
    }

    private static EnvelopeContract ToContract (IpcOneshotBootstrapEnvelope envelope)
    {
        return new EnvelopeContract(
            envelope.BootstrapId,
            envelope.ParentProcess,
            envelope.ProjectFingerprint,
            envelope.SessionToken.GetEncodedValue(),
            envelope.CreatedAtUtc,
            envelope.ExitDeadlineUtc,
            envelope.Endpoint.TransportKind,
            envelope.Endpoint.Address);
    }

    private static void CleanupExpiredCore (
        AbsolutePath directoryPath,
        ProjectFingerprint projectFingerprint,
        DateTimeOffset nowUtc)
    {
        foreach (var path in Directory.EnumerateFiles(
                     directoryPath.Value,
                     "*" + UcliStoragePathNames.OneshotBootstrapFileExtension,
                     SearchOption.TopDirectoryOnly)
                 .Take(MaximumMaintenanceFiles))
        {
            var envelopePath = AbsolutePath.Parse(path);
            if (!TryGetOwnedBootstrapId(envelopePath, out var bootstrapId))
            {
                continue;
            }

            try
            {
                var envelope = ReadEnvelope(envelopePath);
                if (envelope.BootstrapId == bootstrapId
                    && envelope.ProjectFingerprint == projectFingerprint
                    && (envelope.ExitDeadlineUtc <= nowUtc
                    || !ProcessLivenessProbe.IsSameProcess(envelope.ParentProcess)))
                {
                    FileUtilities.EnsureRegularFile(envelopePath, "Oneshot bootstrap envelope");
                    File.Delete(envelopePath.Value);
                }
            }
            catch (Exception exception) when (exception is InvalidDataException
                or IOException
                or UnauthorizedAccessException)
            {
            }
        }
    }

    private static bool TryGetOwnedBootstrapId (
        AbsolutePath path,
        out Guid bootstrapId)
    {
        if (!string.Equals(
                Path.GetExtension(path.Value),
                UcliStoragePathNames.OneshotBootstrapFileExtension,
                StringComparison.Ordinal))
        {
            bootstrapId = Guid.Empty;
            return false;
        }

        return StoragePathSegmentCodec.TryDecodeNonEmptyGuid(
            Path.GetFileNameWithoutExtension(path.Value),
            out bootstrapId);
    }

    private static JsonSerializerOptions CreateSerializerOptions (bool writeIndented)
    {
        return new JsonSerializerOptions
        {
            Converters =
            {
                new ContractLiteralJsonConverterFactory(),
            },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            WriteIndented = writeIndented,
        };
    }

    private static void RequireUtcTimestamp (
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be a non-default UTC value.", parameterName);
        }
    }

    private static void TryDeleteFile (AbsolutePath path)
    {
        try
        {
            File.Delete(path.Value);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteEmptyDirectory (AbsolutePath directoryPath)
    {
        try
        {
            Directory.Delete(directoryPath.Value, recursive: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record EnvelopeContract (
        Guid BootstrapId,
        ProcessIdentity? ParentProcess,
        ProjectFingerprint? ProjectFingerprint,
        string? SessionToken,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset ExitDeadlineUtc,
        IpcTransportKind? EndpointTransportKind,
        string? EndpointAddress);
}
