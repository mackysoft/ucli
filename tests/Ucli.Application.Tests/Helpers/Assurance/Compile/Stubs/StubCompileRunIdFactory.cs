using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubCompileRunIdFactory : ICompileRunIdFactory
{
    private readonly string runId;

    public StubCompileRunIdFactory (string runId)
    {
        this.runId = runId;
    }

    public string Create ()
    {
        return runId;
    }
}
