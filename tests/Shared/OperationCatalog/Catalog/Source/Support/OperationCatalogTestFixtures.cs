using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.TestSupport;

internal static class OperationCatalogTestFixtures
{
    public static OpsCatalogSnapshot CreateSnapshot (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var completedOperations = operations
            .Select(WithDescriptorDigest)
            .ToArray();
        if (!OpsCatalogSnapshot.TryCreate(
                generatedAtUtc,
                completedOperations,
                "operations",
                allowEditLoweringOnlyEntries: false,
                out var snapshot,
                out var error))
        {
            throw new InvalidOperationException($"Operation catalog test fixture is invalid. {error}");
        }

        return snapshot!;
    }

    public static OpsCatalogFetchResult CreateFetchResult (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        return OpsCatalogFetchResult.Success(CreateSnapshot(generatedAtUtc, operations));
    }

    public static ValidatedOpsOperation CreateValidatedOperation (IndexOpEntryJsonContract operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return CreateSnapshot(
            DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            [operation]).Operations[0];
    }

    public static PersistedOpsCatalogReadResult CreatePersistedReadResult (
        DateTimeOffset generatedAtUtc,
        IndexFreshness freshness,
        IReadOnlyList<IndexOpEntryJsonContract> operations)
    {
        return PersistedOpsCatalogReadResult.Success(
            CreateSnapshot(generatedAtUtc, operations),
            freshness);
    }

    public static OpsCatalogSourceRefreshResult CreateSourceRefreshResult (
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<IndexOpEntryJsonContract> operations,
        string? fallbackReason)
    {
        return OpsCatalogSourceRefreshResult.Success(
            CreateSnapshot(generatedAtUtc, operations),
            fallbackReason);
    }

    public static IndexOpEntryJsonContract CreateGoDescribeEntry (
        IReadOnlyList<UcliOperationSideEffect>? sideEffects = null)
    {
        var assurance = CreateSafeQueryAssurance(sideEffects ?? [UcliOperationSideEffect.ObservesUnityState]);
        var describe = CreateDescribe<GoDescribeArgs, GameObjectDescriptionResult>(
            UcliPrimitiveOperationNames.GoDescribe,
            "Returns a GameObject description including components and child hierarchy.",
            assurance);
        return WithDescriptorDigest(
            new IndexOpEntryJsonContract(
                Name: UcliPrimitiveOperationNames.GoDescribe,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsContract: describe.ArgsContract,
                DescriptorDigest: null,
                VerdictContract: null,
                ResultContract: describe.ResultContract,
                Exposure: null,
                PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
            {
                Description = describe.Description,
                Assurance = describe.Assurance,
            });
    }

    public static IndexOpEntryJsonContract CreateSceneSaveEntry ()
    {
        var assurance = new UcliOperationAssuranceContract(
            sideEffects: [UcliOperationSideEffect.SceneSave],
            touchedKinds: [UcliTouchedResourceKind.Scene],
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Observe save-relevant project state without writing project files.",
            callSemantics: "Persist save-relevant Unity state.",
            touchedContract: "Reports resources known to be saved.",
            readPostconditionContract: "Saved resource read surfaces may be stale after a successful call.",
            failureSemantics: "Save failure may leave partial or indeterminate project file changes.",
            dangerousNotes: ["This operation can persist Unity project files without transactional rollback."]);
        var describe = CreateDescribe<ScenePathArgs, UcliNoResult>(
            UcliPrimitiveOperationNames.SceneSave,
            "Saves a Unity scene asset.",
            assurance);
        return WithDescriptorDigest(
            new IndexOpEntryJsonContract(
                Name: UcliPrimitiveOperationNames.SceneSave,
                Kind: UcliOperationKind.Mutation,
                Policy: OperationPolicy.Advanced,
                ArgsContract: describe.ArgsContract,
                DescriptorDigest: null,
                VerdictContract: null,
                ResultContract: null,
                Exposure: null,
                PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
            {
                Description = describe.Description,
                Assurance = describe.Assurance,
            });
    }

    private static UcliOperationDescribeContract CreateDescribe<TArgs, TResult> (
        string operationName,
        string description,
        UcliOperationAssuranceContract assurance)
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            operationName,
            serializerOptions.GetTypeInfo(typeof(TArgs)),
            typeof(TResult) == typeof(UcliNoResult)
                ? null
                : serializerOptions.GetTypeInfo(typeof(TResult)));
        return UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
            description,
            assurance,
            codeContract: null);
    }

    private static UcliOperationAssuranceContract CreateSafeQueryAssurance (IReadOnlyList<UcliOperationSideEffect> sideEffects)
    {
        return new UcliOperationAssuranceContract(
            sideEffects: sideEffects,
            touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Read Unity state without applying mutation.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the observation was not fully produced.",
            dangerousNotes: Array.Empty<string>());
    }

    private static IndexOpEntryJsonContract WithDescriptorDigest (IndexOpEntryJsonContract operation)
    {
        var descriptorWithoutDigest = operation with { DescriptorDigest = null };
        return descriptorWithoutDigest with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(descriptorWithoutDigest),
        };
    }
}
