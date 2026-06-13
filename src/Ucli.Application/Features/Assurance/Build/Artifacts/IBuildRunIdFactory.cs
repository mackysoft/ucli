namespace MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;

/// <summary> Creates build run identifiers. </summary>
internal interface IBuildRunIdFactory
{
    /// <summary> Creates one build run identifier. </summary>
    string Create ();
}
