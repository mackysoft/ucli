using System.Linq;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
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
    };

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionReadResult> ReadOrNull (
        string storageRoot,
        string projectFingerprint,
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
            json = await FileUtilities.ReadAllTextOrNull(documentPath, cancellationToken).ConfigureAwait(false);
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

        MutationReadPostconditionDocument document;
        try
        {
            document = JsonSerializer.Deserialize<MutationReadPostconditionDocument>(json, SerializerOptions)
                ?? throw new JsonException("Mutation read postcondition JSON is null.");
            ValidateDocument(document, documentPath);
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

        var mergedRequirements = MergeRequirements(document.Requirements);
        return MutationReadPostconditionReadResult.Success(
            mergedRequirements.Count == 0 ? null : MapToApplicationReadPostcondition(mergedRequirements));
    }

    /// <inheritdoc />
    public async ValueTask<MutationReadPostconditionStoreOperationResult> WriteMerged (
        string storageRoot,
        string projectFingerprint,
        OperationExecutionReadPostcondition readPostcondition,
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
            var mergedRequirements = MergeRequirements(MapToIpcRequirements(readPostcondition.Requirements));
            IReadOnlyList<IpcExecuteReadPostconditionRequirement> existingRequirements = [];
            var existingReadResult = await ReadOrNull(storageRoot, projectFingerprint, cancellationToken).ConfigureAwait(false);
            if (!existingReadResult.IsSuccess)
            {
                return MutationReadPostconditionStoreOperationResult.Failure(existingReadResult.Error!);
            }

            if (existingReadResult.ReadPostcondition != null)
            {
                existingRequirements = MapToIpcRequirements(existingReadResult.ReadPostcondition.Requirements);
            }

            mergedRequirements = MergeRequirements(existingRequirements.Concat(mergedRequirements).ToArray());
            var document = new MutationReadPostconditionDocument(
                SchemaVersion,
                mergedRequirements);
            var json = JsonSerializer.Serialize(document, SerializerOptions) + Environment.NewLine;
            var directoryPath = Path.GetDirectoryName(documentPath)
                ?? throw new InvalidOperationException($"Mutation read postcondition directory path could not be resolved: {documentPath}");
            FileSystemAccessBoundary.EnsureSecureDirectory(directoryPath);
            await FileUtilities.WriteAllTextAtomically(documentPath, json, cancellationToken).ConfigureAwait(false);
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

        var merged = new SortedDictionary<string, IpcExecuteReadPostconditionRequirement>(StringComparer.Ordinal);
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = NormalizeAndValidateRequirement(requirements[i]);
            var key = GetRequirementKey(requirement);
            if (merged.TryGetValue(key, out var existing)
                && existing.MinSafeGeneratedAtUtc >= requirement.MinSafeGeneratedAtUtc)
            {
                continue;
            }

            merged[key] = requirement;
        }

        return merged.Values.ToArray();
    }

    private static IpcExecuteReadPostconditionRequirement NormalizeAndValidateRequirement (
        IpcExecuteReadPostconditionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentException.ThrowIfNullOrWhiteSpace(requirement.Surface);
        if (requirement.MinSafeGeneratedAtUtc == default)
        {
            throw new ArgumentException("minSafeGeneratedAtUtc must not be default.", nameof(requirement));
        }

        switch (requirement.Surface)
        {
            case IpcExecuteReadPostconditionSurfaceNames.AssetSearch:
            case IpcExecuteReadPostconditionSurfaceNames.GuidPath:
                if (requirement.ScenePath != null)
                {
                    throw new ArgumentException("scenePath must be omitted for project-scoped read postconditions.", nameof(requirement));
                }

                return requirement with { ScenePath = null };

            case IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite:
                if (string.IsNullOrWhiteSpace(requirement.ScenePath))
                {
                    throw new ArgumentException("scenePath is required for scene-tree-lite read postconditions.", nameof(requirement));
                }

                return requirement with
                {
                    ScenePath = PathStringNormalizer.ToSlashSeparated(requirement.ScenePath),
                };

            default:
                throw new ArgumentException($"Unsupported read postcondition surface '{requirement.Surface}'.", nameof(requirement));
        }
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
        for (var i = 0; i < document.Requirements.Count; i++)
        {
            _ = NormalizeAndValidateRequirement(document.Requirements[i]);
        }
    }

    private static string GetRequirementKey (IpcExecuteReadPostconditionRequirement requirement)
    {
        return requirement.Surface + "\u001f" + requirement.ScenePath;
    }

    private static OperationExecutionReadPostcondition MapToApplicationReadPostcondition (
        IReadOnlyList<IpcExecuteReadPostconditionRequirement> requirements)
    {
        var mappedRequirements = new OperationExecutionReadPostconditionRequirement[requirements.Count];
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = requirements[i];
            mappedRequirements[i] = new OperationExecutionReadPostconditionRequirement(
                requirement.Surface,
                requirement.MinSafeGeneratedAtUtc)
            {
                ScenePath = requirement.ScenePath,
            };
        }

        return new OperationExecutionReadPostcondition(mappedRequirements);
    }

    private static IReadOnlyList<IpcExecuteReadPostconditionRequirement> MapToIpcRequirements (
        IReadOnlyList<OperationExecutionReadPostconditionRequirement> requirements)
    {
        var mappedRequirements = new IpcExecuteReadPostconditionRequirement[requirements.Count];
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = requirements[i];
            mappedRequirements[i] = new IpcExecuteReadPostconditionRequirement(
                requirement.Surface,
                requirement.MinSafeGeneratedAtUtc)
            {
                ScenePath = requirement.ScenePath,
            };
        }

        return mappedRequirements;
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
