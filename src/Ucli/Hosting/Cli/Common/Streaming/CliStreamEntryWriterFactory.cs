using MackySoft.Ucli.Application.Shared.Identifiers;

namespace MackySoft.Ucli.Hosting.Cli.Common.Streaming;

/// <summary> Creates per-command stream writers bound to the CLI standard-error stream. </summary>
internal sealed class CliStreamEntryWriterFactory
{
    private readonly TimeProvider timeProvider;
    private readonly IGuidGenerator streamIdGenerator;

    /// <summary> Initializes a factory with the process clock and stream identifier source. </summary>
    public CliStreamEntryWriterFactory (
        TimeProvider timeProvider,
        IGuidGenerator streamIdGenerator)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.streamIdGenerator = streamIdGenerator ?? throw new ArgumentNullException(nameof(streamIdGenerator));
    }

    /// <summary> Creates one independent stream writer for a command invocation. </summary>
    public CliStreamEntryWriter Create (string command)
    {
        return new CliStreamEntryWriter(
            command,
            streamIdGenerator.Generate(),
            Console.Error,
            timeProvider);
    }
}
