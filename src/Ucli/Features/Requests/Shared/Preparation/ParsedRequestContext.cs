using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;

namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

/// <summary> Represents one request that has been read and parsed without project binding. </summary>
/// <param name="RequestJson"> The raw request JSON string. </param>
/// <param name="InputSource"> The input source where request JSON was read. </param>
/// <param name="Request"> The parsed request model. </param>
internal sealed record ParsedRequestContext (
    string RequestJson,
    RequestInputSource InputSource,
    ValidateRequest Request);