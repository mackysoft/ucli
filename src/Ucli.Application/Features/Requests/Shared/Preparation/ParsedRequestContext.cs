using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Represents one request that has been read and parsed without project binding. </summary>
/// <param name="RequestJson"> The normalized internal execute-request JSON string. </param>
/// <param name="Request"> The parsed request model. </param>
internal sealed record ParsedRequestContext (
    string RequestJson,
    ValidateRequest Request);
