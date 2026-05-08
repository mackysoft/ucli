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
            Assurance = new UcliOperationAssuranceContract(
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ObservesLiveUnity),
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
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: true,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ObservesLiveUnity),
        };
    }
}
