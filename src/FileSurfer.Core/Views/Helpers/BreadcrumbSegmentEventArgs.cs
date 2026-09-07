using System;
using FileSurfer.Core.Models;

namespace FileSurfer.Core.Views.Helpers;

/// <summary>
/// Carries the <see cref="PathSegment"/> that was activated in a <see cref="BreadcrumbBar"/>.
/// </summary>
/// <param name="segment">The activated path segment.</param>
public sealed class BreadcrumbSegmentEventArgs(PathSegment segment) : EventArgs
{
    /// <summary>
    /// Gets the path segment the user clicked.
    /// </summary>
    public PathSegment Segment { get; } = segment;
}
