using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents one request execution cancellation scope with timeout diagnostics. </summary>
    internal interface IIpcRequestTimeoutScope : IDisposable
    {
        /// <summary> Gets the token observed by request execution services. </summary>
        CancellationToken Token { get; }

        /// <summary> Gets a value indicating whether this scope was cancelled by its request timeout. </summary>
        bool IsTimeoutCancellationRequested { get; }
    }
}
