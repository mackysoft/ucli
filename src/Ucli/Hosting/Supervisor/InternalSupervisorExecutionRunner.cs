using MackySoft.Ucli.Hosting.Composition.Common;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Supervisor;

/// <summary> Runs the hidden supervisor host through the same service registration path as the public CLI. </summary>
internal sealed class InternalSupervisorExecutionRunner
{
    /// <summary> Executes the supervisor host for the specified repository root. </summary>
    /// <param name="repositoryRoot"> The repository root used as supervisor storage root. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the hosting environment. </param>
    /// <returns> The supervisor process exit code. </returns>
    public async Task<int> RunAsync (
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            return 1;
        }

        await using var serviceProvider = BuildServiceProvider();
        var supervisorHost = serviceProvider.GetRequiredService<SupervisorHost>();
        return await supervisorHost.Run(repositoryRoot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary> Builds the service provider used by the hidden supervisor host. </summary>
    /// <returns> The service provider configured through the shared composition root. </returns>
    internal static ServiceProvider BuildServiceProvider ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();
        return services.BuildServiceProvider();
    }
}
