using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>validate</c> service results. </summary>
internal static class ValidateCommandResultFactory
{
    /// <summary> Creates one command result for <c>validate</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (ValidateServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        object payload = serviceResult.Output == null
            ? new { }
            : new
            {
                readIndex = ReadIndexInfoPayloadProjector.Create(serviceResult.Output.ReadIndex),
            };

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Validate,
                message: serviceResult.Message,
                payload: payload);
        }

        if (serviceResult.HasValidationErrors)
        {
            var validationErrors = new CommandError[serviceResult.ValidationErrors.Count];
            for (var i = 0; i < serviceResult.ValidationErrors.Count; i++)
            {
                var validationError = serviceResult.ValidationErrors[i];
                validationErrors[i] = new CommandError(
                    validationError.Code,
                    validationError.Message,
                    validationError.OpId);
            }

            return new CommandResult(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                Command: UcliCommandNames.Validate,
                Status: IpcProtocol.StatusError,
                ExitCode: (int)CliExitCode.InvalidArgument,
                Message: serviceResult.Message,
                Payload: payload,
                Errors: validationErrors);
        }

        var errorCode = string.IsNullOrWhiteSpace(serviceResult.ErrorCode)
            ? IpcErrorCodes.InternalError
            : serviceResult.ErrorCode;
        var exitCode = string.Equals(errorCode, IpcErrorCodes.InvalidArgument, StringComparison.Ordinal)
            ? CliExitCode.InvalidArgument
            : CliExitCode.ToolError;

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Validate,
            Status: IpcProtocol.StatusError,
            ExitCode: (int)exitCode,
            Message: serviceResult.Message,
            Payload: payload,
            Errors:
            [
                new CommandError(
                    errorCode,
                    serviceResult.Message,
                    null),
            ]);
    }

    /// <summary> Creates one invalid-execution command result for <c>validate</c>. </summary>
    /// <param name="error"> The normalized execution error. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandResultFactory.FromExecutionError(UcliCommandNames.Validate, error);
    }
}
