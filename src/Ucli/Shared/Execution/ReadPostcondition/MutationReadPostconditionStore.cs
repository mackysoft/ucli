using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Shared.Execution.ReadPostcondition;

/// <summary> Persists fingerprint-scoped mutation read-postcondition state under <c>.ucli/local</c>. </summary>
internal sealed class MutationReadPostconditionStore : IMutationReadPostconditionStore
{
    private const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new ContractLiteralJsonConverterFactory(),
        },
    };

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionReadResult> ReadOrNullAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string documentPath;
        try
        {
            documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return MutationReadPostconditionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition path is invalid. {exception.Message}"));
        }

        string? json;
        try
        {
            json = await FileUtilities.ReadAllTextOrNullAsync(documentPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return MutationReadPostconditionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition path is invalid: {documentPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return MutationReadPostconditionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to read mutation read postcondition file: {documentPath}. {exception.Message}"));
        }

        if (json == null)
        {
            return MutationReadPostconditionReadResult.Success(null);
        }

        IReadOnlyList<IpcExecuteReadPostconditionRequirement> mergedRequirements;
        try
        {
            var document = JsonSerializer.Deserialize<MutationReadPostconditionDocument>(json, SerializerOptions)
                ?? throw new JsonException("Mutation read postcondition JSON is null.");
            ValidateDocument(document, documentPath);
            mergedRequirements = MergeRequirements(document.Requirements);
        }
        catch (JsonException exception)
        {
            return MutationReadPostconditionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition is invalid: {documentPath}. {exception.Message}"));
        }
        catch (ArgumentException exception)
        {
            return MutationReadPostconditionReadResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition is invalid: {documentPath}. {exception.Message}"));
        }
        catch (Exception exception)
        {
            return MutationReadPostconditionReadResult.Failure(ExecutionError.InternalError(
                $"Failed to deserialize mutation read postcondition JSON: {documentPath}. {exception.Message}"));
        }

        return MutationReadPostconditionReadResult.Success(
            mergedRequirements.Count == 0 ? null : new IpcExecuteReadPostcondition(mergedRequirements));
    }

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionStoreOperationResult> WriteMergedAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        IpcExecuteReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(readPostcondition);

        string documentPath;
        try
        {
            documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, projectFingerprint);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return MutationReadPostconditionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition path is invalid. {exception.Message}"));
        }

        try
        {
            var mergedRequirements = MergeRequirements(readPostcondition.Requirements);
            IReadOnlyList<IpcExecuteReadPostconditionRequirement> existingRequirements = [];
            var existingReadResult = await ReadOrNullAsync(storageRoot, projectFingerprint, cancellationToken).ConfigureAwait(false);
            if (!existingReadResult.IsSuccess)
            {
                return MutationReadPostconditionStoreOperationResult.Failure(existingReadResult.Error!);
            }

            if (existingReadResult.ReadPostcondition != null)
            {
                existingRequirements = existingReadResult.ReadPostcondition.Requirements;
            }

            mergedRequirements = MergeRequirements(existingRequirements.Concat(mergedRequirements).ToArray());
            var document = new MutationReadPostconditionDocument(
                SchemaVersion,
                mergedRequirements);
            var json = JsonSerializer.Serialize(document, SerializerOptions) + Environment.NewLine;
            var directoryPath = Path.GetDirectoryName(documentPath)
                ?? throw new InvalidOperationException($"Mutation read postcondition directory path could not be resolved: {documentPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
            await FileUtilities.WriteAllTextAtomicallyAsync(documentPath, json, cancellationToken).ConfigureAwait(false);
            return MutationReadPostconditionStoreOperationResult.Success();
        }
        catch (ArgumentException exception)
        {
            return MutationReadPostconditionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition is invalid: {documentPath}. {exception.Message}"));
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return MutationReadPostconditionStoreOperationResult.Failure(ExecutionError.InvalidArgument(
                $"Mutation read postcondition path is invalid: {documentPath}. {exception.Message}"));
        }
        catch (Exception exception) when (IsIoFailure(exception))
        {
            return MutationReadPostconditionStoreOperationResult.Failure(ExecutionError.InternalError(
                $"Failed to write mutation read postcondition file: {documentPath}. {exception.Message}"));
        }
    }

    private static IReadOnlyList<IpcExecuteReadPostconditionRequirement> MergeRequirements (
        IReadOnlyList<IpcExecuteReadPostconditionRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        var merged = new Dictionary<(IpcExecuteReadPostconditionSurface Surface, UnityScenePath? ScenePath), IpcExecuteReadPostconditionRequirement>();
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = requirements[i];
            ArgumentNullException.ThrowIfNull(requirement);
            var key = GetRequirementKey(requirement);
            if (merged.TryGetValue(key, out var existing)
                && existing.MinSafeGeneratedAtUtc >= requirement.MinSafeGeneratedAtUtc)
            {
                continue;
            }

            merged[key] = requirement;
        }

        return merged
            .OrderBy(static pair => pair.Key.Surface)
            .ThenBy(static pair => pair.Key.ScenePath?.Value, StringComparer.Ordinal)
            .Select(static pair => pair.Value)
            .ToArray();
    }

    private static void ValidateDocument (
        MutationReadPostconditionDocument document,
        string documentPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        if (document.SchemaVersion != SchemaVersion)
        {
            throw new ArgumentOutOfRangeException(nameof(document), document.SchemaVersion, $"schemaVersion must be {SchemaVersion}. {documentPath}");
        }

        ArgumentNullException.ThrowIfNull(document.Requirements);
    }

    private static (IpcExecuteReadPostconditionSurface Surface, UnityScenePath? ScenePath) GetRequirementKey (
        IpcExecuteReadPostconditionRequirement requirement)
    {
        return (requirement.Surface, requirement.ScenePath);
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
