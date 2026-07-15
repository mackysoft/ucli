namespace MackySoft.Ucli.Application.Shared.Identifiers;

/// <summary> Generates identifiers using the system GUID source. </summary>
internal sealed class GuidGenerator : IGuidGenerator
{
    /// <inheritdoc />
    public Guid Generate ()
    {
        return Guid.NewGuid();
    }
}
