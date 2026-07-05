using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex;

internal static class IndexCatalogContractValidatorOpsTestSupport
{
    public static IndexOpsDescribeJsonContract CreateOpsDescribe (IndexOpEntryJsonContract operation)
    {
        return new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Operation: operation);
    }

    public static IndexOpEntryJsonContract CreateValidOpsEntry (
        string argsSchemaJson = """{"type":"object","additionalProperties":false,"properties":{}}""",
        string? resultSchemaJson = null,
        IReadOnlyList<UcliOperationInputContract>? inputs = null)
    {
        return new IndexOpEntryJsonContract(
            Name: "ucli.scene.open",
            Kind: "command",
            Policy: "safe",
            ArgsSchemaJson: argsSchemaJson,
            ResultSchemaJson: resultSchemaJson)
        {
            Description = "Opens a Unity scene asset in the editor.",
            Inputs = inputs ??
            [
                new UcliOperationInputContract(
                    name: "path",
                    valueType: "string",
                    description: "Project-relative path to an existing Unity scene asset.",
                    constraints: Array.Empty<UcliOperationInputConstraintContract>()),
            ],
            ResultContract = UcliOperationResultContract.NoResult("No operation-specific result is emitted."),
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate arguments without applying mutation.",
                callSemantics: "Open an editor context without persisting project data.",
                touchedContract: "Reports no mutation resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the operation did not complete.",
                dangerousNotes: Array.Empty<string>()),
        };
    }

    public static IndexOpEntryJsonContract CreateEditLoweringOnlyOpsEntry ()
    {
        return CreateValidOpsEntry(
            argsSchemaJson: """{"type":"object","additionalProperties":false,"properties":{"var":{"type":"string"}}}""",
            inputs: Array.Empty<UcliOperationInputContract>()) with
        {
            Name = UcliPrimitiveOperationNames.CompSet,
            Kind = "mutation",
            Policy = "advanced",
            Exposure = "editLoweringOnly",
            Description = "Assigns serialized property values on a component target.",
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffect.SceneContentMutation],
                touchedKinds: [UcliTouchedResourceKindNames.Scene],
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Validate arguments and compute preview changes without persisting project data.",
                callSemantics: "Apply serialized property values to the live component.",
                touchedContract: "Reports the resource dirtied by the component mutation.",
                readPostconditionContract: "Read surfaces covering touched resources may be stale until refreshed.",
                failureSemantics: "Failure before apply leaves no requested mutation.",
                dangerousNotes: ["This operation can dirty live Unity state without persisting it."]),
        };
    }

    public static IndexOpsCatalogEntryJsonContract CreateValidOpsCatalogEntry ()
    {
        return new IndexOpsCatalogEntryJsonContract(
            Name: "ucli.scene.open",
            Kind: "command",
            Policy: "safe",
            Description: "Opens a Unity scene.",
            DescribeKey: new string('a', 64),
            DescribeHash: new string('b', 64));
    }
}
