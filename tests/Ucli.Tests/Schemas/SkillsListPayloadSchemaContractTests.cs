using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class SkillsListPayloadSchemaContractTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"categories":["internal"],"skillNames":[],"availableCategories":[{"category":"basic","skillCount":0}],"skills":[],"supportedHosts":[]}""")]
    [InlineData("""{"categories":["basic"],"skillNames":[],"availableCategories":[{"category":"internal","skillCount":0}],"skills":[],"supportedHosts":[]}""")]
    [InlineData("""{"categories":["basic"],"skillNames":[],"availableCategories":[{"category":"basic","skillCount":-1}],"skills":[],"supportedHosts":[]}""")]
    public void SkillsListPayloadSchema_RejectsInvalidCategoryInventory (string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);

        var errors = SkillsListPayloadSchemaTestSupport.ValidatePayload(payload.RootElement);

        Assert.NotEmpty(errors);
    }
}
