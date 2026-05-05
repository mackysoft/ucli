namespace MackySoft.Ucli.Application.Features.Init.Common.Contracts;

/// <summary> Represents output values produced by a successful init execution. </summary>
/// <param name="ConfigPath"> The absolute path of the generated <c>.ucli/config.json</c> file. </param>
/// <param name="GitIgnorePath"> The absolute path of the generated <c>.ucli/.gitignore</c> file. </param>
internal sealed record InitExecutionOutput (
    string ConfigPath,
    string GitIgnorePath);
