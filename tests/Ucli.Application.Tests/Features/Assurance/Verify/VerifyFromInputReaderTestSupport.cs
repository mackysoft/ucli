namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

internal static class VerifyFromInputReaderTestSupport
{
    public const string ProjectFingerprint = "project-fingerprint";

    public static TheoryData<string, UcliCode> CreateInvalidInputTheoryData (
        IEnumerable<InvalidInputCase> testCases)
    {
        var data = new TheoryData<string, UcliCode>();
        foreach (var testCase in testCases)
        {
            data.Add(testCase.Json, testCase.ExpectedCode);
        }

        return data;
    }

    public static string CreateDefaultOpResultJson (
        string diagnosticsJson = "[]",
        string op = "edit")
    {
        return $$"""
        {
          "opId": "op-1",
          "op": "{{op}}",
          "phase": "call",
          "applied": true,
          "changed": true,
          "touched": [
            {
              "kind": "asset",
              "path": "Assets/Scene.unity"
            }
          ],
          "diagnostics": {{diagnosticsJson}}
        }
        """;
    }

    public static string CreateValidInputJson (
        string opResultJson,
        string? readPostconditionJson = null,
        string command = "call",
        string? postReadSourceJson = null)
    {
        var readPostconditionProperty = string.IsNullOrWhiteSpace(readPostconditionJson)
            ? string.Empty
            : $"""
              ,
              "readPostcondition": {readPostconditionJson}
            """;
        var normalizedPostReadSourceJson = postReadSourceJson ?? """
            {
              "schemaVersion": 1,
              "steps": [
                {
                  "opId": "op-1",
                  "sourceKind": "edit",
                  "playModeMutation": false,
                  "commit": "context",
                  "persistenceExpected": true,
                  "expectedPostState": "deterministic"
                }
              ]
            }
            """;
        return $$"""
        {
          "protocolVersion": 1,
          "status": "ok",
          "exitCode": 0,
          "command": "{{command}}",
          "payload": {
            "project": {
              "projectFingerprint": "project-fingerprint"
            },
            "opResults": [
              {{opResultJson}}
            ],
            "postReadSource": {{normalizedPostReadSourceJson}}{{readPostconditionProperty}}
          },
          "errors": []
        }
        """;
    }

    internal readonly record struct InvalidInputCase (
        string Json,
        UcliCode ExpectedCode);
}
