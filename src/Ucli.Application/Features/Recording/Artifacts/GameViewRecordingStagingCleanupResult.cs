using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Artifacts;

/// <summary> Represents verified deletion of the provider-private recording output. </summary>
internal sealed record GameViewRecordingStagingCleanupResult (ExecutionError? Error)
{
    public bool IsSuccess => Error is null;

    public static GameViewRecordingStagingCleanupResult Success () =>
        new(Error: null);

    public static GameViewRecordingStagingCleanupResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingStagingCleanupResult(error);
    }
}
