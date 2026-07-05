using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

public sealed class SkillsListPayloadSchemaContractTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"tiers":["internal"],"skillNames":[],"availableTiers":[{"tier":"basic","skillCount":0}],"skills":[],"supportedHosts":[]}""")]
    [InlineData("""{"tiers":["basic"],"skillNames":[],"availableTiers":[{"tier":"internal","skillCount":0}],"skills":[],"supportedHosts":[]}""")]
    [InlineData("""{"tiers":["basic"],"skillNames":[],"availableTiers":[{"tier":"basic","skillCount":-1}],"skills":[],"supportedHosts":[]}""")]
    public void SkillsListPayloadSchema_RejectsInvalidTierInventory (string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);

        var errors = SkillsListPayloadSchemaTestSupport.ValidatePayload(payload.RootElement);

        Assert.NotEmpty(errors);
    }
}
