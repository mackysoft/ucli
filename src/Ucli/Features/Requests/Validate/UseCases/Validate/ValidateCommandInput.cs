using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Features.Requests.Validate.UseCases.Validate;

/// <summary> Represents one normalized <c>validate</c> command input. </summary>
/// <param name="ProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="ReadIndexMode"> The optional normalized <c>--readIndexMode</c> override. </param>
internal sealed record ValidateCommandInput (
    string? ProjectPath,
    ReadIndexMode? ReadIndexMode);
