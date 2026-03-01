using System;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Implements service-provider creation for Unity daemon bootstrap. </summary>
    internal sealed class UnityDaemonServiceProviderFactory : IUnityDaemonServiceProviderFactory
    {
        /// <summary> Creates a configured service provider for one daemon bootstrap session. </summary>
        /// <param name="bootstrapArguments"> The parsed daemon bootstrap arguments. </param>
        /// <param name="shutdownSignal"> The callback invoked when shutdown request is accepted. </param>
        /// <returns> The configured service provider. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when one argument is <see langword="null" />. </exception>
        public ServiceProvider Create (
            DaemonBootstrapArguments bootstrapArguments,
            Action shutdownSignal)
        {
            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            if (shutdownSignal == null)
            {
                throw new ArgumentNullException(nameof(shutdownSignal));
            }

            var services = new ServiceCollection();
            services.AddUnityDaemonIpc(bootstrapArguments, shutdownSignal);
            return services.BuildServiceProvider();
        }
    }
}
