namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the resolved Unity project identity attached to one execute response. </summary>
/// <param name="ProjectPath"> The normalized absolute Unity project root path. </param>
/// <param name="ProjectFingerprint"> The resolved Unity project fingerprint. </param>
/// <param name="UnityVersion"> The Unity editor version resolved for the project, or <c>unknown</c> when unavailable. </param>
public sealed record IpcProjectIdentity (
    string ProjectPath,
    string ProjectFingerprint,
    string UnityVersion)
{
    /// <summary> Gets a sentinel project identity for tests and legacy in-process construction. </summary>
    public static IpcProjectIdentity Unknown { get; } = new(
        ProjectPath: "unknown",
        ProjectFingerprint: "unknown",
        UnityVersion: "unknown");
}
