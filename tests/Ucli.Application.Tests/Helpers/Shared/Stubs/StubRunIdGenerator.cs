using MackySoft.Ucli.Application.Shared.Execution;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubRunIdGenerator : IRunIdGenerator
{
    private readonly Guid runId;

    public StubRunIdGenerator (Guid runId)
    {
        this.runId = runId;
    }

    public Guid Generate ()
    {
        return runId;
    }
}
