using MackySoft.Ucli.Application.Features.Programs.Persistence;

namespace MackySoft.Ucli.Features.Programs.Persistence;

/// <summary> Opens isolated Program Run file stores under each resolved project repository. </summary>
internal sealed class FileProgramRunStoreFactory : IProgramRunStoreFactory, IProgramArtifactStoreFactory
{
    public IProgramRunStore ForProject (ResolvedUnityProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new FileProgramRunStore(project.RepositoryRoot, project.ProjectFingerprint);
    }

    IProgramArtifactStore IProgramArtifactStoreFactory.ForProject (ResolvedUnityProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new FileProgramRunStore(project.RepositoryRoot, project.ProjectFingerprint);
    }
}
