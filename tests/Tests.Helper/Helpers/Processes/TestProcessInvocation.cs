namespace MackySoft.Tests;

using System.Diagnostics;

internal sealed record TestProcessInvocation (
    string FileName,
    IReadOnlyList<string> Arguments)
{
    public ProcessStartInfo CreateStartInfo ()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        for (var i = 0; i < Arguments.Count; i++)
        {
            startInfo.ArgumentList.Add(Arguments[i]);
        }

        return startInfo;
    }
}
