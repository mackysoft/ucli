using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Tests.Helpers.Cli;

internal sealed class StubRequestIdFactory : IRequestIdFactory
{
    private readonly string requestId;

    public StubRequestIdFactory (string requestId)
    {
        this.requestId = requestId;
    }

    public string Create ()
    {
        return requestId;
    }
}
