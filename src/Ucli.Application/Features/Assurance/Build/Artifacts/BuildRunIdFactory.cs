namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Creates collision-resistant build run identifiers. </summary>
internal sealed class BuildRunIdFactory : IBuildRunIdFactory
{
    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="BuildRunIdFactory" /> class. </summary>
    public BuildRunIdFactory (TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Create ()
    {
        return TimestampedExecutionId.Create(timeProvider.GetUtcNow());
    }
}
