namespace MackySoft.Ucli.Application.Features.Recording.UseCases;

/// <summary>Contains one GameView recording start command after CLI input acquisition.</summary>
internal sealed record GameViewRecordingStartInput (
    AbsolutePath? ProjectPath,
    string RequestJson,
    Guid? RecordingId,
    bool Detach,
    int? TimeoutMilliseconds);

/// <summary>Contains one GameView recording status lookup.</summary>
internal sealed record GameViewRecordingStatusInput (
    AbsolutePath? ProjectPath,
    Guid? RecordingId,
    int? TimeoutMilliseconds);

/// <summary>Contains one idempotent GameView recording stop request.</summary>
internal sealed record GameViewRecordingStopInput (
    AbsolutePath? ProjectPath,
    Guid RecordingId,
    int? TimeoutMilliseconds);
