using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Shared.Context;

/// <summary> Resolves shared project/config context for command pipelines. </summary>
internal sealed class ProjectContextResolver : IProjectContextResolver
{
    private readonly IProjectPathInputResolver projectPathInputResolver;

    private readonly IUnityProjectResolver unityProjectResolver;

    private readonly IUcliConfigStore configStore;

    /// <summary> Initializes a new instance of the <see cref="ProjectContextResolver" /> class. </summary>
    /// <param name="projectPathInputResolver"> The project-path input resolver dependency. </param>
    /// <param name="unityProjectResolver"> The UnityProject resolver dependency. </param>
    /// <param name="configStore"> The config-store dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a dependency is <see langword="null" />. </exception>
    public ProjectContextResolver (
        IProjectPathInputResolver projectPathInputResolver,
        IUnityProjectResolver unityProjectResolver,
        IUcliConfigStore configStore)
    {
        this.projectPathInputResolver = projectPathInputResolver ?? throw new ArgumentNullException(nameof(projectPathInputResolver));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    /// <summary> Resolves UnityProject and config values into a shared command context. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the context-resolution result that contains either a fully resolved context or a structured error. </returns>
    public async ValueTask<ProjectContextResolutionResult> Resolve (
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var projectPathCandidate = projectPathInputResolver.Resolve(new ProjectContextResolutionInput(projectPath));
        var unityProjectResult = unityProjectResolver.Resolve(projectPathCandidate);
        if (!unityProjectResult.IsSuccess)
        {
            return ProjectContextResolutionResult.Failure(unityProjectResult.Error!);
        }

        var unityProjectContext = unityProjectResult.Context!;
        var configLoadResult = await configStore.Load(unityProjectContext.RepositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!configLoadResult.IsSuccess)
        {
            return ProjectContextResolutionResult.Failure(UcliConfigDiagnosticErrorMapper.ToExecutionError(
                configLoadResult,
                "Config JSON is invalid."));
        }

        var context = new ProjectContext(
            UnityProject: unityProjectContext,
            Config: configLoadResult.Config!,
            ConfigSource: configLoadResult.Source);
        return ProjectContextResolutionResult.Success(context);
    }
}
