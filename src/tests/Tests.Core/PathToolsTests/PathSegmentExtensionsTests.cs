using FileSurfer.Core.Extensions;
using FileSurfer.Core.Models;

namespace Tests.Core.PathToolsTests;

public class PathSegmentExtensionsTests
{
    private static (string Name, string Path)[] Pairs(IReadOnlyList<PathSegment> segments) =>
        segments.Select(s => (s.DisplayName, s.FullPath)).ToArray();

    [Fact]
    public void ToPathSegments_Blank_ReturnsEmpty()
    {
        Assert.Empty(RemoteUnixPathTools.Instance.ToPathSegments(""));
        Assert.Empty(RemoteUnixPathTools.Instance.ToPathSegments("   "));
    }

    [Fact]
    public void ToPathSegments_UnixRoot_ReturnsSingleRootSegment()
    {
        IReadOnlyList<PathSegment> segments = RemoteUnixPathTools.Instance.ToPathSegments("/");

        Assert.Equal(new[] { ("/", "/") }, Pairs(segments));
    }

    [Fact]
    public void ToPathSegments_UnixNested_ReturnsRootToLeaf()
    {
        IReadOnlyList<PathSegment> segments = RemoteUnixPathTools.Instance.ToPathSegments("/a/b/c");

        Assert.Equal(
            new[] { ("/", "/"), ("a", "/a"), ("b", "/a/b"), ("c", "/a/b/c") },
            Pairs(segments)
        );
    }

    [Fact]
    public void ToPathSegments_UnixTrailingSlash_MatchesWithout()
    {
        (string, string)[] withSlash = Pairs(RemoteUnixPathTools.Instance.ToPathSegments("/a/b/"));
        (string, string)[] without = Pairs(RemoteUnixPathTools.Instance.ToPathSegments("/a/b"));

        Assert.Equal(without, withSlash);
    }

    [Fact]
    public void ToPathSegments_LastSegmentIsLeaf_FirstIsRoot()
    {
        IReadOnlyList<PathSegment> segments = RemoteUnixPathTools.Instance.ToPathSegments(
            "/home/user/projects"
        );

        Assert.Equal("/", segments[0].DisplayName);
        Assert.Equal("projects", segments[^1].DisplayName);
        Assert.Equal("/home/user/projects", segments[^1].FullPath);
        Assert.Equal(4, segments.Count);
    }

    [Fact]
    public void ToPathSegments_WindowsDriveRoot_ReturnsSingleRootSegment()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IReadOnlyList<PathSegment> segments = LocalPathTools.Instance.ToPathSegments(@"C:\");

        Assert.Equal(new[] { (@"C:\", @"C:\") }, Pairs(segments));
    }

    [Fact]
    public void ToPathSegments_WindowsNested_ReturnsRootToLeaf()
    {
        if (!OperatingSystem.IsWindows())
            return;

        IReadOnlyList<PathSegment> segments = LocalPathTools.Instance.ToPathSegments(
            @"C:\dev\oss\FileSurfer"
        );

        Assert.Equal(
            new[]
            {
                (@"C:\", @"C:\"),
                ("dev", @"C:\dev"),
                ("oss", @"C:\dev\oss"),
                ("FileSurfer", @"C:\dev\oss\FileSurfer"),
            },
            Pairs(segments)
        );
    }

    [Fact]
    public void ToPathSegments_WindowsTrailingSeparator_MatchesWithout()
    {
        if (!OperatingSystem.IsWindows())
            return;

        (string, string)[] withSep = Pairs(
            LocalPathTools.Instance.ToPathSegments(@"C:\dev\oss\")
        );
        (string, string)[] without = Pairs(
            LocalPathTools.Instance.ToPathSegments(@"C:\dev\oss")
        );

        Assert.Equal(without, withSep);
    }

    [Fact]
    public void ToPathSegments_WorksThroughIPathToolsInterface()
    {
        IPathTools tools = RemoteUnixPathTools.Instance;

        Assert.Equal(
            new[] { ("/", "/"), ("etc", "/etc") },
            Pairs(tools.ToPathSegments("/etc"))
        );
    }
}
