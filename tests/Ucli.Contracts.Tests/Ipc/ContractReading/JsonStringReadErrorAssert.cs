using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

internal static class JsonStringReadErrorAssert
{
    public static void Equal (
        JsonStringReadError error,
        JsonStringReadErrorKind expectedKind,
        string expectedPropertyName)
    {
        JsonAssert.For(JsonSerializer.SerializeToElement(error))
            .HasInt32("Kind", (int)expectedKind)
            .HasString("PropertyName", expectedPropertyName);
    }
}
