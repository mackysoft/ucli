using System.Collections.ObjectModel;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents non-secret executeMethod runner invocation inputs resolved from a build profile. </summary>
internal sealed class ResolvedBuildRunnerInvocation
{
    /// <summary> Gets an empty execute-method runner invocation. </summary>
    public static ResolvedBuildRunnerInvocation Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        ResolvedBuildRunnerEnvironment.Empty);

    /// <summary> Initializes execute-method runner invocation inputs. </summary>
    public ResolvedBuildRunnerInvocation (
        IReadOnlyDictionary<string, string> arguments,
        ResolvedBuildRunnerEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var copiedArguments = new Dictionary<string, string>(arguments.Count, StringComparer.Ordinal);
        foreach (var pair in arguments)
        {
            if (!IsValidMapKey(pair.Key))
            {
                throw new ArgumentException(
                    $"Runner argument key '{pair.Key}' must be a non-empty string without '=', NUL, or newline characters.",
                    nameof(arguments));
            }

            if (pair.Value == null)
            {
                throw new ArgumentException("Runner argument values must not be null.", nameof(arguments));
            }

            copiedArguments.Add(pair.Key, pair.Value);
        }

        Arguments = new ReadOnlyDictionary<string, string>(copiedArguments);
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary> Gets the invocation arguments. </summary>
    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary> Gets the requested process environment entries. </summary>
    public ResolvedBuildRunnerEnvironment Environment { get; }

    /// <summary> Determines whether a runner argument or environment map key is valid. </summary>
    internal static bool IsValidMapKey (string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOf('=') < 0
            && value.IndexOf('\0') < 0
            && value.IndexOf('\n') < 0
            && value.IndexOf('\r') < 0;
    }
}
