using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

namespace MackySoft.Ucli.Application.Tests;

internal static class UnityRequestExecutionResultTestFactory
{
    public static UnityRequestExecutionResult Failure (
        string message,
        string errorCode)
    {
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            errorCode,
            message,
            RequestServiceResultPolicy.ResolveOutcome(errorCode)));
    }
}
