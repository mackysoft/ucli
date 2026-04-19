namespace MackySoft.Ucli.Features.Requests.Validate;

/// <summary> Represents one normalized <c>validate</c> command input. </summary>
/// <param name="RequestPath"> The optional <c>--requestPath</c> value. </param>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="ReadIndexMode"> The optional <c>--readIndexMode</c> value. </param>
internal sealed record ValidateCommandInput (
    string? RequestPath,
    string? ProjectPath,
    string? ReadIndexMode);