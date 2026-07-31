using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Play.UseCases.Exit;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates command-level JSON results from Play Mode exit execution results. </summary>
internal static class PlayExitCommandResultFactory
{
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(PlayExitExecutionOutput));

    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        PlayTransitionErrorCommandPayloadFactory.TypeInfo;

    public static object CreateEmptyErrorPayload ()
    {
        return PlayTransitionErrorCommandPayloadFactory.Empty();
    }

    /// <summary> Creates one command result for <c>play exit</c>. </summary>
    /// <param name="executionResult"> The Play Mode exit execution result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlayExitExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.PlayExit,
                message: executionResult.Message,
                payload: executionResult.Output!);
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.PlayExit,
            executionResult.Message,
            executionResult.FailureContext == null
                ? CreateEmptyErrorPayload()
                : PlayTransitionErrorCommandPayloadFactory.From(executionResult),
            [executionResult.Error!]);
    }

    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(PlayExitExecutionResult.Failure(error));
    }
}
