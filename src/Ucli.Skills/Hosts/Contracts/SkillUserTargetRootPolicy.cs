namespace MackySoft.Ucli.Skills.Hosts.Contracts;

/// <summary> Describes how a host default user-scope target root is resolved. </summary>
/// <param name="EnvironmentVariableName"> The optional environment variable that overrides the home-relative target root. </param>
/// <param name="EnvironmentVariableChildDirectory"> The child directory appended to the environment variable value when present. </param>
/// <param name="HomeRelativeDirectory"> The home-relative fallback target directory. </param>
public sealed record SkillUserTargetRootPolicy (
    string? EnvironmentVariableName,
    string? EnvironmentVariableChildDirectory,
    string HomeRelativeDirectory);
