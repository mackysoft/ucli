using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests;

internal static class OpsCliOutputContractTestSupport
{
    private static readonly Lazy<ServiceProvider> SharedOpsServiceProvider = new(UcliServiceProviderTestFactory.CreateCore);

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
        UcliOperationKind kind,
        OperationPolicy policy,
        UcliOperationDescribeContract? describe = null)
    {
        describe ??= CreateGoDescribeContract(name, kind, policy);
        var argsContract = describe.ArgsContract
            ?? throw new InvalidOperationException("The operation fixture must declare an args contract.");

        var descriptor = new IndexOpEntryJsonContract(
            Name: name,
            Kind: kind,
            Policy: policy,
            ArgsContract: argsContract,
            DescriptorDigest: null,
            VerdictContract: describe.VerdictContract,
            ResultContract: describe.ResultContract,
            Exposure: null,
            PlayModeSupport: UcliOperationPlayModeSupport.Disallowed)
        {
            Description = describe.Description,
            Assurance = describe.Assurance,
            CodeContract = describe.CodeContract,
        };
        return descriptor with
        {
            DescriptorDigest = UcliOperationDescriptorDigest.Calculate(descriptor),
        };
    }

    public static UcliOperationAssuranceContract CreateAssurance (
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        var isMutation = kind == UcliOperationKind.Mutation;
        var isAdvancedCommand = kind == UcliOperationKind.Command
            && policy == OperationPolicy.Advanced;
        var isDangerousPolicy = policy == OperationPolicy.Dangerous;
        var isRiskyPolicy = policy != OperationPolicy.Safe;
        return new UcliOperationAssuranceContract(
            sideEffects: isDangerousPolicy
                ? [UcliOperationSideEffect.ExternalProcess]
                : isMutation ? [UcliOperationSideEffect.SceneSave]
                : isAdvancedCommand ? [UcliOperationSideEffect.EditorStateChange]
                : [UcliOperationSideEffect.ObservesUnityState],
            touchedKinds: isMutation ? [UcliTouchedResourceKind.Scene] : Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: isMutation ? "Persist save-relevant Unity state." : "Read Unity state without applying mutation.",
            touchedContract: isMutation ? "Reports the saved scene resource." : "Returns no touched resources.",
            readPostconditionContract: isMutation ? "Saved scene read surfaces may be stale after a successful call." : "Does not stale read surfaces by itself.",
            failureSemantics: isMutation ? "Save failure may leave partial or indeterminate scene file changes." : "Failure means the observation was not fully produced.",
            dangerousNotes: isRiskyPolicy ? ["Fixture operation has policy-specific risk metadata for contract validation."] : Array.Empty<string>());
    }

    private static UcliOperationDescribeContract CreateGoDescribeContract (
        string operationName,
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            operationName,
            serializerOptions.GetTypeInfo(typeof(GoDescribeArgs)),
            serializerOptions.GetTypeInfo(typeof(GameObjectDescriptionResult)));
        return UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
            "Returns a GameObject description including components and child hierarchy.",
            CreateAssurance(kind, policy),
            codeContract: null);
    }
}
