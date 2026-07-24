using System.Text.Json;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorManifestStoreTestSupport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static SupervisorManifestStore CreateFileBacked (TimeProvider timeProvider)
    {
        return new SupervisorManifestStore(
            timeProvider,
            static (path, cancellationToken) => FileUtilities.ReadAllBytesOrNullAsync(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllBytesAtomicallyAsync(path, contents, cancellationToken),
            static path => FileUtilities.DeleteIfExists(path));
    }

    public static string Serialize (SupervisorInstanceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(
            new SupervisorInstanceManifestJsonContract(
                manifest.ProcessId,
                manifest.SessionToken.GetEncodedValue(),
                TextVocabulary.GetText(manifest.Endpoint.TransportKind),
                manifest.Endpoint.Address,
                manifest.IssuedAtUtc),
            SerializerOptions);
    }
}
