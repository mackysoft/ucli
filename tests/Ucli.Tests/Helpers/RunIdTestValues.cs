namespace MackySoft.Ucli.Tests;

internal static class RunIdTestValues
{
    public const string CompileText = "32c8976c-42f2-4e42-a857-3cba1241a7de";
    public const string BuildText = "4d4f6988-c38a-4f9c-9d80-49d46beadf4a";
    public const string TestText = "5bece4b0-f581-49c8-9b68-a8b5fc8701e3";

    public static readonly Guid Compile = Guid.Parse(CompileText);
    public static readonly Guid Build = Guid.Parse(BuildText);
    public static readonly Guid Test = Guid.Parse(TestText);
}
