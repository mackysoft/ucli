using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Programs;

public sealed class IpcProgramEffectiveSnapshotsTests
{
    public static TheoryData<bool, bool, string> AuthorizationDigests => new()
    {
        { false, false, "5e27534721bceb155cbd37d8f92d9139677ba289dc3b6d769e4e611ec1e6d718" },
        { false, true, "c537ff1edb9c1bbce3f8341b23aa3aa9789d51b7868a5cccd924271cd2f84338" },
        { true, false, "99390da6ddfec7d25e0bf525e112bf540189644236f9581f5a4c62eaeebfd34d" },
        { true, true, "8b73c2a123a19dfdd174756bd72b58ff0c79498e7e349e71c80f5b157c513302" },
    };

    [Theory]
    [MemberData(nameof(AuthorizationDigests))]
    [Trait("Size", "Small")]
    public void AuthorizationDigest_UsesTheFixedProgramSnapshotInput (bool allowDangerous, bool allowPlayMode, string expectedDigest)
    {
        var digest = IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(allowDangerous, allowPlayMode);

        Assert.Equal(expectedDigest, digest.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationDigest_UsesTheFixedProgramSnapshotInput ()
    {
        var digest = IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
            schemaVersion: 1,
            operationPolicy: "safe",
            planTokenMode: "optional",
            readIndexDefaultMode: "requireFresh",
            operationAllowlist: ["^ucli\\."],
            ipcDefaultTimeoutMilliseconds: 3000,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int>(StringComparer.Ordinal) { ["call"] = 60_000 },
            evalEnabled: false);

        Assert.Equal("41748e67bea427c2add48a59285b22498a70117ec1666b6ce9a3db09419a2fcc", digest.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationDigest_IsIndependentOfMapInsertionOrder ()
    {
        var first = ComputeConfigurationDigest(
            new Dictionary<string, int>(StringComparer.Ordinal) { ["program.run"] = 30_000, ["call"] = 60_000 },
            ["first", "second"]);
        var second = ComputeConfigurationDigest(
            new Dictionary<string, int>(StringComparer.Ordinal) { ["call"] = 60_000, ["program.run"] = 30_000 },
            ["first", "second"]);

        Assert.Equal(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationDigest_PreservesAllowlistOrder ()
    {
        var first = ComputeConfigurationDigest(new Dictionary<string, int>(StringComparer.Ordinal), ["first", "second"]);
        var second = ComputeConfigurationDigest(new Dictionary<string, int>(StringComparer.Ordinal), ["second", "first"]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationDigest_UsesUtf8JsonEscapingForQuotesBackslashesNonAsciiAndControls ()
    {
        const string value = "quote\"\\日本\u0001";
        var digest = IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
            schemaVersion: 1,
            operationPolicy: "safe\"\\日本\u0001",
            planTokenMode: "optional",
            readIndexDefaultMode: "requireFresh",
            operationAllowlist: [value],
            ipcDefaultTimeoutMilliseconds: 1,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int>(StringComparer.Ordinal) { [value] = 2 },
            evalEnabled: true);

        Assert.Equal("8a5f45c0059a75deae0d8b7185d3797fcdb27b133ecdbd2a8f9fab260f45cf36", digest.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationDigest_UsesTheDocumentedCanonicalUtf8Bytes ()
    {
        const string value = "quote\"\\日本\u0001";
        const string canonicalJson = "{\"evalEnabled\":true,\"ipcDefaultTimeoutMilliseconds\":1,\"ipcTimeoutMillisecondsByCommand\":{\"quote\\\"\\\\日本\\u0001\":2},\"operationAllowlist\":[\"quote\\\"\\\\日本\\u0001\"],\"operationPolicy\":\"safe\\\"\\\\日本\\u0001\",\"planTokenMode\":\"optional\",\"readIndexDefaultMode\":\"requireFresh\",\"schemaVersion\":1}";

        var expected = Sha256Digest.Compute(Encoding.UTF8.GetBytes(canonicalJson));
        var actual = IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
            1, "safe\"\\日本\u0001", "optional", "requireFresh", [value], 1,
            new Dictionary<string, int>(StringComparer.Ordinal) { [value] = 2 }, true);

        Assert.Equal(expected, actual);
        Assert.Equal("8a5f45c0059a75deae0d8b7185d3797fcdb27b133ecdbd2a8f9fab260f45cf36", actual.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AuthorizationSnapshot_RejectsDigestThatDoesNotMatchEffectivePermissions ()
    {
        Assert.Throws<ArgumentException>(() => new IpcProgramEffectiveAuthorizationSnapshot(
            allowDangerous: false,
            allowPlayMode: false,
            digest: Sha256Digest.Parse(new string('d', 64))));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConfigurationSnapshot_RejectsDigestThatDoesNotMatchEffectiveValues ()
    {
        Assert.Throws<ArgumentException>(() => new IpcProgramEffectiveConfigurationSnapshot(
            schemaVersion: 1,
            operationPolicy: "safe",
            planTokenMode: "optional",
            readIndexDefaultMode: "requireFresh",
            operationAllowlist: [],
            ipcDefaultTimeoutMilliseconds: 3000,
            ipcTimeoutMillisecondsByCommand: new Dictionary<string, int>(StringComparer.Ordinal),
            evalEnabled: false,
            digest: Sha256Digest.Parse(new string('d', 64))));
    }

    private static Sha256Digest ComputeConfigurationDigest (IReadOnlyDictionary<string, int> timeouts, IReadOnlyList<string> allowlist)
    {
        return IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
            schemaVersion: 1,
            operationPolicy: "safe",
            planTokenMode: "optional",
            readIndexDefaultMode: "requireFresh",
            operationAllowlist: allowlist,
            ipcDefaultTimeoutMilliseconds: 3000,
            ipcTimeoutMillisecondsByCommand: timeouts,
            evalEnabled: false);
    }
}
