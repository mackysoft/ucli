namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines readiness targets supported by the <c>ready</c> command. </summary>
internal enum ReadyTarget
{
    /// <summary> Verifies that request execution can be dispatched. </summary>
    Execution = 0,

    /// <summary> Verifies that mutation requests can be dispatched. </summary>
    Mutation = 1,

    /// <summary> Verifies that Unity test execution can be dispatched. </summary>
    Test = 2,

    /// <summary> Verifies that project-wide read-index artifacts satisfy the selected mode. </summary>
    ReadIndex = 3,
}
