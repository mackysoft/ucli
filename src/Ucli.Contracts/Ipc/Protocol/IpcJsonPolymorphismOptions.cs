using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>Creates the common strict discriminator policy used by IPC tagged unions.</summary>
internal static class IpcJsonPolymorphismOptions
{
    public static JsonPolymorphismOptions Create (params JsonDerivedType[] variants)
    {
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = UcliOperationContractPropertyNames.Kind,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };

        for (var i = 0; i < variants.Length; i++)
        {
            options.DerivedTypes.Add(variants[i]);
        }

        return options;
    }
}
