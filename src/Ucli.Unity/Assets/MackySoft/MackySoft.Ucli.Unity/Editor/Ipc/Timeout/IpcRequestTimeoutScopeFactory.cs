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

            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(timeoutMilliseconds.Value);
            return new LinkedTimeoutScope(cancellationTokenSource);
        }

        private sealed class LinkedTimeoutScope : IIpcRequestTimeoutScope
        {
            private readonly CancellationTokenSource cancellationTokenSource;

            public LinkedTimeoutScope (CancellationTokenSource cancellationTokenSource)
            {
                this.cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
            }

            public CancellationToken Token => cancellationTokenSource.Token;

            public bool IsTimeoutCancellationRequested => cancellationTokenSource.IsCancellationRequested;

            public void Dispose ()
            {
                cancellationTokenSource.Dispose();
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
