using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Represents one request that has been read, parsed, and bound to project context. </summary>
internal sealed record PreparedRequestContext
{
    /// <summary> Initializes a prepared request and its command correlation identity. </summary>
    /// <param name="requestJson"> The normalized internal execute-request JSON string. </param>
    /// <param name="request"> The parsed request model. </param>
    /// <param name="projectContext"> The resolved project/config context. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="requestJson" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> or <paramref name="projectContext" /> is <see langword="null" />. </exception>
    public PreparedRequestContext (
        string requestJson,
        ValidateRequest request,
        ProjectContext projectContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(projectContext);

        RequestJson = requestJson;
        Request = request;
        ProjectContext = projectContext;
    }

    /// <summary> Gets the normalized execute-request JSON. </summary>
    public string RequestJson { get; }

    /// <summary> Gets the parsed request model. </summary>
    public ValidateRequest Request { get; init; }

    /// <summary> Gets the resolved project and configuration context. </summary>
    public ProjectContext ProjectContext { get; }
}
