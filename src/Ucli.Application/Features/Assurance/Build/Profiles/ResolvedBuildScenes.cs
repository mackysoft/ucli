using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build scene selection resolved from a build profile. </summary>
internal abstract class ResolvedBuildScenes
{
    private ResolvedBuildScenes ()
    {
    }

    /// <summary> Gets the scene source represented by this variant. </summary>
    public abstract BuildProfileSceneSource Source { get; }

    /// <summary> Represents scene selection from Unity Editor Build Settings. </summary>
    public sealed class EditorBuildSettings : ResolvedBuildScenes
    {
        /// <inheritdoc />
        public override BuildProfileSceneSource Source => BuildProfileSceneSource.EditorBuildSettings;

    }

    /// <summary> Represents an explicit non-empty list of scene asset paths. </summary>
    public sealed class Explicit : ResolvedBuildScenes
    {
        /// <summary> Initializes explicit scene selection. </summary>
        public Explicit (IReadOnlyList<SceneAssetPath> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            if (paths.Count == 0)
            {
                throw new ArgumentException("Explicit build scenes must contain at least one path.", nameof(paths));
            }

            var copiedPaths = new SceneAssetPath[paths.Count];
            var seenPaths = new HashSet<SceneAssetPath>();
            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i] ?? throw new ArgumentException(
                    "Explicit build scenes must not contain a null path.",
                    nameof(paths));
                if (!seenPaths.Add(path))
                {
                    throw new ArgumentException(
                        $"Explicit build scenes contain duplicate path '{path.Value}'.",
                        nameof(paths));
                }

                copiedPaths[i] = path;
            }

            Paths = Array.AsReadOnly(copiedPaths);
        }

        /// <inheritdoc />
        public override BuildProfileSceneSource Source => BuildProfileSceneSource.Explicit;

        /// <summary> Gets the explicit scene asset paths. </summary>
        public IReadOnlyList<SceneAssetPath> Paths { get; }
    }
}
