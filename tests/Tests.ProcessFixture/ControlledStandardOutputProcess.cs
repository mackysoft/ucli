namespace MackySoft.Ucli.Tests.ProcessFixture;

using System.IO.Pipes;

public static class ControlledStandardOutputProcess
{
    public const int OutputLength = 5000;

    public const char OutputCharacter = '0';

    private const byte ReadySignal = 1;

    private const byte ReleaseSignal = 2;

    public static async Task<int> RunAsync (string[] arguments)
    {
        if (arguments.Length != 1 || string.IsNullOrWhiteSpace(arguments[0]))
        {
            throw new ArgumentException("The controlled-output pipe name is required.", nameof(arguments));
        }

        await using var pipe = new NamedPipeClientStream(
            ".",
            arguments[0],
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await pipe.ConnectAsync().ConfigureAwait(false);
        await pipe.WriteAsync(new byte[] { ReadySignal }).ConfigureAwait(false);
        await pipe.FlushAsync().ConfigureAwait(false);

        var releaseBuffer = new byte[1];
        var releaseCount = await pipe.ReadAsync(releaseBuffer).ConfigureAwait(false);
        if (releaseCount != 1 || releaseBuffer[0] != ReleaseSignal)
        {
            throw new InvalidOperationException("The controlled-output process was not released.");
        }

        await Console.Out.WriteLineAsync(new string(OutputCharacter, OutputLength)).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
        return 0;
    }
}
