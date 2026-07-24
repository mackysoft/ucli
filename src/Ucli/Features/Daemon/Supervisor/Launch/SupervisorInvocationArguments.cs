using MackySoft.FileSystem;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Launch;

/// <summary> Defines hidden command-line arguments used to launch the worktree-local supervisor. </summary>
internal static class SupervisorInvocationArguments
{
    /// <summary> Builds the hidden supervisor invocation argument sequence. </summary>
    /// <param name="repositoryRoot"> The repository root passed to the hidden supervisor host. </param>
    /// <returns> The ordered argument sequence consumed by the hidden supervisor invocation parser. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="repositoryRoot" /> is <see langword="null" />. </exception>
    public static string[] Build (AbsolutePath repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        return
        [
            SupervisorConstants.InternalServeFlag,
            SupervisorConstants.RepositoryRootOption,
            repositoryRoot.Value,
        ];
    }
}
