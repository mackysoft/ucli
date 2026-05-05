namespace MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

/// <summary> Represents the successful or partially successful output of one <c>validate</c> execution. </summary>
/// <param name="ReadIndex"> The emitted <c>payload.readIndex</c> metadata. </param>
internal sealed record ValidateExecutionOutput (
    ReadIndexInfo ReadIndex);
