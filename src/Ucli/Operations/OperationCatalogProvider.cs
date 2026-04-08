using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Operations;

/// <summary> Builds the operation catalog from discovered operation metadata. </summary>
internal sealed class OperationCatalogProvider : IOperationCatalogProvider
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IOpsCatalogReader opsCatalogReader;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogProvider" /> class. </summary>
    /// <param name="projectContextResolver"> The shared context resolver dependency. </param>
    /// <param name="opsCatalogReader"> The ops catalog reader dependency. </param>
    public OperationCatalogProvider (
        IProjectContextResolver projectContextResolver,
        IOpsCatalogReader opsCatalogReader)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.opsCatalogReader = opsCatalogReader ?? throw new ArgumentNullException(nameof(opsCatalogReader));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await projectContextResolver.Resolve(
                projectPath: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Operation catalog context could not be resolved. {contextResult.Error!.Message}");
        }

        return await GetOperations(
                contextResult.Context!.UnityProject,
                contextResult.Context.Config,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);

        var catalogResult = await opsCatalogReader.Read(
                unityProject,
                config,
                mode: null,
                timeout: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Operation catalog discovery failed. {catalogResult.Message}");
        }

        return OperationDescriptorMapper.Map(catalogResult.Response!.Operations!, cancellationToken);
    }
}