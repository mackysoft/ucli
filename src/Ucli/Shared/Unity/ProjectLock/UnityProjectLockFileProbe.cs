using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Implements Unity project lock-file probing using Unity's <c>Temp/UnityLockfile</c> marker. </summary>
internal sealed class UnityProjectLockFileProbe : IUnityProjectLockFileProbe
{
    private const string TempDirectoryName = "Temp";

    private const string UnityLockFileName = "UnityLockfile";

    /// <inheritdoc />
    public UnityProjectLockFileProbeResult Probe (string unityProjectRoot)
    {
        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            return UnityProjectLockFileProbeResult.Failure("Unity project root must not be empty.");
        }

        try
        {
            var lockFilePath = Path.Combine(unityProjectRoot, TempDirectoryName, UnityLockFileName);
            return File.Exists(lockFilePath)
                ? UnityProjectLockFileProbeResult.Locked(lockFilePath)
                : UnityProjectLockFileProbeResult.Unlocked(lockFilePath);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityProjectLockFileProbeResult.Failure(
                $"Unity project lock-file path is invalid. {exception.Message}");
        }
    }
}
