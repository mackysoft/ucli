using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

internal static class CommandResultTestWriter
{
    public static ICommandResultWriter Create ()
    {
        return new CommandResultWriter(new CommandResultJsonContractWriter());
    }
}
