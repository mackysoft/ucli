using MackySoft.Ucli.Shared.Execution.Process;

namespace MackySoft.Ucli.Shared.Unity.Process;

/// <summary> Provides Unity-specific process termination policies. </summary>
internal static class UnityProcessTerminationPolicy
{
    /// <summary> Gets the Unity policy that gives Unity a short cleanup window before hard kill. </summary>
    public static ProcessTerminationPolicy GracefulThenKill { get; } = new(
        ProcessTerminationMode.GracefulThenKill,
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(5));
}
