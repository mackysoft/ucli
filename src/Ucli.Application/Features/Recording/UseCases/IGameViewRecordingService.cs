using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.UseCases;

/// <summary>Owns recording admission, execution correlation, monitoring, stopping, and terminal publication.</summary>
internal interface IGameViewRecordingService
{
    ValueTask<GameViewRecordingServiceResult<GameViewRecordingExecutionPayload>> StartAsync (
        GameViewRecordingStartInput input,
        CancellationToken cancellationToken = default);

    ValueTask<GameViewRecordingServiceResult<GameViewRecordingStatusPayload>> GetStatusAsync (
        GameViewRecordingStatusInput input,
        CancellationToken cancellationToken = default);

    ValueTask<GameViewRecordingServiceResult<GameViewRecordingStopResultPayload>> StopAsync (
        GameViewRecordingStopInput input,
        CancellationToken cancellationToken = default);
}
