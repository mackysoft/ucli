namespace MackySoft.Ucli.Application.Tests;

internal sealed class StaticGuidGenerator : IGuidGenerator
{
    private readonly Guid value;

    public StaticGuidGenerator (Guid value)
    {
        this.value = value;
    }

    public Guid Generate ()
    {
        return value;
    }
}
