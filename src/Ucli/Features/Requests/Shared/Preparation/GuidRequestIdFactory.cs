namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

/// <summary> Creates request identifiers using random GUID values. </summary>
internal sealed class GuidRequestIdFactory : IRequestIdFactory
{
    /// <inheritdoc />
    public string Create ()
    {
        return Guid.NewGuid().ToString("D");
    }
}
