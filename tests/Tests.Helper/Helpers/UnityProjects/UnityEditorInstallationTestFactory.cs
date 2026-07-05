namespace MackySoft.Tests;

/// <summary> Provides helper methods for creating Unity Editor installation layouts in tests. </summary>
internal static class UnityEditorInstallationTestFactory
{
    internal static string WriteEditorExecutable (
        TestDirectoryScope scope,
        string rootRelativePath,
        string unityVersion)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootRelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);

        var versionRelativePath = Path.Combine(rootRelativePath, unityVersion);
        return scope.WriteFile(Path.Combine(versionRelativePath, "Editor", "Unity.exe"), string.Empty);
    }
}
