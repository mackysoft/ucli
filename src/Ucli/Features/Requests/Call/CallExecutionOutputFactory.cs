namespace MackySoft.Ucli.Features.Requests.Call;

/// <summary> Builds call command payloads from prepared request state. </summary>
internal static class CallExecutionOutputFactory
{
    /// <summary> Creates the base call payload when the request identifier is available. </summary>
    /// <param name="requestId"> The request identifier. </param>
    /// <returns> The base payload, or <see langword="null" /> when the request identifier is unavailable. </returns>
    public static CallExecutionOutput? CreateBase (string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return null;
        }

        return new CallExecutionOutput(
            RequestId: requestId,
            OpResults: [],
            Plan: null);
    }
}