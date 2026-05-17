namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Creates compile run identifiers. </summary>
internal interface ICompileRunIdFactory
{
    /// <summary> Creates one compile run identifier. </summary>
    string Create ();
}
