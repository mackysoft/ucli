namespace MackySoft.Ucli.Tests.Helpers.Ipc;

internal sealed class CallerDisconnectingMemoryStream : MemoryStream
{
    public override async ValueTask<int> ReadAsync (
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (Position < Length)
        {
            return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }
}
