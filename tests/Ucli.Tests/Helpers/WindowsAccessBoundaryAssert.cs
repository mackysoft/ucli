using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace MackySoft.Ucli.Tests.Helpers;

[SupportedOSPlatform("windows")]
internal static class WindowsAccessBoundaryAssert
{
    public static void DirectoryIsCurrentUserOnly (string path)
    {
        AssertCurrentUserOnly(new DirectoryInfo(path).GetAccessControl());
    }

    public static void FileIsCurrentUserOnly (string path)
    {
        AssertCurrentUserOnly(new FileInfo(path).GetAccessControl());
    }

    private static void AssertCurrentUserOnly (FileSystemSecurity security)
    {
        Assert.True(security.AreAccessRulesProtected);

        var currentUserSid = GetCurrentUserSid();
        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();

        Assert.NotEmpty(rules);
        Assert.All(
            rules,
            rule =>
            {
                Assert.False(rule.IsInherited);
                Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
                Assert.Equal(currentUserSid, rule.IdentityReference);
            });
    }

    private static SecurityIdentifier GetCurrentUserSid ()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return identity.User
            ?? throw new InvalidOperationException("Current Windows user SID could not be resolved for ACL assertions.");
    }
}
