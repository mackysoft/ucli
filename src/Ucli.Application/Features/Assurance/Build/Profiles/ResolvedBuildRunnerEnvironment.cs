namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents process environment entries requested by an executeMethod runner invocation. </summary>
internal sealed class ResolvedBuildRunnerEnvironment
{
    /// <summary> Gets an empty runner environment request. </summary>
    public static ResolvedBuildRunnerEnvironment Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>());

    /// <summary> Initializes requested process environment entries. </summary>
    public ResolvedBuildRunnerEnvironment (
        IReadOnlyList<string> variables,
        IReadOnlyList<string> secrets)
    {
        ArgumentNullException.ThrowIfNull(variables);
        ArgumentNullException.ThrowIfNull(secrets);

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        Variables = CopyNames(variables, nameof(variables), seenNames);
        Secrets = CopyNames(secrets, nameof(secrets), seenNames);
    }

    /// <summary> Gets names read as non-secret environment variables. </summary>
    public IReadOnlyList<string> Variables { get; }

    /// <summary> Gets names read as secret environment variables. </summary>
    public IReadOnlyList<string> Secrets { get; }

    private static IReadOnlyList<string> CopyNames (
        IReadOnlyList<string> names,
        string parameterName,
        ISet<string> seenNames)
    {
        var copiedNames = new string[names.Count];
        for (var i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (!ResolvedBuildRunnerInvocation.IsValidMapKey(name))
            {
                throw new ArgumentException(
                    $"Runner environment name '{name}' must be a non-empty string without '=', NUL, or newline characters.",
                    parameterName);
            }

            if (!seenNames.Add(name))
            {
                throw new ArgumentException($"Runner environment contains duplicate name '{name}'.", parameterName);
            }

            copiedNames[i] = name;
        }

        return Array.AsReadOnly(copiedNames);
    }
}
