using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary> Configures the closed tagged union of Lifecycle Execution terminal records. </summary>
internal static class LifecycleExecutionTerminalRecordJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(LifecycleExecutionTerminalRecord))
        {
            return false;
        }

        var discriminatorPropertyName = typeInfo.Options.PropertyNamingPolicy?.ConvertName(
                nameof(LifecycleExecutionTerminalRecord.ExecutionKind))
            ?? nameof(LifecycleExecutionTerminalRecord.ExecutionKind);
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = discriminatorPropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(RefreshLifecycleExecutionTerminalRecord),
            TextVocabulary.GetText(LifecycleExecutionKind.Refresh)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(CompileLifecycleExecutionTerminalRecord),
            TextVocabulary.GetText(LifecycleExecutionKind.Compile)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(PlayEnterLifecycleExecutionTerminalRecord),
            TextVocabulary.GetText(LifecycleExecutionKind.PlayEnter)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(PlayExitLifecycleExecutionTerminalRecord),
            TextVocabulary.GetText(LifecycleExecutionKind.PlayExit)));
        typeInfo.PolymorphismOptions = options;
        return true;
    }
}
