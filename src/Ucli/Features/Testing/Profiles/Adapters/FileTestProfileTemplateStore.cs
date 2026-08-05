using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Profiles;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Ports;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Features.Testing.Profiles.Adapters;

/// <summary> Persists profile template JSON files through the local filesystem. </summary>
internal sealed class FileTestProfileTemplateStore : ITestProfileTemplateStore
{
    private const string DefaultOutputPath = "test.profile.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <inheritdoc />
    public async ValueTask<TestProfileInitExecutionResult> WriteAsync (
        TestProfile profile,
        AbsolutePath? outputPath,
        bool force,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedOutputPath = outputPath ?? AbsolutePath.Resolve(
            AbsolutePath.Parse(Environment.CurrentDirectory),
            DefaultOutputPath);
        if (Directory.Exists(resolvedOutputPath.Value))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path must be a file path, but a directory exists: {resolvedOutputPath}"));
        }

        if (File.Exists(resolvedOutputPath.Value) && !force)
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path already exists: {resolvedOutputPath}. Use --force to overwrite."));
        }

        if (!resolvedOutputPath.TryGetParent(out var parentDirectoryPath))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to resolve parent directory from output path: {resolvedOutputPath}"));
        }

        if (File.Exists(parentDirectoryPath.Value))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output directory path points to a file: {parentDirectoryPath}"));
        }

        try
        {
            Directory.CreateDirectory(parentDirectoryPath.Value);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare output directory: {parentDirectoryPath}. {ex.Message}"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var templateJson = JsonSerializer.Serialize(profile, SerializerOptions);
        try
        {
            await File.WriteAllTextAsync(resolvedOutputPath.Value, templateJson + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to write profile template file: {resolvedOutputPath}. {ex.Message}"));
        }

        return TestProfileInitExecutionResult.Success(new TestProfileInitExecutionOutput(resolvedOutputPath.Value));
    }

    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException;
    }
}
