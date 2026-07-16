namespace MackySoft.Ucli.Contracts.Execution;

/// <summary> Identifies one operating-system process generation within the current system boot. </summary>
public sealed record ProcessIdentity
{
    /// <summary> Initializes one validated operating-system process identity. </summary>
    /// <param name="ProcessId"> The positive operating-system process identifier. </param>
    /// <param name="Generation"> The non-zero, operating-system-specific process generation value used only for equality. </param>
    /// <exception cref="ArgumentOutOfRangeException"> <paramref name="ProcessId" /> or <paramref name="Generation" /> is not positive. </exception>
    public ProcessIdentity (
        int ProcessId,
        ulong Generation)
    {
        this.ProcessId = ContractArgumentGuard.RequirePositive(ProcessId, nameof(ProcessId));
        if (Generation == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Generation),
                Generation,
                "Process generation must be positive.");
        }

        this.Generation = Generation;
    }

    /// <summary> Gets the positive operating-system process identifier. </summary>
    public int ProcessId { get; }

    /// <summary> Gets the operating-system-specific process generation value used only for equality. </summary>
    public ulong Generation { get; }
}
