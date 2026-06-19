using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;

/// <summary> Represents one normalized <c>validate</c> command input. </summary>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="ReadIndexMode"> The optional normalized <c>--readIndexMode</c> override. </param>
/// <param name="RequestJson"> The raw request JSON read by the CLI host. </param>
internal sealed record ValidateCommandInput (
    string? ProjectPath,
    ReadIndexMode? ReadIndexMode,
    string RequestJson)
{
    /// <summary> Gets the optional normalized <c>--timeout</c> value in milliseconds. </summary>
    public int? TimeoutMilliseconds { get; init; }
}
