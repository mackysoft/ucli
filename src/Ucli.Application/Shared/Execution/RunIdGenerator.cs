namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Generates collision-resistant identifiers for command runs. </summary>
internal sealed class RunIdGenerator : IRunIdGenerator
{
    /// <inheritdoc />
    public Guid Generate ()
    {
        return Guid.NewGuid();
    }
}
