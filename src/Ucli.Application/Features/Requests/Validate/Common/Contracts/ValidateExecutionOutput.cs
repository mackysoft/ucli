namespace MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

/// <summary> Represents the successful or partially successful output of one <c>validate</c> execution. </summary>
/// <param name="Project"> The resolved Unity project identity. </param>
/// <param name="ReadIndex"> The emitted <c>payload.readIndex</c> metadata. </param>
internal sealed record ValidateExecutionOutput (
    ProjectIdentityInfo Project,
    ReadIndexInfo ReadIndex)
{
    /// <summary> Initializes a new instance of the <see cref="ValidateExecutionOutput" /> record. </summary>
    public ValidateExecutionOutput (ReadIndexInfo ReadIndex)
        : this(ProjectIdentityInfo.Unknown, ReadIndex)
    {
    }
}
