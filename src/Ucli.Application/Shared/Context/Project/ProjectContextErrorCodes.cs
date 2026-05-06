namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Defines machine-readable error codes emitted by project-context resolution. </summary>
internal static class ProjectContextErrorCodes
{
    /// <summary> Gets the error code used when a project path cannot be normalized. </summary>
    public const string ProjectPathInvalidFormat = "PROJECT_PATH_INVALID_FORMAT";

    /// <summary> Gets the error code used when a project path does not exist. </summary>
    public const string ProjectPathNotFound = "PROJECT_PATH_NOT_FOUND";

    /// <summary> Gets the error code used when a directory is missing required Unity project markers. </summary>
    public const string UnityProjectMarkerMissing = "UNITY_PROJECT_MARKER_MISSING";

    /// <summary> Gets a value indicating whether <paramref name="errorCode" /> is a project-context invalid-argument code. </summary>
    /// <param name="errorCode"> The machine-readable error code. </param>
    /// <returns> <see langword="true" /> when the code belongs to project-context resolution; otherwise <see langword="false" />. </returns>
    public static bool Contains (string? errorCode)
    {
        return errorCode is ProjectPathInvalidFormat
            or ProjectPathNotFound
            or UnityProjectMarkerMissing;
    }
}
