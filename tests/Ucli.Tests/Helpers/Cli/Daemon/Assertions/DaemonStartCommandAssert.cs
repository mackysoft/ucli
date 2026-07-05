namespace MackySoft.Tests;

internal static class DaemonStartCommandAssert
{
    public static void InvalidArgumentReturnedWithoutStartExecution (
        CommandExecutionResult result,
        RecordingDaemonStartService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailure(
            result,
            service.Invocations,
            UcliCommandNames.DaemonStart);
    }

    public static void InvalidArgumentReturnedWithoutStartExecutionAndStandardError (
        CommandExecutionResult result,
        RecordingDaemonStartService service)
    {
        CommandResultAssert.HasPreDispatchInvalidArgumentFailureWithEmptyStandardError(
            result,
            service.Invocations,
            UcliCommandNames.DaemonStart);
    }
}
