namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;

internal static class JsonAssertionTestData
{
    public static JsonDocument CreateSampleDocument ()
    {
        return JsonDocument.Parse(
            """
            {
              "status": "pass",
              "errorKind": null,
              "exitCode": 0,
              "counts": {
                "passed": 10,
                "failed": 1
              },
              "flags": {
                "hasRetries": true
              },
              "tests": [
                {
                  "fullName": "Cafe.Tests.Pass"
                },
                {
                  "fullName": "Cafe.Tests.Fail"
                }
              ]
            }
            """);
    }

    public static JsonSchemaNode CreateSampleSchema ()
    {
        return JsonSchemaNode.Object(
            static root => root
                .Required("status", JsonSchemaNode.Value(JsonSchemaType.String))
                .Required("errorKind", JsonSchemaNode.Union(JsonSchemaType.String, JsonSchemaType.Null))
                .Required("exitCode", JsonSchemaNode.Value(JsonSchemaType.Int32))
                .RequiredObject(
                    "counts",
                    static counts => counts
                        .Required("passed", JsonSchemaNode.Value(JsonSchemaType.Int32))
                        .Required("failed", JsonSchemaNode.Value(JsonSchemaType.Int32)))
                .RequiredObject(
                    "flags",
                    static flags => flags
                        .Required("hasRetries", JsonSchemaNode.Value(JsonSchemaType.Boolean)))
                .RequiredArrayOfObject(
                    "tests",
                    static tests => tests
                        .Required("fullName", JsonSchemaNode.Value(JsonSchemaType.String))));
    }
}
