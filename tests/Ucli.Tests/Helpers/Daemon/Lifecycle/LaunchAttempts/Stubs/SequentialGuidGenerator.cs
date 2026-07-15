namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal sealed class SequentialGuidGenerator : IGuidGenerator
{
    private int sequence;

    public Guid Generate ()
    {
        sequence++;
        return new Guid(sequence, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
