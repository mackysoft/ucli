using System.Diagnostics;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Starts detached processes through the operating-system process API. </summary>
internal sealed class DetachedProcessStarter : IDetachedProcessStarter
{
    /// <inheritdoc />
    public IDetachedProcessHandle? Start (ProcessStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var process = Process.Start(startInfo);
        return process is null
            ? null
            : new DetachedProcessHandle(process);
    }
}
