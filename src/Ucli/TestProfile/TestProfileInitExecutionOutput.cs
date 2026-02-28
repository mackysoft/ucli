namespace MackySoft.Ucli.TestProfile;

/// <summary> Represents output values produced by a successful test-profile initialization execution. </summary>
/// <param name="ProfilePath"> The absolute path of the generated profile template JSON file. </param>
internal sealed record TestProfileInitExecutionOutput (string ProfilePath);