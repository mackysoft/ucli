namespace MackySoft.Ucli.UnityProject
{
    /// <summary> Represents a resolved UnityProject context shared by command foundation services. </summary>
    /// <param name="UnityProjectRoot"> The normalized absolute UnityProject root path. </param>
    /// <param name="ProjectFingerprint"> The deterministic fingerprint for project identity checks. </param>
    /// <param name="PathSource"> The path source used during resolution. </param>
    /// <param name="ConfigPath"> The absolute path to <c>.ucli/config.json</c>. </param>
    internal sealed record ResolvedUnityProjectContext (
        string UnityProjectRoot,
        string ProjectFingerprint,
        UnityProjectPathSource PathSource,
        string ConfigPath);
}
