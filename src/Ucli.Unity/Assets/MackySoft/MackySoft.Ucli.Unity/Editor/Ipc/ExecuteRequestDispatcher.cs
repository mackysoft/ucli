using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Dispatches execute requests to operation-phase execution pipelines. </summary>
    internal sealed class ExecuteRequestDispatcher : IExecuteRequestDispatcher
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            },
        };

        private readonly IExecuteRequestNormalizer requestNormalizer;
        private readonly IOperationPhaseExecutor operationPhaseExecutor;

        /// <summary> Initializes a new instance of the <see cref="ExecuteRequestDispatcher" /> class. </summary>
        /// <param name="requestNormalizer"> The execute-request normalizer dependency. </param>
        /// <param name="operationPhaseExecutor"> The operation-phase executor dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
        public ExecuteRequestDispatcher (
            IExecuteRequestNormalizer requestNormalizer,
            IOperationPhaseExecutor operationPhaseExecutor)
        {
            this.requestNormalizer = requestNormalizer ?? throw new ArgumentNullException(nameof(requestNormalizer));
            this.operationPhaseExecutor = operationPhaseExecutor ?? throw new ArgumentNullException(nameof(operationPhaseExecutor));
        }

        /// <summary> Dispatches one execute request and returns the response envelope. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The response envelope for the incoming request. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> or <paramref name="context" /> is <see langword="null" />. </exception>
        /// <exception cref="System.OperationCanceledException"> Thrown when dispatch is canceled. </exception>
        public async Task<IpcResponse> Dispatch (
            IpcExecuteRequest request,
            ExecuteDispatchContext context,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var normalizationResult = requestNormalizer.Normalize(request, cancellationToken);
            if (!normalizationResult.IsSuccess)
            {
                var normalizationError = normalizationResult.Error!;
                return CreateErrorResponse(
                    context,
                    normalizationError.Code,
                    normalizationError.Message,
                    normalizationError.OpId);
            }

            if (!TryResolveExecutionCommand(request.Command, out var executionCommand))
            {
                return CreateErrorResponse(
                    context,
                    IpcErrorCodes.CommandNotImplemented,
                    $"Execute command '{request.Command}' is not implemented.",
                    null);
            }

            try
            {
                var trace = await operationPhaseExecutor.Execute(executionCommand, normalizationResult.Request!, cancellationToken);
                var payload = JsonSerializer.SerializeToElement(new
                {
                    operationTraces = trace.OperationTraces,
                    errors = trace.Errors,
                }, SerializerOptions);
                var errors = trace.Errors
                    .Select(error => new IpcError(error.Code, error.Message, error.OpId))
                    .ToArray();
                return new IpcResponse(
                    ProtocolVersion: context.ProtocolVersion,
                    RequestId: context.RequestId,
                    Status: trace.IsSuccess ? IpcProtocol.StatusOk : IpcProtocol.StatusError,
                    Payload: payload,
                    Errors: errors);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return CreateErrorResponse(
                    context,
                    IpcErrorCodes.InternalError,
                    $"Unexpected error occurred while dispatching execute request. {exception.Message}",
                    null);
            }
        }

        /// <summary> Resolves phase-execution command from execute request command name. </summary>
        /// <param name="commandName"> The execute request command name. </param>
        /// <param name="command"> The resolved phase command. </param>
        /// <returns> <see langword="true" /> when command is supported in this dispatcher; otherwise <see langword="false" />. </returns>
        private static bool TryResolveExecutionCommand (
            string commandName,
            out PhaseExecutionCommand command)
        {
            switch (commandName)
            {
                case IpcExecuteCommandNames.Plan:
                    command = PhaseExecutionCommand.Plan;
                    return true;

                case IpcExecuteCommandNames.Call:
                    command = PhaseExecutionCommand.Call;
                    return true;

                case IpcExecuteCommandNames.Resolve:
                case IpcExecuteCommandNames.Query:
                case IpcExecuteCommandNames.Refresh:
                    command = default;
                    return false;

                default:
                    command = default;
                    return false;
            }
        }

        /// <summary> Creates an error response with one error entry. </summary>
        /// <param name="context"> The request-level dispatch context. </param>
        /// <param name="code"> The error code. </param>
        /// <param name="message"> The error message. </param>
        /// <param name="opId"> The related operation identifier. </param>
        /// <returns> The error response envelope. </returns>
        private static IpcResponse CreateErrorResponse (
            ExecuteDispatchContext context,
            string code,
            string message,
            string? opId)
        {
            return new IpcResponse(
                ProtocolVersion: context.ProtocolVersion,
                RequestId: context.RequestId,
                Status: IpcProtocol.StatusError,
                Payload: JsonSerializer.SerializeToElement(new { }, SerializerOptions),
                Errors: new[]
                {
                    new IpcError(code, message, opId),
                });
        }
    }
}
