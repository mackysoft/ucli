using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Recording.Requests;

/// <summary>Contains either one validated request document or a structured input error.</summary>
internal sealed record GameViewRecordingRequestParseResult
{
    private GameViewRecordingRequestParseResult (
        GameViewRecordingRequestDocument? request,
        ExecutionError? error)
    {
        Request = request;
        Error = error;
    }

    public bool IsSuccess => Request is not null;

    public GameViewRecordingRequestDocument? Request { get; }

    public ExecutionError? Error { get; }

    public static GameViewRecordingRequestParseResult Success (
        GameViewRecordingRequestDocument request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new GameViewRecordingRequestParseResult(request, error: null);
    }

    public static GameViewRecordingRequestParseResult Failure (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new GameViewRecordingRequestParseResult(
            request: null,
            ExecutionError.InvalidArgument(
                message,
                UcliCoreErrorCodes.InvalidArgument));
    }
}
