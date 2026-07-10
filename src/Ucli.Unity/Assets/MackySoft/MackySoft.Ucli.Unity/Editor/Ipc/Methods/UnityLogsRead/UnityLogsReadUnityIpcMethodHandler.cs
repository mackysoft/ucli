using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>unity.logs.read</c> IPC method requests. </summary>
    internal sealed class UnityLogsReadUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly IUnityLogStream unityLogStream;

        private readonly UnityLogsReadRequestValidator requestValidator;

        private readonly UnityLogsReadQueryEngine queryEngine;

        private readonly UnityLogsReadResponseFactory responseFactory;

        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="UnityLogsReadUnityIpcMethodHandler" /> class. </summary>
        public UnityLogsReadUnityIpcMethodHandler (
            IUnityLogStream unityLogStream,
            UnityLogsReadRequestValidator requestValidator,
            UnityLogsReadQueryEngine queryEngine,
            UnityLogsReadResponseFactory responseFactory,
            IDaemonLogger daemonLogger)
        {
            this.unityLogStream = unityLogStream ?? throw new ArgumentNullException(nameof(unityLogStream));
            this.requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
            this.queryEngine = queryEngine ?? throw new ArgumentNullException(nameof(queryEngine));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public string Method => IpcMethodNames.UnityLogsRead;

        /// <inheritdoc />
        public ValueTask<IpcResponse> HandleAsync (
            IpcRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeUnityLogsReadRequest(
                    request,
                    out IpcUnityLogsReadRequest payload,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unity logs read payload decode failed.",
                    decodeErrorResponse.Errors.Count > 0 ? decodeErrorResponse.Errors[0].Message : null);
                return new ValueTask<IpcResponse>(decodeErrorResponse);
            }

            var snapshot = unityLogStream.Snapshot();
            if (!requestValidator.TryValidate(payload, snapshot.StreamId, out var filter, out var errorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Unity logs read rejected due to invalid argument.",
                    errorMessage);
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    errorMessage,
                    null));
            }

            var filteredEvents = queryEngine.Filter(snapshot.Events, filter);
            var responsePayload = responseFactory.Create(filteredEvents, snapshot.NextCursor);
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, responsePayload));
        }
    }
}
