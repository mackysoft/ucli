namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Represents unresolved project-path inputs before source precedence is applied. </summary>
/// <param name="CommandOptionProjectPath"> The optional <c>--projectPath</c> value. </param>
/// <param name="FallbackProjectPath"> The optional command-specific fallback project path. </param>
/// <param name="FallbackSourceLabel"> The optional label that identifies <paramref name="FallbackProjectPath" />. </param>
internal sealed record ProjectContextResolutionInput (
    string? CommandOptionProjectPath,
    string? FallbackProjectPath = null,
    string? FallbackSourceLabel = null);
