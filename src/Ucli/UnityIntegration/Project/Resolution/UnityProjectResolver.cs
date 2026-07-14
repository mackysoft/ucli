using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Resolution;

namespace MackySoft.Ucli.UnityIntegration.Project.Resolution;

/// <summary> Resolves UnityProject identity information from command inputs. </summary>
internal sealed class UnityProjectResolver : IUnityProjectResolver
{
    /// <summary> Resolves UnityProject context from a selected project-path candidate and validates required project markers. </summary>
    /// <param name="projectPathCandidate"> The selected but not yet normalized project-path candidate. </param>
    /// <returns> The resolution result containing either a validated UnityProject context or a structured error. </returns>
    public UnityProjectResolutionResult Resolve (ProjectPathCandidate projectPathCandidate)
    {
        ArgumentNullException.ThrowIfNull(projectPathCandidate);

        var fullPathResult = PathNormalizer.TryNormalizeFullPath(projectPathCandidate.Path);
        if (!fullPathResult.IsSuccess)
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                "UnityProject path is invalid: Path format is invalid.",
                ProjectContextErrorCodes.ProjectPathInvalidFormat));
        }

        var unityProjectRoot = fullPathResult.FullPath!;
        if (!Directory.Exists(unityProjectRoot))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {unityProjectRoot}",
                ProjectContextErrorCodes.ProjectPathNotFound));
        }

        var projectVersionPath = UnityProjectVersionFileReader.GetProjectVersionPath(unityProjectRoot);
        if (!File.Exists(projectVersionPath))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject is invalid. Missing file: {projectVersionPath}",
                ProjectContextErrorCodes.UnityProjectMarkerMissing));
        }

        var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(unityProjectRoot);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(repositoryRoot, unityProjectRoot);
        var unityVersion = ReadUnityVersionOrUnknown(projectVersionPath);
        return UnityProjectResolutionResult.Success(new ResolvedUnityProjectContext(
            UnityProjectRoot: unityProjectRoot,
            RepositoryRoot: repositoryRoot,
            ProjectFingerprint: projectFingerprint,
            PathSource: projectPathCandidate.Source,
            PathSourceLabel: projectPathCandidate.SourceLabel,
            UnityVersion: unityVersion));
    }

    private static string ReadUnityVersionOrUnknown (string projectVersionPath)
    {
        var readResult = UnityProjectVersionFileReader.ReadEditorVersion(projectVersionPath);
        return readResult.Status == UnityProjectVersionFileReader.ReadStatus.Success
            ? readResult.UnityVersion!
            : ProjectIdentityDefaults.UnknownUnityVersion;
    }
}
