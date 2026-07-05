namespace MackySoft.Ucli.Tests.Helpers.Process;

internal static class UnityLogReaderAssert
{
    public static void LogInspected (RecordingUnityLogReader logReader)
    {
        Assert.NotEmpty(logReader.Invocations);
    }
}
