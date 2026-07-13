namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Generates non-empty identifiers for command runs. </summary>
internal interface IRunIdGenerator
{
    /// <summary> Generates a non-empty run identifier. </summary>
    Guid Generate ();
}
