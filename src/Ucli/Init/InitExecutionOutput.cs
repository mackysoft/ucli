namespace MackySoft.Ucli.Init;

/// <summary> Represents output values produced by a successful init execution. </summary>
/// <param name="ProjectPath"> The resolved UnityProject root path. </param>
/// <param name="ProjectFingerprint"> The resolved UnityProject fingerprint. </param>
/// <param name="ConfigPath"> The path of the generated config file. </param>
/// <param name="GitIgnorePath"> The path of the generated <c>.ucli/.gitignore</c> file. </param>
internal sealed record InitExecutionOutput (
    string ProjectPath,
    string ProjectFingerprint,
    string ConfigPath,
    string GitIgnorePath);