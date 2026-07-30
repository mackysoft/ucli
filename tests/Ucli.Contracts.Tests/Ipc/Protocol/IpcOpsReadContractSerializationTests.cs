using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Configuration;
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
        var operationEntry = new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: UcliOperationKind.Query,
            Policy: OperationPolicy.Safe,
            ArgsContract: describe.ArgsContract,
            DescriptorDigest: null,
            ResultContract: describe.ResultContract,
            VerdictContract: describe.VerdictContract,
            Exposure: null,
            PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
        {
            Description = describe.Description,
            Assurance = describe.Assurance,
        };
        operationEntry = operationEntry with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(operationEntry),
        };
        var responsePayload = new IpcOpsReadResponse(
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            Operations: [operationEntry]);

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
                .HasString("playModeSupport", "disallowed")
                .HasString("descriptorDigest", operationEntry.DescriptorDigest!.ToString())
                .HasString("description", describe.Description!)
                .HasProperty("argsContract", argsContract => argsContract
                    .HasString(
                        "contractDigest",
                        describe.ArgsContract!.Value.ContractDigest.ToString())
                    .HasProperty("typeMetadata")
                    .HasProperty("schema"))
                .HasProperty("resultContract", resultContract => resultContract
                    .HasString(
                        "contractDigest",
                        describe.ResultContract!.Value.ContractDigest.ToString())
                    .HasProperty("typeMetadata")
                    .HasProperty("schema"))
                .HasProperty("verdictContract", verdictContract => verdictContract
                    .HasString(
                        "description",
                        "The requested GameObject exists and its description is complete."))
                .HasProperty("assurance", assurance => assurance
                    .HasBoolean("mayDirty", false)
                    .HasBoolean("mayPersist", false)
                    .HasString("planMode", "observesLiveUnity")));
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            "ucli.test.go.describe",
            serializerOptions.GetTypeInfo(typeof(GoDescribeArgs)),
            serializerOptions.GetTypeInfo(typeof(GameObjectDescriptionResult)));
        return UcliOperationDescribeContractBuilder.CreateJudging(
            generationResult,
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()),
            verdictContract: new UcliOperationVerdictContract(
                "The requested GameObject exists and its description is complete."),
            codeContract: null);
    }
}
