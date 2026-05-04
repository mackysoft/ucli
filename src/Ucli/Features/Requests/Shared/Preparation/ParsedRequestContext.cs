using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

/// <summary> Represents one request that has been read and parsed without project binding. </summary>
/// <param name="RequestJson"> The raw request JSON string. </param>
/// <param name="Request"> The parsed request model. </param>
internal sealed record ParsedRequestContext (
    string RequestJson,
    ValidateRequest Request);
