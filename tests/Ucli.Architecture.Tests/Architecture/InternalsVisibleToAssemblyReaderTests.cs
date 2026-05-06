namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class InternalsVisibleToAssemblyReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ReadAssemblyNamesFromSource_supports_attribute_trivia_and_qualified_attribute_name ()
    {
        var assemblyNames = InternalsVisibleToAssemblyReader.ReadAssemblyNamesFromSource(
            """
            using System.Runtime.CompilerServices;

            [assembly: InternalsVisibleTo ( "MackySoft.Ucli.Tests" )]
            [assembly: System.Runtime.CompilerServices.InternalsVisibleToAttribute("MackySoft.Ucli.Unity.Tests.Editor")]
            """);

        Assert.Equal(
            [
                "MackySoft.Ucli.Tests",
                "MackySoft.Ucli.Unity.Tests.Editor",
            ],
            assemblyNames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadAssemblyNamesFromSource_ignores_commented_friend_assembly_declarations ()
    {
        var assemblyNames = InternalsVisibleToAssemblyReader.ReadAssemblyNamesFromSource(
            """
            // [assembly: InternalsVisibleTo("Ignored.Tests")]
            [assembly: InternalsVisibleTo("MackySoft.Ucli.Tests")]
            """);

        Assert.Equal(["MackySoft.Ucli.Tests"], assemblyNames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadAssemblyNamesFromSource_supports_verbatim_string_literals ()
    {
        var assemblyNames = InternalsVisibleToAssemblyReader.ReadAssemblyNamesFromSource(
            """
            [assembly: InternalsVisibleTo(@"MackySoft.Ucli.Tests")]
            """);

        Assert.Equal(["MackySoft.Ucli.Tests"], assemblyNames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadAssemblyNamesFromSource_rejects_non_literal_friend_assembly_declarations ()
    {
        Assert.Throws<InvalidOperationException>(static () => InternalsVisibleToAssemblyReader.ReadAssemblyNamesFromSource(
            """
            internal static class FriendAssemblies
            {
                internal const string Tests = "MackySoft.Ucli.Tests";
            }

            [assembly: InternalsVisibleTo(FriendAssemblies.Tests)]
            """));
    }
}
