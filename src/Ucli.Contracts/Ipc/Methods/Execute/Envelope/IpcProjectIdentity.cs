using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the resolved Unity project identity attached to one execute response. </summary>
public sealed record IpcProjectIdentity
{
    /// <summary> Initializes a resolved Unity project identity. </summary>
    /// <param name="projectPath">
    /// The Unity project root path text carried by the IPC response. The receiving application
    /// validates this text against the current platform before using it as a filesystem path.
    /// </param>
    /// <param name="projectFingerprint"> The canonical project fingerprint. </param>
    /// <param name="unityVersion"> The non-empty Unity editor version, or <c>unknown</c> when unavailable. </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="projectPath" />, <paramref name="projectFingerprint" />, or <paramref name="unityVersion" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="unityVersion" /> is empty or contains outer whitespace.
    /// </exception>
    [JsonConstructor]
    public IpcProjectIdentity (
        string projectPath,
        ProjectFingerprint projectFingerprint,
        string unityVersion)
    {
        if (projectPath == null)
        {
            throw new ArgumentNullException(nameof(projectPath));
        }

        ProjectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));

        if (unityVersion == null)
        {
            throw new ArgumentNullException(nameof(unityVersion));
        }

        if (string.IsNullOrWhiteSpace(unityVersion))
        {
            throw new ArgumentException("Unity version must not be empty or whitespace.", nameof(unityVersion));
        }

        if (StringValueValidator.HasOuterWhitespace(unityVersion))
        {
            throw new ArgumentException("Unity version must not contain leading or trailing whitespace.", nameof(unityVersion));
        }

        ProjectPath = projectPath;
        UnityVersion = unityVersion;
    }

    /// <summary>
    /// Gets the Unity project root path text carried by IPC. Consumers must validate it before
    /// using it as a filesystem path.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary> Gets the canonical project fingerprint. </summary>
    public ProjectFingerprint ProjectFingerprint { get; }

    /// <summary> Gets the non-empty Unity editor version, or <c>unknown</c> when unavailable. </summary>
    public string UnityVersion { get; }
}
