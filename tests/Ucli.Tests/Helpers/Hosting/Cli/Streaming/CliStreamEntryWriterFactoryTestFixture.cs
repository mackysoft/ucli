using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.TestSupport;

/// <summary> Provides the system-backed CLI stream writer factory used by command-level tests. </summary>
internal static class CliStreamEntryWriterFactoryTestFixture
{
    public static CliStreamEntryWriterFactory System { get; } = new(
        TimeProvider.System,
        new GuidGenerator());
}
