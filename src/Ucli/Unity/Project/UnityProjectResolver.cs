using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Contracts.Project;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.EnvironmentVariables;
using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.UnityProject;

/// <summary> Resolves UnityProject identity information from command inputs. </summary>
internal sealed class UnityProjectResolver : IUnityProjectResolver
{
    private const string ProjectSettingsDirectoryName = "ProjectSettings";
    private const string ProjectVersionFileName = "ProjectVersion.txt";

    private readonly IProjectPathInputResolver projectPathInputResolver;

    /// <summary> Initializes a new instance of the <see cref="UnityProjectResolver" /> class. </summary>
    public UnityProjectResolver ()
        : this(new ProjectPathInputResolver(new ProcessEnvironmentVariableReader()))
    {
    }

    /// <summary> Initializes a new instance of the <see cref="UnityProjectResolver" /> class. </summary>
    /// <param name="projectPathInputResolver"> The project-path input resolver dependency. </param>
    public UnityProjectResolver (IProjectPathInputResolver projectPathInputResolver)
    {
        this.projectPathInputResolver = projectPathInputResolver ?? throw new ArgumentNullException(nameof(projectPathInputResolver));
    }

    /// <summary> Resolves UnityProject context from command options and validates required project markers. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    public UnityProjectResolutionResult Resolve (string? projectPath)
    {
        var resolvedProjectPath = projectPathInputResolver.Resolve(projectPath);
        var pathSource = string.IsNullOrWhiteSpace(resolvedProjectPath)
            ? UnityProjectPathSource.CurrentDirectory
            : UnityProjectPathSource.CommandOption;
        var pathCandidate = pathSource == UnityProjectPathSource.CurrentDirectory
            ? Environment.CurrentDirectory
            : resolvedProjectPath!;
        var fullPathResult = TryNormalizePath(pathCandidate);
        if (!fullPathResult.IsSuccess)
        {
            return UnityProjectResolutionResult.Failure(fullPathResult.Error!);
        }

        var unityProjectRoot = fullPathResult.Path!;
        if (!Directory.Exists(unityProjectRoot))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {unityProjectRoot}"));
        }

        var projectVersionPath = Path.Combine(
            unityProjectRoot,
            ProjectSettingsDirectoryName,
            ProjectVersionFileName);
        if (!File.Exists(projectVersionPath))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject is invalid. Missing file: {projectVersionPath}"));
        }

        var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(unityProjectRoot);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(repositoryRoot, unityProjectRoot);
        return UnityProjectResolutionResult.Success(new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: pathSource));
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
        catch (Exception ex) when (PathFormatExceptionClassifier.IsPathFormatException(ex))
        {
            return PathNormalizationResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path is invalid: {pathValue}"));
        }
    }

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