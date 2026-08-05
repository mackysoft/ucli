using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.UseCases;

/// <summary>Returns either a recording payload or a command error with an optional durable execution checkpoint.</summary>
internal sealed record GameViewRecordingServiceResult<TPayload>
    where TPayload : GameViewRecordingPayload
{
    private GameViewRecordingServiceResult (
        TPayload? payload,
        ExecutionError? error,
        GameViewRecordingExecutionPayload? executionCheckpoint)
    {
        if ((payload is null) == (error is null))
        {
            throw new ArgumentException("A recording service result must contain exactly one success payload or error.");
        }
        if (payload is not null && executionCheckpoint is not null)
        {
            throw new ArgumentException("A successful recording service result cannot contain an error checkpoint.");
        }

        Payload = payload;
        Error = error;
        ExecutionCheckpoint = executionCheckpoint;
    }

    [MemberNotNullWhen(true, nameof(Payload))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public TPayload? Payload { get; }

    public ExecutionError? Error { get; }

    public GameViewRecordingExecutionPayload? ExecutionCheckpoint { get; }

    public static GameViewRecordingServiceResult<TPayload> Success (TPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new GameViewRecordingServiceResult<TPayload>(payload, error: null, executionCheckpoint: null);
    }

    public static GameViewRecordingServiceResult<TPayload> Failure (
        ExecutionError error,
        GameViewRecordingExecutionPayload? executionCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingServiceResult<TPayload>(payload: null, error, executionCheckpoint);
    }
}
