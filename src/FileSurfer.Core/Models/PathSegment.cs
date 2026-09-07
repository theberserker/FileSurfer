namespace FileSurfer.Core.Models;

/// <summary>
/// Represents a single clickable segment of a directory path shown in the breadcrumb bar.
/// </summary>
/// <param name="DisplayName">
/// The name shown for this segment: the directory name, or the root token
/// (for example <c>C:\</c> or <c>/</c>) for the first segment.
/// </param>
/// <param name="FullPath">
/// The absolute path this segment navigates to when clicked.
/// </param>
public readonly record struct PathSegment(string DisplayName, string FullPath);
