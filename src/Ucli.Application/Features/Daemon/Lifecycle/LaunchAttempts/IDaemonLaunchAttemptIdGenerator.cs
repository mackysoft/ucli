namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

/// <summary> Generates path-safe daemon launch-attempt identifiers. </summary>
internal interface IDaemonLaunchAttemptIdGenerator
{
    /// <summary> Creates one launch-attempt identifier for the specified UTC timestamp. </summary>
    /// <param name="startedAtUtc"> The launch-attempt start timestamp. </param>
    /// <returns> The generated launch-attempt identifier. </returns>
    string Create (DateTimeOffset startedAtUtc);
}
