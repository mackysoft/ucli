using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Represents one preflight-prepared request ready for phase execution. </summary>
/// <param name="RequestJson"> The raw request JSON string. </param>
/// <param name="InputSource"> The input source where request JSON was read. </param>
/// <param name="Request"> The parsed request model used for static validation. </param>
/// <param name="UnityProject"> The resolved Unity project context. </param>
/// <param name="Config"> The loaded configuration values. </param>
/// <param name="ConfigSource"> The config source where <paramref name="Config" /> was resolved. </param>
internal sealed record PhaseExecutionPreparedRequest (
    string RequestJson,
    RequestInputSource InputSource,
    ValidateRequest Request,
    ResolvedUnityProjectContext UnityProject,
    UcliConfig Config,
    ConfigSource ConfigSource);