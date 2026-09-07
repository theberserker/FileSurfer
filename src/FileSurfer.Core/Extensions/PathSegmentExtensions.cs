using System;
using System.Collections.Generic;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Extensions;

/// <summary>
/// Provides extension methods for decomposing a path into breadcrumb <see cref="PathSegment"/>s.
/// </summary>
public static class PathSegmentExtensions
{
    // Guards against a misbehaving IPathTools implementation causing an unbounded loop.
    private const int MaxSegments = 4096;

    /// <summary>
    /// Splits an absolute path into ordered breadcrumb segments, from the root to the leaf.
    /// </summary>
    /// <param name="pathTools">Path tools for the file system that owns <paramref name="path"/>.</param>
    /// <param name="path">The absolute directory path to split.</param>
    /// <returns>
    /// The segments in root-to-leaf order. The first segment's <see cref="PathSegment.DisplayName"/>
    /// is the root token (for example <c>C:\</c> or <c>/</c>). Empty when <paramref name="path"/> is blank.
    /// </returns>
    public static IReadOnlyList<PathSegment> ToPathSegments(this IPathTools pathTools, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Array.Empty<PathSegment>();

        List<PathSegment> segments = new();
        string current = pathTools.NormalizePath(path);

        while (segments.Count < MaxSegments)
        {
            string name = pathTools.GetFileName(current);
            if (name.Length == 0)
                name = current;

            segments.Add(new PathSegment(name, current));

            string parent = pathTools.GetParentDir(current);
            if (parent.Length == 0 || pathTools.PathsAreEqual(parent, current))
                break;

            current = parent;
        }

        segments.Reverse();
        return segments;
    }
}
