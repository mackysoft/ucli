using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

internal static class IndexCatalogContractValidatorOpsTestSupport
{
    public static IndexOpsDescribeJsonContract CreateOpsDescribe (IndexOpEntryJsonContract operation)
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: Sha256DigestTestFactory.Compute("source-hash").ToString(),
            Operation: operation);
    }

    public static IndexOpEntryJsonContract CreateValidOpsEntry ()
    {
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.GoDescribe,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(GoDescribeArgs)),
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(GameObjectDescriptionResult)));
        var describe = UcliOperationDescribeContractBuilder.CreateJudging(
            generationResult,
            "Returns a GameObject description including components and child hierarchy.",
            CreateSafeQueryAssurance(),
            verdictContract: new UcliOperationVerdictContract(
                "The requested GameObject exists and its description is complete."),
            codeContract: null);

        return WithDescriptorDigest(
            new IndexOpEntryJsonContract(
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
            });
    }

    public static IndexOpEntryJsonContract CreateEditLoweringOnlyOpsEntry ()
    {
        var assurance = new UcliOperationAssuranceContract(
            sideEffects: [UcliOperationSideEffect.SceneContentMutation],
            touchedKinds: [UcliTouchedResourceKind.Scene],
            planMode: UcliOperationPlanMode.MayCreatePreviewState,
            planSemantics: "Validate arguments and compute preview changes without persisting project data.",
            callSemantics: "Apply serialized property values to the live component.",
            touchedContract: "Reports the resource dirtied by the component mutation.",
            readPostconditionContract: "Read surfaces covering touched resources may be stale until refreshed.",
            failureSemantics: "Failure before apply leaves no requested mutation.",
            dangerousNotes: ["This operation can dirty live Unity state without persisting it."]);
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.CompSet,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(ComponentSetArgs)),
            resultTypeInfo: null);
        var describe = UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
            "Assigns serialized property values on a component target.",
            assurance,
            codeContract: null);

        return WithDescriptorDigest(
            new IndexOpEntryJsonContract(
                Name: UcliPrimitiveOperationNames.CompSet,
                Kind: UcliOperationKind.Mutation,
                Policy: OperationPolicy.Advanced,
                ArgsContract: describe.ArgsContract,
                DescriptorDigest: null,
                VerdictContract: null,
                ResultContract: null,
                Exposure: UcliOperationExposure.EditLoweringOnly,
                PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
            {
                Description = describe.Description,
                Assurance = describe.Assurance,
            });
    }

    public static IndexOpEntryJsonContract WithDescriptorDigest (IndexOpEntryJsonContract operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var descriptorWithoutDigest = operation with { DescriptorDigest = null };
        return descriptorWithoutDigest with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(descriptorWithoutDigest),
        };
    }

    public static IndexOpsCatalogEntryJsonContract CreateValidOpsCatalogEntry ()
    {
        return new IndexOpsCatalogEntryJsonContract(
            Name: "ucli.scene.open",
            Kind: UcliOperationKind.Command,
            Policy: OperationPolicy.Safe,
            Description: "Opens a Unity scene.",
            DescribeKey: new string('a', 64),
            DescribeHash: new string('b', 64));
    }

    private static UcliOperationAssuranceContract CreateSafeQueryAssurance ()
    {
        return new UcliOperationAssuranceContract(
            sideEffects: [UcliOperationSideEffect.ObservesUnityState],
            touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Read Unity state without applying mutation.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the observation was not fully produced.",
            dangerousNotes: Array.Empty<string>());
    }
}
