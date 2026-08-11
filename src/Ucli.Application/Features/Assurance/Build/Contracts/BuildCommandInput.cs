using MackySoft.Ucli.Application.Shared.Paths;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;

/// <summary> Represents normalized inputs for the <c>build.run</c> assurance command. </summary>
internal sealed record BuildCommandInput (
    FilePathReference ProfilePath,
    AbsolutePath? ProjectPath,
    UnityExecutionMode? Mode,
    int? TimeoutMilliseconds);
