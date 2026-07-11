namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;

/// <summary> Creates collision-resistant compile run identifiers. </summary>
internal sealed class CompileRunIdFactory : ICompileRunIdFactory
{
    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="CompileRunIdFactory" /> class. </summary>
    public CompileRunIdFactory (TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Create ()
    {
        return TimestampedExecutionId.Create(timeProvider.GetUtcNow());
    }
}
