namespace MackySoft.Ucli.TestSupport;

internal sealed class UnexpectedGuidGenerator : IGuidGenerator
{
    public Guid Generate ()
    {
        throw new InvalidOperationException(
            "A reconnection attempt must not generate a new Lifecycle Execution identifier.");
    }
}
