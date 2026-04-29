namespace MackySoft.Ucli.Hosting.Cli.Common.Startup;

/// <summary> Loads hosting-visible operation metadata before command execution starts. </summary>
internal interface IOperationCatalogWarmup
{
    /// <summary> Ensures the operation catalog is loaded. </summary>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes after warmup finishes. </returns>
    ValueTask Warmup (CancellationToken cancellationToken = default);
}
