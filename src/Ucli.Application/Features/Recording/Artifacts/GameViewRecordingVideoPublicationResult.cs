using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Represents validation and immutable publication of one finalized recording video. </summary>
internal sealed record GameViewRecordingVideoPublicationResult
{
    private GameViewRecordingVideoPublicationResult (
        GameViewRecordingVideoPublication? publication,
        ExecutionError? error)
    {
        Publication = publication;
        Error = error;
    }

    public GameViewRecordingVideoPublication? Publication { get; }

    public ExecutionError? Error { get; }

    public bool IsSuccess => Publication is not null;

    public static GameViewRecordingVideoPublicationResult Success (
        GameViewRecordingVideoPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        return new GameViewRecordingVideoPublicationResult(publication, error: null);
    }

    public static GameViewRecordingVideoPublicationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingVideoPublicationResult(publication: null, error);
    }
}
