using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Features.Programs.Persistence;

/// <summary> Opens isolated Program Run file stores under each resolved project repository. </summary>
internal sealed class FileProgramRunStoreFactory : IProgramRunStoreFactory
{
    public IProgramRunStore ForProject (ResolvedUnityProjectContext project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new FileProgramRunStore(project.RepositoryRoot, project.ProjectFingerprint);
    }
}
