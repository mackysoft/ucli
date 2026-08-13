namespace MackySoft.Ucli.Tests.ProcessFixture;

internal static class Program
{
    public static Task<int> Main (string[] arguments)
    {
        return ControlledStandardOutputProcess.RunAsync(arguments);
    }
}
