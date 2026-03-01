using System;
using System.Linq;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Registers Unity daemon IPC services into one DI service collection. </summary>
    internal static class UnityDaemonServiceCollectionExtensions
    {
        /// <summary> Adds Unity daemon IPC services required by daemon bootstrap. </summary>
        /// <param name="services"> The target service collection. </param>
        /// <param name="bootstrapArguments"> The parsed daemon bootstrap arguments. </param>
        /// <param name="shutdownSignal"> The callback invoked when shutdown request is accepted. </param>
        /// <returns> The same service collection for fluent chaining. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when one argument is <see langword="null" />. </exception>
        public static IServiceCollection AddUnityDaemonIpc (
            this IServiceCollection services,
            DaemonBootstrapArguments bootstrapArguments,
            Action shutdownSignal)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (bootstrapArguments == null)
            {
                throw new ArgumentNullException(nameof(bootstrapArguments));
            }

            if (shutdownSignal == null)
            {
                throw new ArgumentNullException(nameof(shutdownSignal));
            }

            services.AddSingleton(bootstrapArguments);
            services.AddSingleton(shutdownSignal);
            services.AddSingleton<ISessionTokenValidator>(new FileBackedSessionTokenValidator(bootstrapArguments.SessionPath));
            services.AddSingleton<IExecuteRequestDispatcher>(static _ => CreateExecuteRequestDispatcher());
            services.AddSingleton<IUnityIpcRequestHandler, UnityIpcRequestHandler>();
            services.AddSingleton<IUnityIpcConnectionHandler, UnityIpcConnectionHandler>();
            services.AddSingleton<IUnityIpcTransportListener, NamedPipeUnityIpcTransportListener>();
            services.AddSingleton<IUnityIpcTransportListener, UnixDomainSocketUnityIpcTransportListener>();
            services.AddSingleton<IUnityIpcServer>(serviceProvider =>
            {
                return new UnityIpcServer(
                    serviceProvider.GetRequiredService<IUnityIpcRequestHandler>(),
                    serviceProvider.GetRequiredService<IUnityIpcConnectionHandler>(),
                    serviceProvider.GetServices<IUnityIpcTransportListener>().ToArray());
            });
            return services;
        }

        /// <summary> Creates execute-request dispatcher used by Unity daemon mode. </summary>
        /// <returns> The execute-request dispatcher instance. </returns>
        private static IExecuteRequestDispatcher CreateExecuteRequestDispatcher ()
        {
            var normalizer = new ExecuteRequestNormalizer();
            var operationRegistry = new InMemoryPhaseOperationRegistry(Array.Empty<IPhaseOperation>());
            var phaseExecutor = new OperationPhaseExecutor(operationRegistry);
            return new ExecuteRequestDispatcher(normalizer, phaseExecutor);
        }
    }
}
