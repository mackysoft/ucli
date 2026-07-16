using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Testing;

public sealed class TestRunProgressEntryContractTests
{
    private static readonly Guid RunId = Guid.Parse("12345678-1234-5678-9abc-def012345678");

    [Fact]
    [Trait("Size", "Small")]
    public void TestCaseResult_DefinesCompleteWireVocabulary ()
    {
        Assert.Equal(
            ["pass", "fail", "skipped", "inconclusive"],
            ContractLiteralCodec.GetLiterals<TestCaseResult>());
    }

    public static TheoryData<bool, string, string?> InvalidCaseIdentityValues => new()
    {
        { false, "TestId", null },
        { false, "TestId", "" },
        { false, "TestId", " " },
        { false, "TestName", null },
        { false, "TestName", "" },
        { false, "TestName", " " },
        { false, "AssemblyName", "" },
        { false, "AssemblyName", " " },
        { false, "TestPlatform", null },
        { false, "TestPlatform", "" },
        { false, "TestPlatform", " " },
        { true, "TestId", null },
        { true, "TestId", "" },
        { true, "TestId", " " },
        { true, "TestName", null },
        { true, "TestName", "" },
        { true, "TestName", " " },
        { true, "AssemblyName", "" },
        { true, "AssemblyName", " " },
        { true, "TestPlatform", null },
        { true, "TestPlatform", "" },
        { true, "TestPlatform", " " },
    };

    [Theory]
    [MemberData(nameof(InvalidCaseIdentityValues))]
    [Trait("Size", "Small")]
    public void CaseEntry_WhenIdentityValueIsInvalid_RejectsConstruction (
        bool isFinished,
        string parameterName,
        string? invalidValue)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => CreateCaseEntry(
            isFinished,
            testId: parameterName == "TestId" ? invalidValue! : "test-1",
            testName: parameterName == "TestName" ? invalidValue! : "CanPass",
            assemblyName: parameterName == "AssemblyName" ? invalidValue : "Assembly.Tests",
            testPlatform: parameterName == "TestPlatform" ? invalidValue! : "editmode",
            categories: ["smoke"]));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(false, true, -1)]
    [InlineData(false, false, 0)]
    [InlineData(false, false, 1)]
    [InlineData(true, true, -1)]
    [InlineData(true, false, 0)]
    [InlineData(true, false, 1)]
    [Trait("Size", "Small")]
    public void CaseEntry_WhenCategoriesAreInvalid_RejectsConstruction (
        bool isFinished,
        bool isNull,
        int invalidIndex)
    {
        string[] categories = isNull
            ? null!
            : invalidIndex switch
            {
                0 => [null!],
                1 => ["smoke", " "],
                _ => ["smoke"],
            };

        var exception = Assert.ThrowsAny<ArgumentException>(() => CreateCaseEntry(
            isFinished,
            "test-1",
            "CanPass",
            "Assembly.Tests",
            "editmode",
            categories));

        Assert.Equal("Categories", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestCaseFinished_WhenResultIsDefault_RejectsConstruction ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TestCaseFinishedEntry(
            RunId,
            "test-1",
            "CanPass",
            "Assembly.Tests",
            "editmode",
            ["smoke"],
            default,
            12,
            null,
            null));

        Assert.Equal("Result", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestCaseFinished_WhenDurationIsNegative_RejectsConstruction ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new TestCaseFinishedEntry(
            RunId,
            "test-1",
            "CanPass",
            "Assembly.Tests",
            "editmode",
            ["smoke"],
            TestCaseResult.Pass,
            -1,
            null,
            null));

        Assert.Equal("DurationMilliseconds", exception.ParamName);
    }

    [Theory]
    [InlineData("TestPlatform", null)]
    [InlineData("TestPlatform", "")]
    [InlineData("TestPlatform", " ")]
    [InlineData("AssemblyNames", null)]
    [InlineData("AssemblyNames", "")]
    [InlineData("AssemblyNames", " ")]
    [InlineData("TestCategories", null)]
    [InlineData("TestCategories", "")]
    [InlineData("TestCategories", " ")]
    [Trait("Size", "Small")]
    public void TestRunStarted_WhenRequiredValueIsInvalid_RejectsConstruction (
        string parameterName,
        string? invalidValue)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(() => new TestRunStartedEntry(
            RunId,
            parameterName == "TestPlatform" ? invalidValue! : "editmode",
            TestFilter: null,
            parameterName == "AssemblyNames" ? [invalidValue!] : ["Assembly.Tests"],
            parameterName == "TestCategories" ? [invalidValue!] : ["smoke"]));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProgressEntries_CopyCollectionsIntoReadOnlySnapshots ()
    {
        var startedCategories = new[] { "started" };
        var finishedCategories = new[] { "finished" };
        var assemblyNames = new[] { "Assembly.Tests" };
        var runCategories = new[] { "run" };
        var started = new TestCaseStartedEntry(
            RunId,
            "test-1",
            "CanPass",
            "Assembly.Tests",
            "editmode",
            startedCategories);
        var finished = new TestCaseFinishedEntry(
            RunId,
            "test-1",
            "CanPass",
            "Assembly.Tests",
            "editmode",
            finishedCategories,
            TestCaseResult.Pass,
            12,
            null,
            null);
        var runStarted = new TestRunStartedEntry(
            RunId,
            "editmode",
            TestFilter: null,
            assemblyNames,
            runCategories);

        startedCategories[0] = "changed";
        finishedCategories[0] = "changed";
        assemblyNames[0] = "Changed.Tests";
        runCategories[0] = "changed";

        Assert.Equal("started", Assert.Single(started.Categories));
        Assert.Equal("finished", Assert.Single(finished.Categories));
        Assert.Equal("Assembly.Tests", Assert.Single(runStarted.AssemblyNames));
        Assert.Equal("run", Assert.Single(runStarted.TestCategories));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)started.Categories)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)finished.Categories)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)runStarted.AssemblyNames)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)runStarted.TestCategories)[0] = "changed");
    }

    private static object CreateCaseEntry (
        bool isFinished,
        string testId,
        string testName,
        string? assemblyName,
        string testPlatform,
        string[] categories)
    {
        return isFinished
            ? new TestCaseFinishedEntry(
                RunId,
                testId,
                testName,
                assemblyName,
                testPlatform,
                categories,
                TestCaseResult.Pass,
                12,
                null,
                null)
            : new TestCaseStartedEntry(
                RunId,
                testId,
                testName,
                assemblyName,
                testPlatform,
                categories);
    }
}
