using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines readiness targets supported by the <c>ready</c> command. </summary>
internal enum ReadyTarget
{
    /// <summary> Verifies that request execution can be dispatched. </summary>
    [UcliContractLiteral("execution")]
    Execution = 1,

    /// <summary> Verifies that mutation requests can be dispatched. </summary>
    [UcliContractLiteral("mutation")]
    Mutation = 2,

    /// <summary> Verifies that Unity test execution can be dispatched. </summary>
    [UcliContractLiteral("test")]
    Test = 3,

    /// <summary> Verifies that project-wide read-index artifacts satisfy the selected mode. </summary>
    [UcliContractLiteral("readIndex")]
    ReadIndex = 4,
}
