namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Describes one physical Unity project lifecycle lock request. </summary>
internal sealed class ProjectLifecycleLockRequest
{
    /// <summary> Initializes a new instance of the <see cref="ProjectLifecycleLockRequest" /> class. </summary>
    /// <param name="unityProjectRoot"> The resolved Unity project root path that scopes the lifecycle lock. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="unityProjectRoot" /> is <see langword="null" />, empty, or whitespace. </exception>
    public ProjectLifecycleLockRequest (string unityProjectRoot)
    {
        if (string.IsNullOrWhiteSpace(unityProjectRoot))
        {
            throw new ArgumentException("Unity project root must not be empty.", nameof(unityProjectRoot));
        }

        UnityProjectRoot = unityProjectRoot;
    }

    /// <summary> Gets the resolved Unity project root path that scopes the lifecycle lock. </summary>
    public string UnityProjectRoot { get; }
}
