using MackySoft.Ucli.Hosting.Cli.Requests.Input;

namespace MackySoft.Tests;

internal sealed class RequestInputReaderStub : IRequestInputReader
{
    private readonly RequestInputReadResult result;

    private RequestInputReaderStub (RequestInputReadResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public static RequestInputReaderStub Success (string requestJson)
    {
        return new RequestInputReaderStub(RequestInputReadResult.Success(requestJson));
    }

    public ValueTask<RequestInputReadResult> ReadAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(result);
    }
}
