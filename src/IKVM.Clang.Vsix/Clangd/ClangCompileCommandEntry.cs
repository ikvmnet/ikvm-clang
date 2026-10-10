using System;
using System.Collections.Generic;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// The compile command of one source file in one context: a Clang project built for one target identifier.
/// </summary>
internal sealed class ClangCompileCommandEntry
{

    /// <summary>
    /// Creates the entry.
    /// </summary>
    public ClangCompileCommandEntry(string file, ClangCompileContext context, string workingDirectory, IReadOnlyList<string> arguments)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        WorkingDirectory = workingDirectory ?? throw new ArgumentNullException(nameof(workingDirectory));
        Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
    }

    /// <summary>
    /// Full path of the source file.
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The project and target identifier the command belongs to.
    /// </summary>
    public ClangCompileContext Context { get; }

    /// <summary>
    /// Directory the compiler runs in.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The compiler followed by its arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

}
