using System;
using System.Threading;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Creates production request timeout scopes backed by <see cref="CancellationTokenSource" /> timers. </summary>
    internal sealed class IpcRequestTimeoutScopeFactory : IIpcRequestTimeoutScopeFactory
    {
        /// <inheritdoc />
        public IIpcRequestTimeoutScope CreateLinked (int? timeoutMilliseconds, CancellationToken cancellationToken)
        {
            if (!timeoutMilliseconds.HasValue)
            {
                return new PassthroughTimeoutScope(cancellationToken);
            }

            if (timeoutMilliseconds.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "timeoutMilliseconds must be greater than zero when specified.");
            }

            var timeoutCancellationTokenSource = new CancellationTokenSource();
            timeoutCancellationTokenSource.CancelAfter(timeoutMilliseconds.Value);
            try
            {
                var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCancellationTokenSource.Token);
                return new LinkedTimeoutScope(linkedCancellationTokenSource, timeoutCancellationTokenSource);
            }
            catch
            {
                timeoutCancellationTokenSource.Dispose();
                throw;
            }
        }

        private sealed class LinkedTimeoutScope : IIpcRequestTimeoutScope
        {
            private readonly CancellationTokenSource linkedCancellationTokenSource;
            private readonly CancellationTokenSource timeoutCancellationTokenSource;

            public LinkedTimeoutScope (
                CancellationTokenSource linkedCancellationTokenSource,
                CancellationTokenSource timeoutCancellationTokenSource)
            {
                this.linkedCancellationTokenSource = linkedCancellationTokenSource ?? throw new ArgumentNullException(nameof(linkedCancellationTokenSource));
                this.timeoutCancellationTokenSource = timeoutCancellationTokenSource ?? throw new ArgumentNullException(nameof(timeoutCancellationTokenSource));
            }

            public CancellationToken Token => linkedCancellationTokenSource.Token;

            public bool IsTimeoutCancellationRequested => timeoutCancellationTokenSource.IsCancellationRequested;

            public void Dispose ()
            {
                linkedCancellationTokenSource.Dispose();
                timeoutCancellationTokenSource.Dispose();
            }
        }

        private sealed class PassthroughTimeoutScope : IIpcRequestTimeoutScope
        {
            public PassthroughTimeoutScope (CancellationToken cancellationToken)
            {
                Token = cancellationToken;
            }

            public CancellationToken Token { get; }

            public bool IsTimeoutCancellationRequested => false;

            public void Dispose ()
            {
            }
        }
    }
}
