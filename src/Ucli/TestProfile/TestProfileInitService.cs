using System.Text.Json;
using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.TestProfile;

/// <summary> Implements profile initialization flow that generates test profile template JSON files. </summary>
internal sealed class TestProfileInitService : ITestProfileInitService
{
    private const string DefaultOutputPath = "test.profile.json";
    private const string JsonExtension = ".json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary> Creates or overwrites a test profile template JSON file. </summary>
    /// <param name="outputPath"> The optional output path value from CLI input. </param>
    /// <param name="force"> Whether existing files can be overwritten. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the profile-init execution result that contains generated output on success or a structured error on failure. </returns>
    public async ValueTask<TestProfileInitExecutionResult> Execute (
        string? outputPath,
        bool force,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outputPathResolution = ResolveOutputPath(outputPath);
        if (!outputPathResolution.IsSuccess)
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(outputPathResolution.ErrorMessage!));
        }

        var resolvedOutputPath = outputPathResolution.OutputPath!;
        if (Directory.Exists(resolvedOutputPath))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path must be a file path, but a directory exists: {resolvedOutputPath}"));
        }

        if (File.Exists(resolvedOutputPath) && !force)
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path already exists: {resolvedOutputPath}. Use --force to overwrite."));
        }

        string? parentDirectoryPath;
        try
        {
            parentDirectoryPath = Path.GetDirectoryName(resolvedOutputPath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path is invalid: {resolvedOutputPath}. {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(parentDirectoryPath))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to resolve parent directory from output path: {resolvedOutputPath}"));
        }

        if (File.Exists(parentDirectoryPath))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output directory path points to a file: {parentDirectoryPath}"));
        }

        try
        {
            Directory.CreateDirectory(parentDirectoryPath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path is invalid: {resolvedOutputPath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to prepare output directory: {parentDirectoryPath}. {ex.Message}"));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var templateJson = JsonSerializer.Serialize(TestProfile.CreateDefault(), SerializerOptions);
        try
        {
            await File.WriteAllTextAsync(
                    resolvedOutputPath,
                    templateJson + Environment.NewLine,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument(
                $"Output path is invalid: {resolvedOutputPath}. {ex.Message}"));
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            return TestProfileInitExecutionResult.Failure(ExecutionError.InternalError(
                $"Failed to write profile template file: {resolvedOutputPath}. {ex.Message}"));
        }

        var output = new TestProfileInitExecutionOutput(resolvedOutputPath);
        return TestProfileInitExecutionResult.Success(output);
    }

    /// <summary> Resolves CLI input into an absolute output path. </summary>
    /// <param name="outputPath"> The optional output path value from CLI input. </param>
    /// <returns> A successful absolute output path, or an invalid-input error message. </returns>
    private static OutputPathResolution ResolveOutputPath (string? outputPath)
    {
        var pathValueResolution = ResolveOutputPathValue(outputPath);
        if (!pathValueResolution.IsSuccess)
        {
            return OutputPathResolution.Failure(pathValueResolution.ErrorMessage!);
        }

        try
        {
            var fullPath = Path.GetFullPath(pathValueResolution.PathValue!);
            return OutputPathResolution.Success(fullPath);
        }
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return OutputPathResolution.Failure($"Output path is invalid: {ex.Message}");
        }
    }

    /// <summary> Resolves and normalizes the raw output path value from CLI input. </summary>
    /// <param name="outputPath"> The optional output path value from CLI input. </param>
    /// <returns> A normalized path value, or an invalid-input error message. </returns>
    private static PathValueResolution ResolveOutputPathValue (string? outputPath)
    {
        var normalizedPath = StringValueNormalizer.TrimToNull(outputPath);
        if (normalizedPath is not null && IsDirectoryPathDefinition(normalizedPath))
        {
            return PathValueResolution.Failure(
                $"Output path must be a file path. Directory-style path is not allowed: {normalizedPath}");
        }

        var pathWithExtension = EnsureJsonExtension(normalizedPath ?? DefaultOutputPath);
        return PathValueResolution.Success(pathWithExtension);
    }

    /// <summary> Ensures a path ends with <c>.json</c>. </summary>
    /// <param name="pathValue"> The path value to normalize. </param>
    /// <returns> The original path when it already ends with <c>.json</c>; otherwise the path with <c>.json</c> appended. </returns>
    private static string EnsureJsonExtension (string pathValue)
    {
        ArgumentNullException.ThrowIfNull(pathValue);
        return pathValue.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase)
            ? pathValue
            : pathValue + JsonExtension;
    }

    /// <summary> Determines whether the path uses a directory-style suffix. </summary>
    /// <param name="pathValue"> The raw path value from CLI input. </param>
    /// <returns> <see langword="true" /> when the path ends with <c>/</c> or <c>\</c>; otherwise <see langword="false" />. </returns>
    private static bool IsDirectoryPathDefinition (string pathValue)
    {
        ArgumentNullException.ThrowIfNull(pathValue);

        if (Path.EndsInDirectorySeparator(pathValue))
        {
            return true;
        }

        // NOTE: Keep rejecting Windows-style trailing separator on non-Windows runtimes.
        return pathValue.EndsWith("\\", StringComparison.Ordinal);
    }

    /// <summary> Determines whether an exception indicates a filesystem I/O failure. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when it is an I/O failure; otherwise <see langword="false" />. </returns>
    private static bool IsIoFailure (Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException;
    }

    /// <summary> Represents output-path resolution result. </summary>
    /// <param name="OutputPath"> The resolved absolute output path when successful. </param>
    /// <param name="ErrorMessage"> The error message when resolution failed. </param>
    private sealed record OutputPathResolution (
        string? OutputPath,
        string? ErrorMessage)
    {
        /// <summary> Gets a value indicating whether output-path resolution succeeded. </summary>
        public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary> Creates a successful output-path resolution. </summary>
        /// <param name="outputPath"> The resolved absolute output path. </param>
        /// <returns> The successful output-path resolution result. </returns>
        public static OutputPathResolution Success (string outputPath)
        {
            return new OutputPathResolution(outputPath, null);
        }

        /// <summary> Creates a failed output-path resolution. </summary>
        /// <param name="errorMessage"> The invalid-input error message. </param>
        /// <returns> The failed output-path resolution result. </returns>
        public static OutputPathResolution Failure (string errorMessage)
        {
            return new OutputPathResolution(null, errorMessage);
        }
    }

    /// <summary> Represents raw path-value resolution result. </summary>
    /// <param name="PathValue"> The normalized path value when successful. </param>
    /// <param name="ErrorMessage"> The error message when resolution failed. </param>
    private sealed record PathValueResolution (
        string? PathValue,
        string? ErrorMessage)
    {
        /// <summary> Gets a value indicating whether path-value resolution succeeded. </summary>
        public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

        /// <summary> Creates a successful path-value resolution. </summary>
        /// <param name="pathValue"> The normalized path value. </param>
        /// <returns> The successful path-value resolution result. </returns>
        public static PathValueResolution Success (string pathValue)
        {
            return new PathValueResolution(pathValue, null);
        }

        /// <summary> Creates a failed path-value resolution. </summary>
        /// <param name="errorMessage"> The invalid-input error message. </param>
        /// <returns> The failed path-value resolution result. </returns>
        public static PathValueResolution Failure (string errorMessage)
        {
            return new PathValueResolution(null, errorMessage);
        }
    }
}
