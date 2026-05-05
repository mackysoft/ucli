namespace MackySoft.Ucli.Features.Daemon.Supervisor.Invocation;

/// <summary> Defines hidden command-line arguments used to launch the worktree-local supervisor. </summary>
internal static class SupervisorInvocationArguments
{
    /// <summary> Gets the hidden command-line flag used to run the supervisor host. </summary>
    public const string InternalServeFlag = "--ucli-internal-supervisor-serve";

    /// <summary> Gets the hidden option name used to pass the repository root to the supervisor host. </summary>
    public const string RepositoryRootOption = "--repositoryRoot";

    /// <summary> Builds the hidden supervisor invocation argument sequence. </summary>
    /// <param name="repositoryRoot"> The repository root passed to the hidden supervisor host. </param>
    /// <returns> The ordered argument sequence consumed by the hidden supervisor invocation parser. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="repositoryRoot" /> is empty or whitespace. </exception>
    public static string[] Build (string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("Repository root must not be empty.", nameof(repositoryRoot));
        }

        return
        [
            InternalServeFlag,
            RepositoryRootOption,
            repositoryRoot,
        ];
    }
}
