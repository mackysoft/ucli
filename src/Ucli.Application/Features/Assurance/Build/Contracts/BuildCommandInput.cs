namespace MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;

/// <summary> Represents normalized inputs for the <c>build.run</c> assurance command. </summary>
internal sealed record BuildCommandInput (
    string ProfilePath,
    string? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds);
