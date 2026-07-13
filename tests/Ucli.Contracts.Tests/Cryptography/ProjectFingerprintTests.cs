using System.Reflection;

namespace MackySoft.Ucli.Contracts.Tests.Cryptography;

public sealed class ProjectFingerprintTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private const string OtherFingerprint = "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    public static TheoryData<string> InvalidCanonicalValues => new()
    {
        string.Empty,
        new string('a', 63),
        new string('a', 65),
        "0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef",
        "g123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
    };

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithCanonicalValue_PreservesCanonicalText ()
    {
        var fingerprint = new ProjectFingerprint(Fingerprint);

        Assert.Equal(Fingerprint, fingerprint.ToString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ProjectFingerprint(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidCanonicalValues))]
    [Trait("Size", "Small")]
    public void Constructor_WithNonCanonicalValue_ThrowsArgumentException (string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ProjectFingerprint(value));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WithCanonicalValue_ReturnsEquivalentFingerprint ()
    {
        var result = ProjectFingerprint.TryParse(Fingerprint, out var fingerprint);

        Assert.True(result);
        Assert.Equal(new ProjectFingerprint(Fingerprint), fingerprint);
    }

    [Theory]
    [InlineData(null)]
    [MemberData(nameof(InvalidCanonicalValues))]
    [Trait("Size", "Small")]
    public void TryParse_WithNonCanonicalValue_ReturnsFalse (string? value)
    {
        var result = ProjectFingerprint.TryParse(value, out var fingerprint);

        Assert.False(result);
        Assert.Null(fingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_WithSameCanonicalValue_UsesValueSemantics ()
    {
        var first = new ProjectFingerprint(Fingerprint);
        var second = new ProjectFingerprint(Fingerprint);

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.False(first != second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Equality_WithDifferentCanonicalValue_ReturnsFalse ()
    {
        var first = new ProjectFingerprint(Fingerprint);
        var second = new ProjectFingerprint(OtherFingerprint);

        Assert.NotEqual(first, second);
        Assert.False(first == second);
        Assert.True(first != second);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PublicSurface_ExposesOnlyOneStringConstructorWithoutSentinelsOrConversions ()
    {
        var type = typeof(ProjectFingerprint);
        var constructor = Assert.Single(type.GetConstructors());

        Assert.True(type.IsSealed);
        Assert.Equal(typeof(string), Assert.Single(constructor.GetParameters()).ParameterType);
        Assert.Empty(type.GetMember("Empty", BindingFlags.Public | BindingFlags.Static));
        Assert.Empty(type.GetMember("Unknown", BindingFlags.Public | BindingFlags.Static));
        Assert.DoesNotContain(
            type.GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name is "op_Implicit" or "op_Explicit");
        Assert.DoesNotContain(
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly),
            method => method.ReturnType == typeof(byte[])
                || method.GetParameters().Any(parameter => parameter.ParameterType == typeof(byte[])));
        Assert.DoesNotContain(
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            property => property.PropertyType == typeof(byte[]));
        Assert.DoesNotContain(
            type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static),
            field => field.FieldType == typeof(byte[]));
    }
}
