using MackySoft.FileSystem;

namespace MackySoft.Ucli.Hosting.Cli.Recording.Input;

/// <summary>Reads one GameView recording request from a file or redirected standard input.</summary>
internal interface IGameViewRecordingRequestInputReader
{
    /// <summary>Reads exactly one input source: <paramref name="requestPath"/> or redirected standard input.</summary>
    ValueTask<GameViewRecordingRequestInputReadResult> ReadAsync (
        AbsolutePath? requestPath,
        CancellationToken cancellationToken = default);
}
