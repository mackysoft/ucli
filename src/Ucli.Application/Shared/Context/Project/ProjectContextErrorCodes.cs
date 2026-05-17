namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Defines machine-readable error codes emitted by project-context resolution. </summary>
internal static class ProjectContextErrorCodes
{
    /// <summary> Gets the error code used when a project path cannot be normalized. </summary>
    public static readonly UcliCode ProjectPathInvalidFormat = new("PROJECT_PATH_INVALID_FORMAT");

    /// <summary> Gets the error code used when a project path does not exist. </summary>
    public static readonly UcliCode ProjectPathNotFound = new("PROJECT_PATH_NOT_FOUND");

    /// <summary> Gets the error code used when a directory is missing required Unity project markers. </summary>
    public static readonly UcliCode UnityProjectMarkerMissing = new("UNITY_PROJECT_MARKER_MISSING");

    /// <summary> Gets the error codes owned by project-context resolution. </summary>
    public static IReadOnlyCollection<UcliCode> All { get; } =
    [
        ProjectPathInvalidFormat,
        ProjectPathNotFound,
        UnityProjectMarkerMissing,
    ];

    /// <summary> Gets a value indicating whether <paramref name="errorCode" /> is a project-context invalid-argument code. </summary>
    /// <param name="errorCode"> The machine-readable error code. </param>
    /// <returns> <see langword="true" /> when the code belongs to project-context resolution; otherwise <see langword="false" />. </returns>
    public static bool Contains (UcliCode? errorCode)
    {
        return errorCode.HasValue && All.Contains(errorCode.Value);
    }
}
