using MackySoft.Ucli.Skills.Hosts.Contracts;
using MackySoft.Ucli.Skills.Shared;

namespace MackySoft.Ucli.Skills.Installation;

/// <summary> Resolves host-specific user-scope SKILL target roots. </summary>
public sealed class SkillUserTargetRootResolver
{
    private readonly Func<string?> homeDirectoryProvider;
    private readonly Func<string, string?> environmentVariableProvider;

    /// <summary> Initializes a new instance of the <see cref="SkillUserTargetRootResolver" /> class. </summary>
    public SkillUserTargetRootResolver () : this(
        static () => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetEnvironmentVariable)
    {
    }

    /// <summary> Initializes a new instance of the <see cref="SkillUserTargetRootResolver" /> class. </summary>
    /// <param name="homeDirectoryProvider"> Provides the current user's home directory. </param>
    /// <param name="environmentVariableProvider"> Provides process environment variables. </param>
    public SkillUserTargetRootResolver (
        Func<string?> homeDirectoryProvider,
        Func<string, string?> environmentVariableProvider)
    {
        this.homeDirectoryProvider = homeDirectoryProvider ?? throw new ArgumentNullException(nameof(homeDirectoryProvider));
        this.environmentVariableProvider = environmentVariableProvider ?? throw new ArgumentNullException(nameof(environmentVariableProvider));
    }

    /// <summary> Resolves the default user-scope target root for one host. </summary>
    /// <param name="descriptor"> The host descriptor that owns the user target policy. </param>
    /// <returns> The full target root or an environment failure. </returns>
    public SkillOperationResult<string> ResolveDefaultTargetRoot (SkillHostDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var policy = descriptor.UserTargetRootPolicy;
        if (!string.IsNullOrWhiteSpace(policy.EnvironmentVariableName))
        {
            var environmentRoot = environmentVariableProvider(policy.EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(environmentRoot))
            {
                var targetRoot = string.IsNullOrWhiteSpace(policy.EnvironmentVariableChildDirectory)
                    ? environmentRoot
                    : Path.Combine(environmentRoot, policy.EnvironmentVariableChildDirectory);
                return SkillOperationResult<string>.Success(Path.GetFullPath(targetRoot));
            }
        }

        return ResolveUnderHome(policy.HomeRelativeDirectory);
    }

    private SkillOperationResult<string> ResolveUnderHome (string homeRelativeDirectory)
    {
        var homeDirectory = homeDirectoryProvider();
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            return SkillOperationResult<string>.FailureResult(
                SkillFailureCodes.UserTargetUnavailable,
                "Could not resolve the current user's home directory for SKILL user scope.");
        }

        return SkillOperationResult<string>.Success(Path.GetFullPath(Path.Combine(homeDirectory, homeRelativeDirectory)));
    }
}
