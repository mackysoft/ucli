namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents a resolved UnityProject context shared by command foundation services. </summary>
/// <param name="UnityProjectRoot"> The normalized absolute UnityProject root path. </param>
/// <param name="RepositoryRoot"> The normalized absolute repository root path used for <c>.ucli</c> storage. </param>
/// <param name="ProjectFingerprint"> The deterministic fingerprint for project identity checks. </param>
/// <param name="PathSource"> The path source used during resolution. </param>
/// <param name="PathSourceLabel"> The optional label for the source used during resolution. </param>
/// <param name="UnityVersion"> The Unity editor version resolved from <c>ProjectSettings/ProjectVersion.txt</c>, or <c>unknown</c>. </param>
internal sealed record ResolvedUnityProjectContext (
    string UnityProjectRoot,
    string RepositoryRoot,
    string ProjectFingerprint,
    UnityProjectPathSource PathSource,
    string? PathSourceLabel = null,
    string UnityVersion = ProjectIdentityDefaults.UnknownUnityVersion);
