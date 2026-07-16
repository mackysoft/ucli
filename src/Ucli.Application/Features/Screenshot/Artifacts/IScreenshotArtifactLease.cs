using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Screenshot.Artifacts;

/// <summary>
/// Owns the staging and commit lifecycle for one screenshot capture.
/// The caller must terminate every prepared lease with <see cref="Discard" /> after the capture attempt,
/// regardless of commit success, failure, or exception.
/// </summary>
internal interface IScreenshotArtifactLease
{
    /// <summary> Validates one raw staging image and atomically commits its PNG artifact. </summary>
    /// <param name="staging"> The captured raw-image contract returned by Unity. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The committed artifact reference, or a structured commit error. </returns>
    ValueTask<ScreenshotArtifactCommitResult> CommitAsync (
        IpcScreenshotStagingImage staging,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates this capture lease by discarding its prepared staging layout.
    /// Calling this operation more than once is safe and never deletes a committed PNG artifact.
    /// </summary>
    /// <returns> The discard result. </returns>
    ScreenshotArtifactDiscardResult Discard ();
}
