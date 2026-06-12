using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the syntax contract for scene paths declared by build profiles. </summary>
public static class BuildProfileScenePathContract
{
    /// <summary> Gets the project-relative root required for build scene asset paths. </summary>
    public const string AssetsRootPrefix = "Assets/";

    /// <summary> Gets the Unity scene asset extension required for build scene asset paths. </summary>
    public const string SceneAssetExtension = ".unity";

    /// <summary> Determines whether <paramref name="path" /> is a normalized project-relative Unity scene asset path. </summary>
    /// <param name="path"> The path to inspect. </param>
    /// <returns> <see langword="true" /> when the path satisfies the build profile scene path syntax contract; otherwise <see langword="false" />. </returns>
    public static bool IsProjectRelativeSceneAssetPath (string? path)
    {
        if (path == null
            || path.Length == 0
            || StringValueValidator.HasOuterWhitespace(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || IsWindowsDriveQualifiedPath(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || !path.StartsWith(AssetsRootPrefix, StringComparison.Ordinal)
            || !path.EndsWith(SceneAssetExtension, StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0
                || string.Equals(segment, ".", StringComparison.Ordinal)
                || string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWindowsDriveQualifiedPath (string path)
    {
        return path.Length >= 2
            && IsAsciiLetter(path[0])
            && path[1] == ':';
    }

    private static bool IsAsciiLetter (char value)
    {
        return value is (>= 'A' and <= 'Z')
            or (>= 'a' and <= 'z');
    }
}
