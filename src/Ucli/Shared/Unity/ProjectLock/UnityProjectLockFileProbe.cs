using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Implements Unity project lock-file probing using Unity's <c>Temp/UnityLockfile</c> marker. </summary>
internal sealed class UnityProjectLockFileProbe : IUnityProjectLockFileProbe
{
    private const string TempDirectoryName = "Temp";

    private const string UnityLockFileName = "UnityLockfile";

    /// <inheritdoc />
    public UnityProjectLockFileProbeResult Probe (AbsolutePath unityProjectRoot)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);

        var lockFilePath = ContainedPath.Create(
            unityProjectRoot,
            RootRelativePath.Parse($"{TempDirectoryName}/{UnityLockFileName}")).Target;
        try
        {
            _ = File.GetAttributes(lockFilePath.Value);
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
        catch (Exception exception) when (exception is IOException
                                         || exception is UnauthorizedAccessException)
        {
            return UnityProjectLockFileProbeResult.Failure(
                UnityProjectLockFailureMessage.CreateInspectionFailed(lockFilePath, exception));
        }
    }
}
