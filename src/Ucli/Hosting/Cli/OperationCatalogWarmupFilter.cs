using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Hosting.Cli;

/// <summary> Loads the operation catalog before command execution to fail early on registry configuration errors. </summary>
internal sealed class OperationCatalogWarmupFilter : ConsoleAppFilter
{
    private readonly IOperationCatalog operationCatalog;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogWarmupFilter" /> class. </summary>
    /// <param name="operationCatalog"> The operation catalog used for startup warmup. </param>
    /// <param name="next"> The next filter in the command pipeline. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationCatalog" /> is <see langword="null" />. </exception>
    public OperationCatalogWarmupFilter (
        IOperationCatalog operationCatalog,
        ConsoleAppFilter next)
        : base(next)
    {
        this.operationCatalog = operationCatalog ?? throw new ArgumentNullException(nameof(operationCatalog));
    }

    /// <summary> Ensures operation metadata is loaded before command handlers run. </summary>
    /// <param name="context"> The command execution context. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes after warmup and downstream filter execution. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="context" /> is <see langword="null" />. </exception>
    public override async Task InvokeAsync (ConsoleAppContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        await operationCatalog.GetAll(cancellationToken).ConfigureAwait(false);
        await Next.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
    }
}