using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Decorates one shared IPC connection handler and signals oneshot completion after the first handled request. </summary>
    internal sealed class UnityOneshotConnectionHandler : IUnityIpcConnectionHandler
    {
        private readonly UnityIpcConnectionHandler innerConnectionHandler;

        private readonly OneshotRequestCompletionSignal completionSignal;

        /// <summary> Initializes a new instance of the <see cref="UnityOneshotConnectionHandler" /> class. </summary>
        /// <param name="innerConnectionHandler"> The shared IPC connection handler. </param>
        /// <param name="completionSignal"> The oneshot completion signal. </param>
        public UnityOneshotConnectionHandler (
            UnityIpcConnectionHandler innerConnectionHandler,
            OneshotRequestCompletionSignal completionSignal)
        {
            this.innerConnectionHandler = innerConnectionHandler ?? throw new ArgumentNullException(nameof(innerConnectionHandler));
            this.completionSignal = completionSignal ?? throw new ArgumentNullException(nameof(completionSignal));
        }

        /// <inheritdoc />
        public async Task<IpcRequest?> Handle (
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            var request = await innerConnectionHandler.Handle(stream, cancellationToken);
            if (request != null && !string.Equals(request.Method, IpcMethodNames.Ping, StringComparison.Ordinal))
            {
                completionSignal.Signal();
            }

            return request;
        }
    }
}
