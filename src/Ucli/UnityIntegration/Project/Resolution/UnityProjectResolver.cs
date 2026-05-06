using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.UnityIntegration.Project.Resolution;

/// <summary> Resolves UnityProject identity information from command inputs. </summary>
internal sealed class UnityProjectResolver : IUnityProjectResolver
{
    private const string ProjectSettingsDirectoryName = "ProjectSettings";
    private const string ProjectVersionFileName = "ProjectVersion.txt";

    private readonly IProjectPathInputResolver projectPathInputResolver;

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
        var fullPathResult = PathNormalizer.TryNormalizeFullPath(pathCandidate);
        if (!fullPathResult.IsSuccess)
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path is invalid: {pathCandidate}"));
        }

        var unityProjectRoot = fullPathResult.FullPath!;
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
}
