#nullable enable

using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation failure entry captured by phase execution. </summary>
    /// <param name="Code"> The machine-readable error code. </param>
    /// <param name="Message"> The human-readable error message. </param>
    /// <param name="OpId"> The related operation identifier, or <see langword="null" /> when unavailable. </param>
    public sealed record OperationFailure (
        UcliCodeValue Code,
        string Message,
        string? OpId);
}
