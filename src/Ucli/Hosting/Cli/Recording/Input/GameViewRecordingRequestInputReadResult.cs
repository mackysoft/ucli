using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Recording.Input;

/// <summary>Contains either validated JSON text or a structured input error.</summary>
internal sealed record GameViewRecordingRequestInputReadResult
{
    private GameViewRecordingRequestInputReadResult (string? json, ExecutionError? error)
    {
        Json = json;
        Error = error;
    }

    public bool IsSuccess => Json is not null;

    public string? Json { get; }

    public ExecutionError? Error { get; }

    public static GameViewRecordingRequestInputReadResult Success (string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return new GameViewRecordingRequestInputReadResult(json, error: null);
    }

    public static GameViewRecordingRequestInputReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new GameViewRecordingRequestInputReadResult(json: null, error);
    }
}
