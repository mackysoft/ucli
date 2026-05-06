using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Shared.Context;

/// <summary> Resolves shared project/config context for command pipelines. </summary>
internal sealed class ProjectContextResolver : IProjectContextResolver
{
    private readonly IUnityProjectResolver unityProjectResolver;
    private readonly IUcliConfigStore configStore;

    /// <summary> Initializes a new instance of the <see cref="ProjectContextResolver" /> class. </summary>
    /// <param name="unityProjectResolver"> The UnityProject resolver dependency. </param>
    /// <param name="configStore"> The config-store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProjectResolver" /> or <paramref name="configStore" /> is <see langword="null" />. </exception>
    public ProjectContextResolver (
        IUnityProjectResolver unityProjectResolver,
        IUcliConfigStore configStore)
    {
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    /// <summary> Resolves UnityProject and config values into a shared command context. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the context-resolution result that contains either a fully resolved context or a structured error. </returns>
    public async ValueTask<ProjectContextResolutionResult> Resolve (
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var unityProjectResult = unityProjectResolver.Resolve(projectPath);
        if (!unityProjectResult.IsSuccess)
        {
            return ProjectContextResolutionResult.Failure(unityProjectResult.Error!);
        }

        var unityProjectContext = unityProjectResult.Context!;
        var configLoadResult = await configStore.Load(unityProjectContext.RepositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!configLoadResult.IsSuccess)
        {
            if (configLoadResult.Diagnostics.Count > 0)
            {
                return ProjectContextResolutionResult.Failure(UcliConfigDiagnosticErrorMapper.ToInvalidArgument(
                    configLoadResult.Diagnostics,
                    "Config JSON is invalid."));
            }

            return ProjectContextResolutionResult.Failure(configLoadResult.Error!);
        }

        var context = new ProjectContext(
            UnityProject: unityProjectContext,
            Config: configLoadResult.Config!,
            ConfigSource: configLoadResult.Source);
        return ProjectContextResolutionResult.Success(context);
    }
}
