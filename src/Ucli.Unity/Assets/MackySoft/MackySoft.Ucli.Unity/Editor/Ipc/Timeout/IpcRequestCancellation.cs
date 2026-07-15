using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Identifies why one IPC request execution token was canceled. </summary>
    internal enum IpcRequestCancellationReason
    {
        None = 0,
        Upstream = 1,
        ExecutionDeadline = 2,
    }

    /// <summary> Carries one request's execution token and first observed cancellation reason. </summary>
    internal sealed class IpcRequestCancellation : IDisposable
    {
        private int reason;

        private readonly CancellationToken executionToken;

        private readonly CancellationTokenRegistration deadlineCancellationRegistration;

        private readonly CancellationTokenRegistration upstreamCancellationRegistration;

        /// <summary> Initializes cancellation state owned by the request phase scope. </summary>
        /// <param name="executionToken"> The token canceled by the first upstream or execution-cutoff cancellation. </param>
        /// <param name="executionDeadlineToken"> The token canceled only at the execution cutoff. </param>
        /// <param name="upstreamCancellationToken"> The method execution lifetime selected by connection policy and linked into the scope. </param>
        public IpcRequestCancellation (
            CancellationToken executionToken,
            CancellationToken executionDeadlineToken,
            CancellationToken upstreamCancellationToken)
        {
            this.executionToken = executionToken;

            if (upstreamCancellationToken.IsCancellationRequested)
            {
                reason = (int)IpcRequestCancellationReason.Upstream;
            }
            else if (executionDeadlineToken.IsCancellationRequested)
            {
                reason = (int)IpcRequestCancellationReason.ExecutionDeadline;
            }

            deadlineCancellationRegistration = executionDeadlineToken.Register(
                static state => ((IpcRequestCancellation)state).TrySetReason(
                    IpcRequestCancellationReason.ExecutionDeadline),
                this);
            try
            {
                upstreamCancellationRegistration = upstreamCancellationToken.Register(
                    static state => ((IpcRequestCancellation)state).TrySetReason(
                        IpcRequestCancellationReason.Upstream),
                    this);
            }
            catch
            {
                deadlineCancellationRegistration.Dispose();
                throw;
            }
        }

        /// <summary> Gets the token observed by the lane executor and method handler. </summary>
        public CancellationToken Token => executionToken;

        /// <summary> Gets the first cancellation reason observed for this request. </summary>
        public IpcRequestCancellationReason Reason =>
            (IpcRequestCancellationReason)Volatile.Read(ref reason);

        /// <summary> Releases cancellation registrations owned by this request state. </summary>
        public void Dispose ()
        {
            upstreamCancellationRegistration.Dispose();
            deadlineCancellationRegistration.Dispose();
        }

        /// <summary> Records an additional upstream cancellation before the linked execution token is canceled. </summary>
        internal void RecordAdditionalUpstreamCancellation ()
        {
            TrySetReason(IpcRequestCancellationReason.Upstream);
        }

        private void TrySetReason (IpcRequestCancellationReason cancellationReason)
        {
            _ = Interlocked.CompareExchange(
                ref reason,
                (int)cancellationReason,
                (int)IpcRequestCancellationReason.None);
        }
    }
}
