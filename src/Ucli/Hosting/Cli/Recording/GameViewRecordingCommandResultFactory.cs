using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Recording;

/// <summary>Projects recording application results into the public CLI result envelope.</summary>
internal static class GameViewRecordingCommandResultFactory
{
    public static JsonTypeInfo ExecutionSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(GameViewRecordingExecutionPayload));

    public static JsonTypeInfo StatusSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(
            typeof(GameViewRecordingStatusCommandPayload));

    public static JsonTypeInfo StopSuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(
            typeof(GameViewRecordingStopResultPayload));

    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<GameViewRecordingErrorCommandPayload>();

    public static object CreateEmptyErrorPayload () =>
        CommandErrorPayload.Empty<GameViewRecordingErrorCommandPayload>();

    public static CommandResult CreateStart (
        GameViewRecordingServiceResult<GameViewRecordingExecutionPayload> result) =>
        CreateExecution(
            UcliCommandNames.RecordingStart,
            "GameView recording start completed.",
            result);

    public static CommandResult CreateStatus (
        GameViewRecordingServiceResult<GameViewRecordingStatusPayload> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return CreateFailure(UcliCommandNames.RecordingStatus, result);
        }

        return CommandResult.Success(
            UcliCommandNames.RecordingStatus,
            "GameView recording status retrieved.",
            UcliNonNullJsonObject.Wrap(
                GameViewRecordingStatusCommandPayload.Create(result.Payload),
                typeof(GameViewRecordingStatusCommandPayload),
                CliOutputJsonSerializerOptions.Default));
    }

    public static CommandResult CreateStop (
        GameViewRecordingServiceResult<GameViewRecordingStopResultPayload> result) =>
        CreateExecution(
            UcliCommandNames.RecordingStop,
            "GameView recording stop completed.",
            result);

    public static CommandResult CreateExecutionError (
        string command,
        ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            command,
            ApplicationFailure.FromExecutionError(error),
            CreateEmptyErrorPayload());
    }

    private static CommandResult CreateExecution<TPayload> (
        string command,
        string message,
        GameViewRecordingServiceResult<TPayload> result)
        where TPayload : GameViewRecordingPayload
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsSuccess)
        {
            return CreateFailure(command, result);
        }

        return CommandResult.Success(
            command,
            message,
            UcliNonNullJsonObject.Wrap(
                result.Payload,
                typeof(TPayload),
                CliOutputJsonSerializerOptions.Default));
    }

    private static CommandResult CreateFailure<TPayload> (
        string command,
        GameViewRecordingServiceResult<TPayload> result)
        where TPayload : GameViewRecordingPayload
    {
        var error = result.Error
            ?? throw new InvalidOperationException("A failed recording service result must contain an error.");
        var payload = result.ExecutionCheckpoint is null
            ? CreateEmptyErrorPayload()
            : CommandErrorPayload.Detailed(
                new GameViewRecordingErrorCommandPayload(result.ExecutionCheckpoint));
        return CommandFailureProjector.Create(
            command,
            ApplicationFailure.FromExecutionError(error),
            payload);
    }
}
