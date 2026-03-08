namespace MackySoft.Ucli.Ops;

/// <summary> Represents raw input for <c>ops describe</c>. </summary>
/// <param name="OperationName"> The target operation name. </param>
/// <param name="ProjectPath"> The optional project path. </param>
/// <param name="Mode"> The optional execution mode. </param>
/// <param name="Timeout"> The optional timeout in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional read-index mode override. </param>
internal sealed record OpsDescribeCommandInput (
    string? OperationName,
    string? ProjectPath,
    string? Mode,
    string? Timeout,
    string? ReadIndexMode);