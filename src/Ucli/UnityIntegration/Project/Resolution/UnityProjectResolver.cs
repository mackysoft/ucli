using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
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

        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        if (!AbsolutePath.TryResolve(currentDirectory, projectPathCandidate.Path, out var unityProjectRoot, out _))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                "UnityProject path is invalid: Path format is invalid.",
                ProjectContextErrorCodes.ProjectPathInvalidFormat));
        }

        return Resolve(
            unityProjectRoot,
            projectPathCandidate.Source,
            projectPathCandidate.SourceLabel);
    }

    /// <inheritdoc />
    public UnityProjectResolutionResult Resolve (
        AbsolutePath unityProjectRoot,
        UnityProjectPathSource source,
        string? sourceLabel = null)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);

        if (!Directory.Exists(unityProjectRoot.Value))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject path does not exist: {unityProjectRoot.Value}",
                ProjectContextErrorCodes.ProjectPathNotFound));
        }

        var projectVersionPath = UnityProjectVersionFileReader.GetProjectVersionPath(unityProjectRoot);
        if (!File.Exists(projectVersionPath.Value))
        {
            return UnityProjectResolutionResult.Failure(ExecutionError.InvalidArgument(
                $"UnityProject is invalid. Missing file: {projectVersionPath.Value}",
                ProjectContextErrorCodes.UnityProjectMarkerMissing));
        }

        var repositoryRoot = UcliStoragePathResolver.ResolveStorageRoot(unityProjectRoot);
        var projectFingerprint = UnityProjectFingerprintCalculator.Create(repositoryRoot, unityProjectRoot);
        var unityVersion = ReadUnityVersionOrUnknown(projectVersionPath);
        return UnityProjectResolutionResult.Success(ResolvedUnityProjectContext.Create(
            unityProjectRoot: unityProjectRoot,
            repositoryRoot: repositoryRoot,
            projectFingerprint: projectFingerprint,
            pathSource: source,
            pathSourceLabel: sourceLabel,
            unityVersion: unityVersion));
    }

    private static string ReadUnityVersionOrUnknown (AbsolutePath projectVersionPath)
    {
        var readResult = UnityProjectVersionFileReader.ReadEditorVersion(projectVersionPath);
        return readResult.Status == UnityProjectVersionFileReader.ReadStatus.Success
            ? readResult.UnityVersion!
            : ProjectIdentityDefaults.UnknownUnityVersion;
    }
}
