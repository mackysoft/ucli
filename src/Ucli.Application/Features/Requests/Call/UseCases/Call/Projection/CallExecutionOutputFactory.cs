using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Requests.Call.UseCases.Call.Projection;

/// <summary> Builds call command payloads from prepared request state. </summary>
internal static class CallExecutionOutputFactory
{
    /// <summary> Creates the base call payload when the request identifier is available. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <returns> The base payload. </returns>
    public static CallExecutionOutput CreateBase (string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        return new CallExecutionOutput(
            RequestId: requestId,
            OpResults: [],
            Plan: null,
            ReadPostcondition: null);
    }

    /// <summary> Tries to create the base call payload for failure projection. </summary>
    /// <param name="requestId"> The request identifier when available. </param>
    /// <returns> The base payload, or <see langword="null" /> when the request identifier is unavailable. </returns>
    public static CallExecutionOutput? TryCreateBase (string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        return CreateBase(requestId);
    }
}
