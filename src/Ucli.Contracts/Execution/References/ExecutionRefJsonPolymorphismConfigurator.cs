using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts;

/// <summary> Configures the effective tagged-union contract shared by CLI and IPC serialization. </summary>
internal static class ExecutionRefJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type == typeof(ExecutionRef))
        {
            typeInfo.PolymorphismOptions = CreatePolymorphismOptions(
                typeInfo,
                typeof(ActiveExecutionRef),
                typeof(RecoveryExecutionRef),
                typeof(TerminalExecutionRef));
            return true;
        }

        if (typeInfo.Type == typeof(IRecoveryExecutionRef))
        {
            typeInfo.PolymorphismOptions = CreatePolymorphismOptions(
                typeInfo,
                typeof(RecoveryExecutionRef));
            return true;
        }

        if (typeInfo.Type == typeof(IReconnectableExecutionRef))
        {
            typeInfo.PolymorphismOptions = CreatePolymorphismOptions(
                typeInfo,
                typeof(ActiveExecutionRef),
                typeof(RecoveryExecutionRef));
            return true;
        }

        if (typeInfo.Type == typeof(ITerminalExecutionRef))
        {
            typeInfo.PolymorphismOptions = CreatePolymorphismOptions(
                typeInfo,
                typeof(TerminalExecutionRef));
            return true;
        }

        return false;
    }

    private static JsonPolymorphismOptions CreatePolymorphismOptions (
        JsonTypeInfo typeInfo,
        params Type[] referenceTypes)
    {
        var lifecyclePropertyName = typeInfo.Options.PropertyNamingPolicy?.ConvertName(
                nameof(ExecutionRef.Lifecycle))
            ?? nameof(ExecutionRef.Lifecycle);
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = lifecyclePropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var referenceType in referenceTypes)
        {
            options.DerivedTypes.Add(new JsonDerivedType(
                referenceType,
                TextVocabulary.GetText(GetLifecycle(referenceType))));
        }

        return options;
    }

    private static ExecutionLifecycle GetLifecycle (Type referenceType)
    {
        if (referenceType == typeof(ActiveExecutionRef))
        {
            return ExecutionLifecycle.Active;
        }
        if (referenceType == typeof(RecoveryExecutionRef))
        {
            return ExecutionLifecycle.Recovery;
        }
        if (referenceType == typeof(TerminalExecutionRef))
        {
            return ExecutionLifecycle.Terminal;
        }

        throw new ArgumentOutOfRangeException(
            nameof(referenceType),
            referenceType,
            "Execution reference polymorphism supports only concrete lifecycle branches.");
    }
}
