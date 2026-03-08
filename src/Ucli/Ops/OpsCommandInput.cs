namespace MackySoft.Ucli.Ops;

/// <summary> Represents raw input for <c>ops list</c>. </summary>
/// <param name="ProjectPath"> The optional project path. </param>
/// <param name="Mode"> The optional execution mode. </param>
/// <param name="Timeout"> The optional timeout in milliseconds. </param>
/// <param name="ReadIndexMode"> The optional read-index mode override. </param>
internal sealed record OpsCommandInput (
    string? ProjectPath,
    string? Mode,
    string? Timeout,
    string? ReadIndexMode);