using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

internal static class OpsCliOutputContractTestSupport
{
    private static readonly Lazy<ServiceProvider> SharedOpsServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

    private static readonly string[] FreezeInternalOperationFields =
    [
        "policyDerivation",
        "policyRestriction",
        "exposure",
        "policyReason",
    ];

    public static async Task<CommandExecutionResult> RunOpsListCommandAsync (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        string? nameRegex = null,
        string? operationKind = null,
        string? maxPolicy = null,
        bool failFast = false)
    {
        return await CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<OpsListCommand>(
                    SharedOpsServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .ListAsync(
                    projectPath: projectPath,
                    mode: mode,
                    timeout: timeout,
                    readIndexMode: readIndexMode,
                    nameRegex: nameRegex,
                    operationKind: operationKind,
                    maxPolicy: maxPolicy,
                    failFast: failFast));
    }

    public static async Task<CommandExecutionResult> RunOpsDescribeCommandAsync (
        string operationName,
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false)
    {
        return await CommandResultCapture.ExecuteAsync(() =>
            ActivatorUtilities.CreateInstance<OpsDescribeCommand>(
                    SharedOpsServiceProvider.Value,
                    CommandResultTestWriter.Create())
                .DescribeAsync(
                    operationName,
                    projectPath: projectPath,
                    mode: mode,
                    timeout: timeout,
                    readIndexMode: readIndexMode,
                    failFast: failFast));
    }

    public static IndexOpEntryJsonContract CreateDescribedEntry (
        string name,
        string kind,
        string policy,
        string argsSchemaJson,
        string? resultSchemaJson = null,
        UcliOperationDescribeContract? describe = null)
    {
        var useDefaultDescribe = describe == null;
        describe ??= CreateGoDescribeContract();
        return new IndexOpEntryJsonContract(
            name,
            kind,
            policy,
            argsSchemaJson,
            resultSchemaJson)
        {
            Description = describe.Description,
            Inputs = describe.Inputs,
            ResultContract = describe.ResultContract,
            Assurance = useDefaultDescribe ? CreateAssurance(kind, policy) : describe.Assurance,
            CodeContract = describe.CodeContract,
        };
    }

    public static UcliOperationAssuranceContract CreateAssurance (
        string kind,
        string policy)
    {
        var isMutation = string.Equals(kind, "mutation", StringComparison.Ordinal);
        var isAdvancedCommand = string.Equals(kind, "command", StringComparison.Ordinal)
            && string.Equals(policy, "advanced", StringComparison.Ordinal);
        var isDangerousPolicy = string.Equals(policy, "dangerous", StringComparison.Ordinal);
        var isRiskyPolicy = !string.Equals(policy, "safe", StringComparison.Ordinal);
        return new UcliOperationAssuranceContract(
            sideEffects: isDangerousPolicy
                ? [UcliOperationSideEffect.ExternalProcess]
                : isMutation ? [UcliOperationSideEffect.SceneSave]
                : isAdvancedCommand ? [UcliOperationSideEffect.EditorStateChange]
                : [UcliOperationSideEffect.ObservesUnityState],
            touchedKinds: isMutation ? [UcliTouchedResourceKindNames.Scene] : Array.Empty<string>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: isMutation ? "Persist save-relevant Unity state." : "Read Unity state without applying mutation.",
            touchedContract: isMutation ? "Reports the saved scene resource." : "Returns no touched resources.",
            readPostconditionContract: isMutation ? "Saved scene read surfaces may be stale after a successful call." : "Does not stale read surfaces by itself.",
            failureSemantics: isMutation ? "Save failure may leave partial or indeterminate scene file changes." : "Failure means the observation was not fully produced.",
            dangerousNotes: isRiskyPolicy ? ["Fixture operation has policy-specific risk metadata for contract validation."] : Array.Empty<string>());
    }

    public static void AssertDescribeVariantFields (JsonElement operationElement)
    {
        var targetInput = Assert.Single(
            operationElement.GetProperty("inputs").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "target", StringComparison.Ordinal));
        var globalObjectIdVariant = Assert.Single(
            targetInput.GetProperty("variants").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "byGlobalObjectId", StringComparison.Ordinal));
        Assert.False(globalObjectIdVariant.TryGetProperty("argsPaths", out _));
        Assert.False(globalObjectIdVariant.TryGetProperty("constraints", out _));

        var field = Assert.Single(globalObjectIdVariant.GetProperty("fields").EnumerateArray());
        Assert.Equal("globalObjectId", field.GetProperty("name").GetString());
        Assert.Equal("$.target.globalObjectId", field.GetProperty("argsPath").GetString());
        Assert.Equal("Resolved Unity GlobalObjectId.", field.GetProperty("description").GetString());

        var constraint = Assert.Single(field.GetProperty("constraints").EnumerateArray());
        Assert.Equal("globalObjectId", constraint.GetProperty("kind").GetString());

        var sceneHierarchyVariant = Assert.Single(
            targetInput.GetProperty("variants").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "bySceneHierarchyPath", StringComparison.Ordinal));
        Assert.False(sceneHierarchyVariant.TryGetProperty("argsPaths", out _));
        Assert.False(sceneHierarchyVariant.TryGetProperty("constraints", out _));

        var sceneField = Assert.Single(
            sceneHierarchyVariant.GetProperty("fields").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "scene", StringComparison.Ordinal));
        Assert.Equal("$.target.scene", sceneField.GetProperty("argsPath").GetString());
        Assert.Equal("Scene asset path for a hierarchy selector.", sceneField.GetProperty("description").GetString());
        var sceneConstraint = Assert.Single(
            sceneField.GetProperty("constraints").EnumerateArray(),
            constraint => string.Equals(constraint.GetProperty("kind").GetString(), "assetExists", StringComparison.Ordinal));
        Assert.Equal("assetExists", sceneConstraint.GetProperty("kind").GetString());
        Assert.Equal("scene", sceneConstraint.GetProperty("assetKind").GetString());

        var hierarchyPathField = Assert.Single(
            sceneHierarchyVariant.GetProperty("fields").EnumerateArray(),
            candidate => string.Equals(candidate.GetProperty("name").GetString(), "hierarchyPath", StringComparison.Ordinal));
        Assert.Equal("$.target.hierarchyPath", hierarchyPathField.GetProperty("argsPath").GetString());
        Assert.Equal("Unity hierarchy path inside the selected scene or prefab.", hierarchyPathField.GetProperty("description").GetString());
        var hierarchyPathConstraint = Assert.Single(
            hierarchyPathField.GetProperty("constraints").EnumerateArray(),
            constraint => string.Equals(constraint.GetProperty("kind").GetString(), "hierarchyPath", StringComparison.Ordinal));
        Assert.Equal("hierarchyPath", hierarchyPathConstraint.GetProperty("kind").GetString());
    }

    public static void AssertNoFreezeInternalOperationTopLevelFields (JsonElement operation)
    {
        foreach (var property in operation.EnumerateObject())
        {
            Assert.DoesNotContain(property.Name, FreezeInternalOperationFields);
        }
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract ()
    {
        return UcliOperationDescribeContractBuilder.Create<GoDescribeArgs, GameObjectDescriptionResult>(
            "Returns a GameObject description including components and child hierarchy.",
            new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.ObservesUnityState },
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
