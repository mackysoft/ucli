using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Represents removal of an unregistered request artifact owned by one fresh lease. </summary>
internal sealed record GameViewRecordingArtifactDiscardResult (ExecutionError? Error)
{
    public bool IsSuccess => Error is null;

    public static GameViewRecordingArtifactDiscardResult Success () =>
        new(Error: null);

    public static GameViewRecordingArtifactDiscardResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingArtifactDiscardResult(error);
    }
}
