using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Features.OperationCatalog;

internal static class OperationCatalogTestFixtures
{
    public static OpsCatalogFetchResult CreateFetchResult (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        if (!OpsCatalogSnapshot.TryCreate(generatedAtUtc, operations, "operations", out var snapshot, out var error))
        {
            throw new InvalidOperationException($"Operation catalog test fixture is invalid. {error}");
        }

        return OpsCatalogFetchResult.Success(snapshot!);
    }

    public static IndexOpEntryJsonContract CreateGoDescribeEntry ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: """{"type":"object"}""")
        {
            Description = "Returns a GameObject description including components and child hierarchy.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            Assurance = CreateSafeQueryAssurance(),
        };
    }

    public static IndexOpEntryJsonContract CreateSceneSaveEntry ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.SceneSave,
            Kind: "mutation",
            Policy: "advanced",
            ArgsSchemaJson: """{"type":"object"}""")
        {
            Description = "Saves a Unity scene asset.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.NoResult("No operation-specific result is emitted."),
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: [UcliOperationSideEffectValues.SceneSave],
                touchedKinds: [UcliTouchedResourceKindNames.Scene],
                planMode: UcliOperationPlanModeValues.ObservesLiveUnity,
                planSemantics: "Observe save-relevant project state without writing project files.",
                callSemantics: "Persist save-relevant Unity state.",
                touchedContract: "Reports resources known to be saved.",
                readPostconditionContract: "Saved resource read surfaces may be stale after a successful call.",
                failureSemantics: "Save failure may leave partial or indeterminate project file changes.",
                dangerousNotes: ["This operation can persist Unity project files without transactional rollback."]),
        };
    }

    private static UcliOperationAssuranceContract CreateSafeQueryAssurance ()
    {
        return new UcliOperationAssuranceContract(
            sideEffects: [UcliOperationSideEffectValues.ObservesUnityState],
            touchedKinds: Array.Empty<string>(),
            planMode: UcliOperationPlanModeValues.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Read Unity state without applying mutation.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the observation was not fully produced.",
            dangerousNotes: Array.Empty<string>());
    }
}
