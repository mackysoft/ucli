using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Shared.Configuration;

namespace MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;

/// <summary> Represents one preflight-prepared request ready for phase execution. </summary>
/// <param name="PreparedRequest"> The request that has been read, parsed, and bound to project context. </param>
/// <param name="OperationsByName"> The authoritative operation metadata keyed by operation name. </param>
internal sealed record PhaseExecutionPreparedRequest (
    PreparedRequestContext PreparedRequest,
    IReadOnlyDictionary<string, UcliOperationDescriptor> OperationsByName)
{
    /// <summary> Gets the normalized internal execute-request JSON string. </summary>
    public string RequestJson => PreparedRequest.RequestJson;

    /// <summary> Gets the request input source. </summary>
    public RequestInputSource InputSource => PreparedRequest.InputSource;

    /// <summary> Gets the parsed request model used for static validation. </summary>
    public ValidateRequest Request => PreparedRequest.Request;

    /// <summary> Gets the resolved Unity project context. </summary>
    public ResolvedUnityProjectContext UnityProject => PreparedRequest.ProjectContext.UnityProject;

    /// <summary> Gets the loaded configuration values. </summary>
    public UcliConfig Config => PreparedRequest.ProjectContext.Config;

    /// <summary> Gets the configuration source. </summary>
    public ConfigSource ConfigSource => PreparedRequest.ProjectContext.ConfigSource;
}
