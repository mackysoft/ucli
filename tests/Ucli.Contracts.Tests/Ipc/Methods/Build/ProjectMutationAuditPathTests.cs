using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Build;

public sealed class ProjectMutationAuditPathTests
{
    [Theory]
    [InlineData("Assets/Generated.asset")]
    [InlineData("ProjectSettings/TagManager.asset")]
    [InlineData("Packages/manifest.json")]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathIsCanonicalAuditedDescendant_PreservesValue (string value)
    {
        var path = new ProjectMutationAuditPath(value);

        Assert.Equal(value, path.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Assets")]
    [InlineData("ProjectSettings")]
    [InlineData("Packages")]
    [InlineData("/Assets/Generated.asset")]
    [InlineData("C:/Project/Assets/Generated.asset")]
    [InlineData("Assets/./Generated.asset")]
    [InlineData("Assets/Generated/../Generated.asset")]
    [InlineData("Assets//Generated.asset")]
    [InlineData("Assets\\Generated.asset")]
    [InlineData("Assets/Generated.asset/")]
    [InlineData("assets/Generated.asset")]
    [InlineData("Library/Generated.asset")]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathViolatesAuditPathContract_ThrowsArgumentException (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ProjectMutationAuditPath(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPathIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ProjectMutationAuditPath(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonRoundTrip_PreservesStringWireShapeAndTypedValue ()
    {
        var path = new ProjectMutationAuditPath("Assets/Generated.asset");

        var json = JsonSerializer.Serialize(path, IpcJsonSerializerOptions.Default);
        var roundTrip = JsonSerializer.Deserialize<ProjectMutationAuditPath>(
            json,
            IpcJsonSerializerOptions.Default);

        Assert.Equal("\"Assets/Generated.asset\"", json);
        Assert.Equal(path, roundTrip);
    }
}
