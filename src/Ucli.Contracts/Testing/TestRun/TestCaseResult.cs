using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Defines the finite outcomes reported for one completed test case. </summary>
public enum TestCaseResult
{
    /// <summary> The test case passed. </summary>
    [UcliContractLiteral("pass")]
    Pass = 1,

    /// <summary> The test case failed. </summary>
    [UcliContractLiteral("fail")]
    Fail = 2,

    /// <summary> The test case was skipped. </summary>
    [UcliContractLiteral("skipped")]
    Skipped = 3,

    /// <summary> The test case did not produce a conclusive result. </summary>
    [UcliContractLiteral("inconclusive")]
    Inconclusive = 4,
}
