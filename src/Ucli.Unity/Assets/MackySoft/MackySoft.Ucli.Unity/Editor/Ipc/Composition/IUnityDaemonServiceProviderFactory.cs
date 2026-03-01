using System;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates service-provider instances used by Unity daemon bootstrap. </summary>
    internal interface IUnityDaemonServiceProviderFactory
    {
        /// <summary> Creates a configured service provider for one daemon bootstrap session. </summary>
        /// <param name="bootstrapArguments"> The parsed daemon bootstrap arguments. </param>
        /// <param name="shutdownSignal"> The callback invoked when shutdown request is accepted. </param>
        /// <returns> The configured service provider. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when one argument is <see langword="null" />. </exception>
        ServiceProvider Create (
            DaemonBootstrapArguments bootstrapArguments,
            Action shutdownSignal);
    }
}
