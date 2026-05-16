namespace MackySoft.Ucli.Tests;

internal static class ProjectIdentityInfoTestFactory
{
    public const string ProjectFingerprint = "project-fingerprint";

    public const string UnityVersion = "6000.1.4f1";

    public static string DefaultProjectPath { get; } = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ucli-tests",
        "UnityProject"));

    public static ProjectIdentityInfo Create ()
    {
        return new ProjectIdentityInfo(
            ProjectPath: DefaultProjectPath,
            ProjectFingerprint: ProjectFingerprint,
            UnityVersion: UnityVersion);
    }
}
