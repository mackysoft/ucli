namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one validation error entry for static request validation. </summary>
/// <param name="Code"> The machine-readable validation code. </param>
/// <param name="Message"> The validation detail message. </param>
/// <param name="InstancePath"> The RFC 6901 JSON Pointer locating the invalid request value, or <see langword="null" /> when the failure is not tied to JSON input. </param>
internal sealed record ValidationError
{
    public ValidationError (
        UcliCode Code,
        string Message,
        string? InstancePath)
    {
        this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
        ArgumentException.ThrowIfNullOrWhiteSpace(Message);
        this.Message = Message;
        if (InstancePath is not null && (InstancePath.Length == 0 || InstancePath[0] != '/'))
        {
            throw new ArgumentException(
                "Instance path must be a non-root RFC 6901 JSON Pointer.",
                nameof(InstancePath));
        }

        this.InstancePath = InstancePath;
    }

    public UcliCode Code { get; }

    public string Message { get; }

    public string? InstancePath { get; }
}
