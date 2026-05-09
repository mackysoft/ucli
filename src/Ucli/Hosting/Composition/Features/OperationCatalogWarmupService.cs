using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Hosting.Composition.Features;

/// <summary> Bridges hosting startup warmup to the shared operation catalog. </summary>
internal sealed class OperationCatalogWarmupService : IOperationCatalogWarmup
{
    private readonly IOperationCatalog operationCatalog;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogWarmupService" /> class. </summary>
    /// <param name="operationCatalog"> The shared operation catalog dependency. </param>
    public OperationCatalogWarmupService (IOperationCatalog operationCatalog)
    {
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
    }

    /// <inheritdoc />
    public async ValueTask WarmupAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await operationCatalog.GetAllAsync(cancellationToken).ConfigureAwait(false);
    }
}
