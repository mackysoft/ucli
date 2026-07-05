using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcOpsReadContractSerializationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcOpsReadRequest_SerializesEditLoweringCatalogFlagOnlyWhenSpecified ()
    {
        var defaultRequest = new IpcOpsReadRequest();
        var defaultJson = IpcPayloadCodec.SerializeToElement(defaultRequest);

        Assert.False(defaultJson.TryGetProperty("includeEditLoweringOnly", out _));

        var validationRequest = new IpcOpsReadRequest(
            FailFast: true,
            RequireReadinessGate: true,
            IncludeEditLoweringOnly: true);
        var validationJson = IpcPayloadCodec.SerializeToElement(validationRequest);

        Assert.True(validationJson.GetProperty("failFast").GetBoolean());
        Assert.True(validationJson.GetProperty("requireReadinessGate").GetBoolean());
        Assert.True(validationJson.GetProperty("includeEditLoweringOnly").GetBoolean());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcOpsReadContracts_SerializeWithCamelCaseFields ()
    {
        var requestPayload = new IpcOpsReadRequest(FailFast: true, RequireReadinessGate: true);
        var describe = CreateGoDescribeContract();
        var responsePayload = new IpcOpsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Operations:
            [
                new IndexOpEntryJsonContract(
                    Name: UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    ArgsSchemaJson: """{"type":"object"}""",
                    ResultSchemaJson: """{"type":"object"}""")
                {
                    Description = describe.Description,
                    Inputs = describe.Inputs,
                    ResultContract = describe.ResultContract,
                    Assurance = describe.Assurance,
                },
            ]);

        var request = IpcPayloadCodec.SerializeToElement(requestPayload);
        var response = IpcPayloadCodec.SerializeToElement(responsePayload);

        JsonAssert.For(request)
            .HasBoolean("failFast", true)
            .HasBoolean("requireReadinessGate", true);
        JsonAssert.For(response)
            .HasString("generatedAtUtc", "2026-03-06T00:00:00+00:00")
            .HasArrayLength("operations", 1)
            .HasProperty("operations", 0, operation => operation
                .HasString("name", UcliPrimitiveOperationNames.GoDescribe)
                .HasString("kind", "query")
                .HasString("policy", "safe")
                .HasString("description", describe.Description!)
                .HasProperty("resultContract", resultContract => resultContract
                    .HasBoolean("emitted", true)
                    .HasString("resultType", "GameObjectDescriptionResult"))
                .HasProperty("assurance", assurance => assurance
                    .HasBoolean("mayDirty", false)
                    .HasBoolean("mayPersist", false)
                    .HasString("planMode", "observesLiveUnity"))
                .HasString("argsSchemaJson", """{"type":"object"}"""));

        var operationElement = response.GetProperty("operations")[0];
        var targetInputElement = operationElement.GetProperty("inputs").EnumerateArray().Single(input =>
            string.Equals(input.GetProperty("name").GetString(), "target", StringComparison.Ordinal));
        var globalObjectIdVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "byGlobalObjectId", StringComparison.Ordinal));
        var fieldElement = Assert.Single(globalObjectIdVariantElement.GetProperty("fields").EnumerateArray());

        Assert.False(globalObjectIdVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(globalObjectIdVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(fieldElement)
            .HasString("name", "globalObjectId")
            .HasString("argsPath", "$.target.globalObjectId")
            .HasString("description", "Resolved Unity GlobalObjectId.")
            .HasArrayLength("constraints", 1)
            .HasProperty("constraints", 0, constraint => constraint
                .HasString("kind", "globalObjectId"));

        var sceneHierarchyVariantElement = targetInputElement.GetProperty("variants").EnumerateArray().Single(variant =>
            string.Equals(variant.GetProperty("name").GetString(), "bySceneHierarchyPath", StringComparison.Ordinal));
        var sceneFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "scene", StringComparison.Ordinal));
        var hierarchyPathFieldElement = sceneHierarchyVariantElement.GetProperty("fields").EnumerateArray().Single(candidate =>
            string.Equals(candidate.GetProperty("name").GetString(), "hierarchyPath", StringComparison.Ordinal));

        Assert.False(sceneHierarchyVariantElement.TryGetProperty("argsPaths", out _));
        Assert.False(sceneHierarchyVariantElement.TryGetProperty("constraints", out _));
        JsonAssert.For(sceneFieldElement)
            .HasString("argsPath", "$.target.scene")
            .HasString("description", "Scene asset path for a hierarchy selector.");
        var assetExistsConstraint = sceneFieldElement.GetProperty("constraints").EnumerateArray().Single(constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), "assetExists", StringComparison.Ordinal));
        JsonAssert.For(assetExistsConstraint)
            .HasString("assetKind", "scene");
        JsonAssert.For(hierarchyPathFieldElement)
            .HasString("argsPath", "$.target.hierarchyPath")
            .HasString("description", "Unity hierarchy path inside the selected scene or prefab.");
        Assert.Contains(hierarchyPathFieldElement.GetProperty("constraints").EnumerateArray(), constraint =>
            string.Equals(constraint.GetProperty("kind").GetString(), "hierarchyPath", StringComparison.Ordinal));
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()));
    }
}
