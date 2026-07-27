using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Resolves the effective System.Text.Json contracts used by the uCLI IPC boundary. </summary>
public sealed class IpcJsonTypeInfoResolver : IJsonTypeInfoResolver
{
    private readonly DefaultJsonTypeInfoResolver resolver;

    private IpcJsonTypeInfoResolver (Action<JsonTypeInfo>? modifier = null)
    {
        resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(IpcJsonPolymorphismConfigurator.Configure);
        if (modifier != null)
        {
            resolver.Modifiers.Add(modifier);
        }
    }

    /// <summary> Gets the shared resolver used by IPC serialization and contract generation. </summary>
    public static IpcJsonTypeInfoResolver Default { get; } = new();

    /// <summary> Creates an IPC resolver with one consumer-specific modifier applied after the shared IPC contract. </summary>
    /// <param name="modifier"> The modifier applied after polymorphism is configured. </param>
    /// <returns> A resolver that preserves the shared IPC contract before applying <paramref name="modifier" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="modifier" /> is <see langword="null" />. </exception>
    internal static IJsonTypeInfoResolver Create (Action<JsonTypeInfo> modifier)
    {
        if (modifier == null)
        {
            throw new ArgumentNullException(nameof(modifier));
        }

        return new IpcJsonTypeInfoResolver(modifier);
    }

    /// <inheritdoc />
    public JsonTypeInfo? GetTypeInfo (
        Type type,
        JsonSerializerOptions options)
    {
        return resolver.GetTypeInfo(type, options);
    }
}
