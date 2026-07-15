using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Identifies how a C# eval return value is represented. </summary>
public enum CsEvalReturnValueKind
{
    /// <summary> Indicates that the evaluated entry point returned <see langword="null" />. </summary>
    [UcliContractLiteral("null")]
    Null = 1,

    /// <summary> Indicates that the evaluated entry point returned a JSON value. </summary>
    [UcliContractLiteral("json")]
    Json = 2,
}
