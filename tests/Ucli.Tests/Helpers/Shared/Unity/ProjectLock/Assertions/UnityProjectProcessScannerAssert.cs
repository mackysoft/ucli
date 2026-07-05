using MackySoft.Ucli.Tests.Helpers.Process;

namespace MackySoft.Ucli.Tests.Helpers.Unity;

internal static class UnityProjectProcessScannerAssert
{
    public static void UsesAbsoluteUnixPsExecutable (RecordingProcessRunner processRunner)
    {
        var request = Assert.Single(processRunner.Invocations).Request;
        Assert.StartsWith("/", request.FileName, StringComparison.Ordinal);
        Assert.EndsWith("/ps", request.FileName, StringComparison.Ordinal);
    }

    public static void UsesWindowsPowerShellProcessList (RecordingProcessRunner processRunner)
    {
        var request = Assert.Single(processRunner.Invocations).Request;
        Assert.Equal("powershell.exe", request.FileName);
        Assert.Contains("-Command", request.Arguments);
    }
}
