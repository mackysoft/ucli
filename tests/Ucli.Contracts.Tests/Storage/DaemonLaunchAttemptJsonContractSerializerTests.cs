using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonLaunchAttemptJsonContractSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithEmptyLaunchAttemptId_ThrowsArgumentException ()
    {
        const string Json = """
            {
              "launchAttemptId": "00000000-0000-0000-0000-000000000000"
            }
            """;

        Assert.Throws<ArgumentException>(() =>
            DaemonLaunchAttemptJsonContractSerializer.Deserialize(Json));
    }
}
