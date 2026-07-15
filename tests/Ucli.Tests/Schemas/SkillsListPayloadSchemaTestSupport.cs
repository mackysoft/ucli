using System.Text.Json;

namespace MackySoft.Ucli.Tests.Schemas;

internal static class SkillsListPayloadSchemaTestSupport
{
    public static void AssertPayloadMatchesSchema (JsonElement root)
    {
        var errors = ValidatePayload(root.GetProperty("payload"), out var payloadSchemaPath);
        Assert.True(errors.Count == 0, JsonSchemaValidationMessageBuilder.Build(errors, payloadSchemaPath));
    }

    public static IReadOnlyList<string> ValidatePayload (JsonElement payload)
    {
        return ValidatePayload(payload, out _);
    }

    private static IReadOnlyList<string> ValidatePayload (
        JsonElement payload,
        out string payloadSchemaPath)
    {
        var schemaSet = CliOutputSchemaTestSupport.SchemaSet;
        payloadSchemaPath = schemaSet.FindPayloadSchemaPath(UcliCommandNames.SkillsList)
            ?? throw new InvalidOperationException("skills.list payload schema was not found.");
        Assert.Equal("cli-output/payload/skills.list.schema.json", payloadSchemaPath);

        return schemaSet.Validate(payloadSchemaPath, payload, "$.payload");
    }
}
