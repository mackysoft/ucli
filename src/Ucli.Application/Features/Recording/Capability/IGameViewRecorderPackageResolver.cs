using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Application.Features.Recording.Capability;

/// <summary>Observes the resolved Unity Recorder package without loading Recorder assemblies.</summary>
internal interface IGameViewRecorderPackageResolver
{
    /// <summary>Reads the resolved package graph for one Unity project.</summary>
    ValueTask<GameViewRecorderPackageResolution> ResolveAsync (
        ResolvedUnityProjectContext unityProject,
        CancellationToken cancellationToken = default);
}
