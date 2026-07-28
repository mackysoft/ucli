using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts;

/// <summary> Configures the effective tagged-union contract shared by CLI and IPC serialization. </summary>
internal static class ExecutionRefJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(ExecutionRef))
        {
            return false;
        }

        var lifecyclePropertyName = typeInfo.Options.PropertyNamingPolicy?.ConvertName(
                nameof(ExecutionRef.Lifecycle))
            ?? nameof(ExecutionRef.Lifecycle);
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = lifecyclePropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(ActiveExecutionRef),
            TextVocabulary.GetText(ExecutionLifecycle.Active)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(RecoveryExecutionRef),
            TextVocabulary.GetText(ExecutionLifecycle.Recovery)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(TerminalExecutionRef),
            TextVocabulary.GetText(ExecutionLifecycle.Terminal)));
        typeInfo.PolymorphismOptions = options;
        return true;
    }
}
