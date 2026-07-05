using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Tests.Helpers.Indexing;

internal sealed class ThrowingJsonContractWriter<TContract> : IJsonContractWriter<TContract>
{
    private readonly Exception exception;

    public ThrowingJsonContractWriter (Exception exception)
    {
        this.exception = exception;
    }

    public string Write (TContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        throw exception;
    }
}
