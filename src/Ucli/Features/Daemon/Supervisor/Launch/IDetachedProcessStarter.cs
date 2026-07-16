using System.Diagnostics;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Starts one detached process and transfers its local process handle to the caller. </summary>
internal interface IDetachedProcessStarter
{
    /// <summary> Starts one process from the specified start information. </summary>
    /// <param name="startInfo"> The complete detached-process start information. </param>
    /// <returns> The owned process handle, or <see langword="null" /> when process creation did not return one. </returns>
    IDetachedProcessHandle? Start (ProcessStartInfo startInfo);
}
