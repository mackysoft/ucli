using System.Text;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Requests;

/// <summary>Applies adapter limits and creates the immutable effective request digest.</summary>
internal static class GameViewRecordingRequestNormalizer
{
    public static GameViewRecordingRequestNormalizationResult Normalize (
        GameViewRecordingRequestDocument request,
        int minimumWidth,
        int maximumWidth,
        int minimumHeight,
        int maximumHeight,
        int dimensionMultiple,
        int minimumFrameRate,
        int maximumFrameRate,
        int defaultMaxDurationSeconds,
        int maximumMaxDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (dimensionMultiple <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dimensionMultiple),
                dimensionMultiple,
                "Recording dimension multiple must be positive.");
        }

        if (request.Resolution.Width < minimumWidth
            || request.Resolution.Width > maximumWidth
            || request.Resolution.Height < minimumHeight
            || request.Resolution.Height > maximumHeight)
        {
            return GameViewRecordingRequestNormalizationResult.Failure(
                "Requested recording resolution is outside the adapter limits.");
        }

        if (request.Resolution.Width % dimensionMultiple != 0
            || request.Resolution.Height % dimensionMultiple != 0)
        {
            return GameViewRecordingRequestNormalizationResult.Failure(
                "Requested recording resolution does not satisfy the adapter dimension multiple.");
        }

        if (request.FrameRate < minimumFrameRate
            || request.FrameRate > maximumFrameRate)
        {
            return GameViewRecordingRequestNormalizationResult.Failure(
                "Requested recording frameRate is outside the adapter limits.");
        }

        var maxDurationSeconds = request.MaxDurationSeconds.HasValue
            ? request.MaxDurationSeconds.Value
            : defaultMaxDurationSeconds;
        if (maxDurationSeconds <= 0
            || maxDurationSeconds > maximumMaxDurationSeconds)
        {
            return GameViewRecordingRequestNormalizationResult.Failure(
                "Requested maxDurationSeconds is outside the adapter limits.");
        }

        var effectiveContract = new GameViewRecordingRequest(
            GameViewRecordingRequest.CurrentSchemaVersion,
            request.Resolution,
            request.FrameRate,
            maxDurationSeconds);
        var json = JsonSerializer.SerializeToElement(
            effectiveContract,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var canonicalBytes = Rfc8785JsonCanonicalizer.Canonicalize(json);
        var canonicalJson = Encoding.UTF8.GetString(canonicalBytes);
        return GameViewRecordingRequestNormalizationResult.Success(
            new GameViewRecordingEffectiveRequest(
                effectiveContract.SchemaVersion,
                effectiveContract.Resolution,
                effectiveContract.FrameRate,
                effectiveContract.MaxDurationSeconds,
                canonicalJson,
                Sha256Digest.Compute(canonicalBytes)));
    }
}

/// <summary>Contains either an effective request or a structured limit failure.</summary>
internal sealed record GameViewRecordingRequestNormalizationResult
{
    private GameViewRecordingRequestNormalizationResult (
        GameViewRecordingEffectiveRequest? request,
        ExecutionError? error)
    {
        Request = request;
        Error = error;
    }

    public bool IsSuccess => Request is not null;

    public GameViewRecordingEffectiveRequest? Request { get; }

    public ExecutionError? Error { get; }

    public static GameViewRecordingRequestNormalizationResult Success (
        GameViewRecordingEffectiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new GameViewRecordingRequestNormalizationResult(request, error: null);
    }

    public static GameViewRecordingRequestNormalizationResult Failure (string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new GameViewRecordingRequestNormalizationResult(
            request: null,
            ExecutionError.InvalidArgument(
                message,
                UcliCoreErrorCodes.InvalidArgument));
    }
}
