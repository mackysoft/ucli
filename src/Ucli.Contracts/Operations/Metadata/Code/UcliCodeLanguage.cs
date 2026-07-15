using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Identifies the source language described by an operation code contract. </summary>
public enum UcliCodeLanguage
{
    /// <summary> Indicates C# source code. </summary>
    [UcliContractLiteral("csharp")]
    CSharp = 1,
}
