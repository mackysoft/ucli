using MackySoft.Ucli.Application.Shared.Context.Project;

namespace MackySoft.Ucli.Hosting.Cli.Common.Projection;

/// <summary> Projects resolved project identity into the public CLI payload shape. </summary>
internal static class ProjectIdentityPayloadProjector
{
    /// <summary> Creates the public <c>payload.project</c> object. </summary>
    /// <param name="project"> The resolved project identity. </param>
    /// <returns> The JSON-serializable project identity payload. </returns>
    public static object Create (ProjectIdentityInfo project)
    {
        ArgumentNullException.ThrowIfNull(project);

        return new
        {
            projectPath = project.ProjectPath,
            projectFingerprint = project.ProjectFingerprint,
            unityVersion = project.UnityVersion,
        };
    }
}
