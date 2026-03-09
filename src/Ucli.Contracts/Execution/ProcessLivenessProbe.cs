using System.Diagnostics;

namespace MackySoft.Ucli.Contracts.Execution;

/// <summary> Probes whether one operating-system process identifier is still alive. </summary>
internal static class ProcessLivenessProbe
{
    /// <summary> Gets whether the specified process identifier still points to a live process. </summary>
    /// <param name="processId"> The operating-system process identifier. </param>
    /// <returns> <see langword="true" /> when the process exists and has not exited; otherwise <see langword="false" />. </returns>
    internal static bool IsAlive (int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            try
            {
                return !process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}