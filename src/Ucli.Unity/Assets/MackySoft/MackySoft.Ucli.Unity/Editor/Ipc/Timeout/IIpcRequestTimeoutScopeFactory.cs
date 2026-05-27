using System.Threading;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates cancellation scopes for IPC request execution timeouts. </summary>
    internal interface IIpcRequestTimeoutScopeFactory
    {
        /// <summary> Creates a cancellation scope linked to the caller token and the optional request timeout. </summary>
        /// <param name="timeoutMilliseconds"> The timeout in milliseconds, or <c>null</c> when the request is unbounded. </param>
        /// <param name="cancellationToken"> The caller cancellation token. </param>
        /// <returns> The created cancellation scope. </returns>
        IIpcRequestTimeoutScope CreateLinked (int? timeoutMilliseconds, CancellationToken cancellationToken);
    }
}
