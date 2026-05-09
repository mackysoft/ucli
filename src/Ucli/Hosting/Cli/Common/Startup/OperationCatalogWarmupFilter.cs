using ConsoleAppFramework;

namespace MackySoft.Ucli.Hosting.Cli.Common.Startup;

/// <summary> Loads the operation catalog before command execution to fail early on registry configuration errors. </summary>
internal sealed class OperationCatalogWarmupFilter : ConsoleAppFilter
{
    private readonly IOperationCatalogWarmup operationCatalogWarmup;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogWarmupFilter" /> class. </summary>
    /// <param name="operationCatalogWarmup"> The operation catalog warmup dependency used for startup warmup. </param>
    /// <param name="next"> The next filter in the command pipeline. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationCatalogWarmup" /> is <see langword="null" />. </exception>
    public OperationCatalogWarmupFilter (
        IOperationCatalogWarmup operationCatalogWarmup,
        ConsoleAppFilter next)
        : base(next)
    {
        this.operationCatalogWarmup = operationCatalogWarmup ?? throw new ArgumentNullException(nameof(operationCatalogWarmup));
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

        await operationCatalogWarmup.WarmupAsync(cancellationToken).ConfigureAwait(false);
        await Next.InvokeAsync(context, cancellationToken).ConfigureAwait(false);
    }
}
