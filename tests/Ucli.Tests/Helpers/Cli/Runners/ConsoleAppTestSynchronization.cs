namespace MackySoft.Tests;

internal static class ConsoleAppTestSynchronization
{
    public static SemaphoreSlim Lock { get; } = new(1, 1);
}
