using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Identifies an accepted source form for an operation that compiles source code. </summary>
public enum UcliCodeSourceFormKind
{
    /// <summary> Indicates a complete C# compilation unit. </summary>
    [UcliContractLiteral("compilationUnit")]
    CompilationUnit = 1,

    /// <summary> Indicates a generated entry-point method body. </summary>
    [UcliContractLiteral("snippet")]
    Snippet = 2,
}
