namespace MackySoft.Ucli.Application.Tests;

internal static class UnityRequestExecutionResultTestFactory
{
    public static UnityRequestExecutionResult Failure (
        string message,
        UcliCodeValue errorCode)
    {
        return UnityRequestExecutionResult.Failure(new UnityRequestFailure(
            errorCode,
            message));
    }
}
