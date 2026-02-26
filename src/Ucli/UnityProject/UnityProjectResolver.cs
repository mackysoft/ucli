using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.UnityProject;

/// <summary> Resolves UnityProject identity information from command inputs. </summary>
internal sealed class UnityProjectResolver : IUnityProjectResolver
{
    private const string ProjectSettingsDirectoryName = "ProjectSettings";
    private const string ProjectVersionFileName = "ProjectVersion.txt";

    private const string UcliDirectoryName = ".ucli";
    private const string ConfigFileName = "config.json";

    /// <summary> Resolves UnityProject context from command options and validates required project markers. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    public UnityProjectResolutionResult Resolve (string? projectPath)
    {
        var pathSource = string.IsNullOrWhiteSpace(projectPath)
            ? UnityProjectPathSource.CurrentDirectory
            : UnityProjectPathSource.CommandOption;
        var pathCandidate = pathSource == UnityProjectPathSource.CurrentDirectory
            ? Environment.CurrentDirectory
            : projectPath!;
        var fullPathResult = TryNormalizePath(pathCandidate);
        if (!fullPathResult.IsSuccess)
        {
            return UnityProjectResolutionResult.Failure(fullPathResult.Error!);
        }

        var unityProjectRoot = fullPathResult.Path!;
        if (!Directory.Exists(unityProjectRoot))
        {
            return UnityProjectResolutionResult.Failure(CreateInvalidArgument(
                $"UnityProject path does not exist: {unityProjectRoot}"));
        }

        var projectVersionPath = Path.Combine(
            unityProjectRoot,
            ProjectSettingsDirectoryName,
            ProjectVersionFileName);
        if (!File.Exists(projectVersionPath))
        {
            return UnityProjectResolutionResult.Failure(CreateInvalidArgument(
                $"UnityProject is invalid. Missing file: {projectVersionPath}"));
        }

        var projectFingerprint = CreateProjectFingerprint(unityProjectRoot);
        var configPath = Path.Combine(unityProjectRoot, UcliDirectoryName, ConfigFileName);
        return UnityProjectResolutionResult.Success(new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: pathSource,
            ConfigPath: configPath));
    }

    /// <summary> Creates a deterministic SHA-256 fingerprint for the normalized UnityProject root path. </summary>
    /// <param name="unityProjectRoot"> The normalized absolute UnityProject root path. </param>
    /// <returns> The lowercase hexadecimal SHA-256 string. </returns>
    private static string CreateProjectFingerprint (string unityProjectRoot)
    {
        var normalizedPath = NormalizeForFingerprint(unityProjectRoot);
        var normalizedBytes = Encoding.UTF8.GetBytes(normalizedPath);
        var hashBytes = SHA256.HashData(normalizedBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary> Normalizes a UnityProject path value to improve fingerprint stability. </summary>
    /// <param name="unityProjectRoot"> The input UnityProject root path. </param>
    /// <returns> The normalized path value used as fingerprint input. </returns>
    private static string NormalizeForFingerprint (string unityProjectRoot)
    {
        var fullPath = Path.GetFullPath(unityProjectRoot);
        fullPath = fullPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var pathRoot = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(pathRoot) && string.Equals(fullPath, pathRoot, PathComparison))
        {
            return NormalizeCase(fullPath);
        }

        var trimmedPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return NormalizeCase(trimmedPath);
    }

    /// <summary> Normalizes path casing for platforms with case-insensitive paths. </summary>
    /// <param name="path"> The path value to normalize. </param>
    /// <returns> The normalized path string. </returns>
    private static string NormalizeCase (string path)
    {
        return OperatingSystem.IsWindows()
            ? path.ToUpperInvariant()
            : path;
    }

    /// <summary> Attempts to normalize a path into an absolute path while converting path format errors to structured output. </summary>
    /// <param name="pathValue"> The input path value. </param>
    /// <returns> The normalization result. </returns>
    private static PathNormalizationResult TryNormalizePath (string pathValue)
    {
        try
        {
            var fullPath = Path.GetFullPath(pathValue);
            return PathNormalizationResult.Success(fullPath);
        }
        catch (Exception ex) when (IsPathFormatException(ex))
        {
            return PathNormalizationResult.Failure(CreateInvalidArgument(
                $"UnityProject path is invalid: {pathValue}"));
        }
    }

    /// <summary> Determines whether an exception represents invalid input path formatting. </summary>
    /// <param name="exception"> The exception to inspect. </param>
    /// <returns>
    /// <para> <see langword="true" /> when the exception should be mapped to <c>INVALID_ARGUMENT</c>. </para>
    /// <para> Otherwise, <see langword="false" />. </para>
    /// </returns>
    private static bool IsPathFormatException (Exception exception)
    {
        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }

    /// <summary> Creates an invalid-argument foundation error. </summary>
    /// <param name="message"> The error message. </param>
    /// <returns> The structured invalid-argument error. </returns>
    private static ExecutionError CreateInvalidArgument (string message)
    {
        return new ExecutionError(ExecutionErrorKind.InvalidArgument, message);
    }

    /// <summary> Gets the path comparison mode for the current operating system. </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary> Represents the result of path normalization. </summary>
    /// <param name="Path"> The normalized absolute path. </param>
    /// <param name="Error"> The structured error when normalization fails. </param>
    private readonly record struct PathNormalizationResult (
        string? Path,
        ExecutionError? Error)
    {
        /// <summary> Gets a value indicating whether path normalization succeeded. </summary>
        public bool IsSuccess => !string.IsNullOrWhiteSpace(Path) && Error is null;

        /// <summary> Creates a successful path normalization result. </summary>
        /// <param name="path"> The normalized absolute path. </param>
        /// <returns> The successful result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="path" /> is <see langword="null" />. </exception>
        public static PathNormalizationResult Success (string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return new PathNormalizationResult(path, null);
        }

        /// <summary> Creates a failed path normalization result. </summary>
        /// <param name="error"> The structured error. </param>
        /// <returns> The failed result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
        public static PathNormalizationResult Failure (ExecutionError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new PathNormalizationResult(null, error);
        }
    }
}