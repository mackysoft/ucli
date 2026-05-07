namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Probes Unity's project lock file before starting a Unity process for the same project. </summary>
internal interface IUnityProjectLockFileProbe
{
    /// <summary> Reads the current Unity project lock-file state. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <returns> The observed project lock-file state. </returns>
    UnityProjectLockFileProbeResult Probe (string unityProjectRoot);
}
