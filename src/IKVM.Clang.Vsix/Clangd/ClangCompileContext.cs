using System;
using System.IO;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// A Clang project built for one target identifier: what the user picks between in the editor's project context
/// list.
/// </summary>
internal sealed class ClangCompileContext : IEquatable<ClangCompileContext>
{

    /// <summary>
    /// Creates the context.
    /// </summary>
    public ClangCompileContext(string projectPath, string targetIdentifier, int order)
    {
        ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
        TargetIdentifier = targetIdentifier ?? "";
        Order = order;
    }

    /// <summary>
    /// Full path of the project file.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// The target identifier, or an empty string for a project that builds a single target.
    /// </summary>
    public string TargetIdentifier { get; }

    /// <summary>
    /// Position of the target identifier in the project's list; the first is the default context.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Identifies the context in the language server protocol.
    /// </summary>
    public string Id => TargetIdentifier.Length == 0 ? ProjectPath : ProjectPath + "|" + TargetIdentifier;

    /// <summary>
    /// Text shown for the context in the editor.
    /// </summary>
    public string Label
    {
        get
        {
            var name = Path.GetFileNameWithoutExtension(ProjectPath);
            return TargetIdentifier.Length == 0 ? name : $"{name} ({TargetIdentifier})";
        }
    }

    /// <inheritdoc />
    public bool Equals(ClangCompileContext? other)
    {
        return other is not null && StringComparer.OrdinalIgnoreCase.Equals(Id, other.Id);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as ClangCompileContext);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Id);

}
