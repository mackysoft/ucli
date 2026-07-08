using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcJsonSerializerOptionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Default_HasStableConfiguration ()
    {
        var options = IpcJsonSerializerOptions.Default;

        Assert.Same(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.True(options.PropertyNameCaseInsensitive);
        Assert.Equal(JsonUnmappedMemberHandling.Disallow, options.UnmappedMemberHandling);
        Assert.False(options.WriteIndented);
    }
}
