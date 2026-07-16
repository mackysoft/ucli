namespace MackySoft.Ucli.Application.Shared.Identifiers;

/// <summary> Generates non-empty collision-resistant identifiers. </summary>
internal interface IGuidGenerator
{
    /// <summary> Generates one non-empty identifier. </summary>
    Guid Generate ();
}
