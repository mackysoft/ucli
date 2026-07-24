using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary> Describes one physical Unity project lifecycle lock request. </summary>
internal sealed class ProjectLifecycleLockRequest
{
    /// <summary> Initializes a new instance of the <see cref="ProjectLifecycleLockRequest" /> class. </summary>
    /// <param name="unityProjectRoot"> The resolved Unity project root path that scopes the lifecycle lock. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityProjectRoot" /> is <see langword="null" />. </exception>
    public ProjectLifecycleLockRequest (AbsolutePath unityProjectRoot)
    {
        UnityProjectRoot = unityProjectRoot ?? throw new ArgumentNullException(nameof(unityProjectRoot));
    }

    /// <summary> Gets the resolved Unity project root path that scopes the lifecycle lock. </summary>
    public AbsolutePath UnityProjectRoot { get; }
}
