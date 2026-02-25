using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Context
{
    /// <summary> Resolves shared foundation context for init/status command pipelines. </summary>
    internal sealed class InitStatusContextResolver : IInitStatusContextResolver
    {
        private readonly IUnityProjectResolver unityProjectResolver;
        private readonly IUcliConfigStore configStore;

        /// <summary> Initializes a new instance of the <see cref="InitStatusContextResolver" /> class. </summary>
        /// <param name="unityProjectResolver"> The UnityProject resolver dependency. </param>
        /// <param name="configStore"> The config-store dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProjectResolver" /> or <paramref name="configStore" /> is <see langword="null" />. </exception>
        public InitStatusContextResolver (
            IUnityProjectResolver unityProjectResolver,
            IUcliConfigStore configStore)
        {
            this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
            this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        }

        /// <summary> Resolves UnityProject and config values into a shared init/status context. </summary>
        /// <param name="projectPath"> The optional <c>--projectPath</c> value. When <see langword="null" />, empty, or whitespace, the current working directory is used. </param>
        /// <param name="cancellationToken"> A cancellation token propagated by command execution. </param>
        /// <returns> The context-resolution result that contains either a fully resolved context or a structured error. </returns>
        public InitStatusContextResolutionResult Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var unityProjectResult = unityProjectResolver.Resolve(projectPath, cancellationToken);
            if (!unityProjectResult.IsSuccess)
            {
                return InitStatusContextResolutionResult.Failure(unityProjectResult.Error!);
            }

            var unityProjectContext = unityProjectResult.Context!;
            var configLoadResult = configStore.Load(unityProjectContext.UnityProjectRoot, cancellationToken);
            if (!configLoadResult.IsSuccess)
            {
                return InitStatusContextResolutionResult.Failure(configLoadResult.Error!);
            }

            var context = new InitStatusContext(
                UnityProject: unityProjectContext,
                Config: configLoadResult.Config!,
                ConfigSource: configLoadResult.Source);
            return InitStatusContextResolutionResult.Success(context);
        }
    }
}
