using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class StubBuildRunIdFactory : IBuildRunIdFactory
{
    private readonly string runId;

    public StubBuildRunIdFactory (string runId)
    {
        this.runId = runId;
    }

    public string Create ()
    {
        return runId;
    }
}
