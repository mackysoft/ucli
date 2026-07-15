using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one normalized BuildReport step summary. </summary>
public sealed record IpcBuildReportStep
{
    /// <summary> Initializes one normalized BuildReport step summary. </summary>
    /// <param name="Name"> The step name, which may be empty but must not be <see langword="null" />. </param>
    /// <param name="DurationMilliseconds"> The non-negative step duration in milliseconds. </param>
    /// <param name="Depth"> The non-negative BuildReport step depth. </param>
    /// <param name="MessageCount"> The non-negative number of messages attached to the step. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Name" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a numeric value is negative. </exception>
    [JsonConstructor]
    public IpcBuildReportStep (
        string Name,
        long DurationMilliseconds,
        int Depth,
        int MessageCount)
    {
        if (DurationMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DurationMilliseconds),
                DurationMilliseconds,
                "Build report step duration must not be negative.");
        }

        this.Name = ContractArgumentGuard.RequireNotNull(Name, nameof(Name));
        this.DurationMilliseconds = DurationMilliseconds;
        this.Depth = ContractArgumentGuard.RequireNonNegative(Depth, nameof(Depth));
        this.MessageCount = ContractArgumentGuard.RequireNonNegative(MessageCount, nameof(MessageCount));
    }

    public string Name { get; }

    public long DurationMilliseconds { get; }

    public int Depth { get; }

    public int MessageCount { get; }
}
