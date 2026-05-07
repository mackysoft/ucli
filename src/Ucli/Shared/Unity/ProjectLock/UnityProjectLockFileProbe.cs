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
            try
            {
                _ = File.GetAttributes(lockFilePath);
                return UnityProjectLockFileProbeResult.Locked(lockFilePath);
            }
            catch (FileNotFoundException)
            {
                return UnityProjectLockFileProbeResult.Unlocked(lockFilePath);
            }
            catch (DirectoryNotFoundException)
            {
                return UnityProjectLockFileProbeResult.Unlocked(lockFilePath);
            }
            catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception)
                                             || exception is IOException
                                             || exception is UnauthorizedAccessException)
            {
                return UnityProjectLockFileProbeResult.Failure(
                    UnityProjectLockFailureMessage.CreateInspectionFailed(lockFilePath, exception));
            }
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return UnityProjectLockFileProbeResult.Failure(
                $"Unity project lock-file path is invalid. {exception.Message}");
        }
    }
}
