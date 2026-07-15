using System;
using MackySoft.Ucli.Contracts;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>daemon.logs.read</c> IPC method requests. </summary>
    internal sealed class DaemonLogsReadUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly IDaemonLogStream daemonLogStream;

        private readonly IDaemonLogsReadRequestValidator requestValidator;

        private readonly IDaemonLogsReadQueryEngine queryEngine;

        private readonly DaemonLogsReadResponseFactory responseFactory;

        private readonly IDaemonLogger daemonLogger;

        /// <summary> Initializes a new instance of the <see cref="DaemonLogsReadUnityIpcMethodHandler" /> class. </summary>
        /// <param name="daemonLogStream"> The daemon-log stream dependency. </param>
        /// <param name="requestValidator"> The daemon-log request validator dependency. </param>
        /// <param name="queryEngine"> The daemon-log query-engine dependency. </param>
        /// <param name="responseFactory"> The daemon-log response-factory dependency. </param>
        /// <param name="daemonLogger"> The daemon daemon-logger dependency. </param>
        public DaemonLogsReadUnityIpcMethodHandler (
            IDaemonLogStream daemonLogStream,
            IDaemonLogsReadRequestValidator requestValidator,
            IDaemonLogsReadQueryEngine queryEngine,
            DaemonLogsReadResponseFactory responseFactory,
            IDaemonLogger daemonLogger)
        {
            this.daemonLogStream = daemonLogStream ?? throw new ArgumentNullException(nameof(daemonLogStream));
            this.requestValidator = requestValidator ?? throw new ArgumentNullException(nameof(requestValidator));
            this.queryEngine = queryEngine ?? throw new ArgumentNullException(nameof(queryEngine));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
            this.daemonLogger = daemonLogger ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        /// <inheritdoc />
        public UnityIpcMethod Method => UnityIpcMethod.DaemonLogsRead;

        /// <inheritdoc />
        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeDaemonLogsReadRequest(
                    request,
                    out IpcDaemonLogsReadRequest payload,
                    out var decodeErrorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read payload decode failed.",
                    decodeErrorResponse?.Errors.Count > 0 ? decodeErrorResponse.Errors[0].Message : null);
                return new ValueTask<IpcResponse>(decodeErrorResponse);
            }

            var snapshot = daemonLogStream.Snapshot();

            if (!requestValidator.TryValidate(
                    payload,
                    snapshot.StreamId,
                    out var filter,
                    out var errorMessage))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Daemon logs read rejected due to invalid argument.",
                    errorMessage);
                return new ValueTask<IpcResponse>(CreateInvalidArgumentResponse(request, errorMessage));
            }

            var filteredEvents = queryEngine.Filter(snapshot.Events, filter);
            var responsePayload = responseFactory.Create(filteredEvents, snapshot.NextCursor);
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(request, responsePayload));
        }

        /// <summary> Creates one invalid-argument response envelope. </summary>
        /// <param name="request"> The request envelope context. </param>
        /// <param name="message"> The user-facing error message. </param>
        /// <returns> The invalid-argument response envelope. </returns>
        private static IpcResponse CreateInvalidArgumentResponse (
            ValidatedUnityIpcRequest request,
            string message)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InvalidArgument,
                message,
                null);
        }
    }
}
