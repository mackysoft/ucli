using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from the dedicated refresh application result. </summary>
internal static class RefreshCommandResultFactory
{
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(RefreshExecutionOutput));

    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<RefreshExecutionErrorCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<RefreshExecutionErrorCommandPayload>();
    }

    /// <summary> Creates one command result for <c>refresh</c>. </summary>
    public static CommandResult Create (RefreshExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);
        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Refresh,
                message: executionResult.Message,
                payload: executionResult.Output!);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Refresh,
            executionResult.Message,
            executionResult.ErrorOutput is null
                ? CreateEmptyErrorPayload()
                : CommandErrorPayload.Detailed(
                    RefreshExecutionErrorCommandPayload.From(executionResult.ErrorOutput)),
            executionResult.Failures);
    }

    /// <summary> Creates a refresh command result from pre-application input failure. </summary>
    public static CommandResult CreateExecutionError (
        Guid requestId,
        ExecutionError error)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Request id must not be empty.", nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(error);
        return Create(RefreshExecutionResult.Failure(
            ApplicationFailure.FromExecutionError(error)));
    }
}
