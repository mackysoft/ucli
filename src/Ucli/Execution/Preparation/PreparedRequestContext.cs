using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Execution;

/// <summary> Represents one request that has been read, parsed, and bound to project context. </summary>
/// <param name="RequestJson"> The raw request JSON string. </param>
/// <param name="InputSource"> The input source where request JSON was read. </param>
/// <param name="Request"> The parsed request model. </param>
/// <param name="ProjectContext"> The resolved project/config context. </param>
internal sealed record PreparedRequestContext (
    string RequestJson,
    RequestInputSource InputSource,
    ValidateRequest Request,
    ProjectContext ProjectContext);