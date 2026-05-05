using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Supervisor;

namespace MackySoft.Ucli;

internal static class Program
{
    /// <summary> Selects the hosting mode and delegates execution to the matching runner. </summary>
    /// <param name="args"> The command-line arguments passed to the process. </param>
    /// <returns> The process exit code determined by the selected runner. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="args" /> is <see langword="null" />. </exception>
    private static async Task<int> Main (string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var supervisorInvocation = InternalSupervisorInvocationParser.Parse(args);
        if (supervisorInvocation.IsMatched)
        {
            if (!supervisorInvocation.IsValid)
            {
                return 1;
            }

            var supervisorRunner = new InternalSupervisorExecutionRunner();
            return await supervisorRunner.RunAsync(supervisorInvocation.RepositoryRoot).ConfigureAwait(false);
        }

        var cliRunner = new CliExecutionRunner();
        return await cliRunner.RunAsync(args).ConfigureAwait(false);
    }
}
