using System;
using System.Collections.Generic;

namespace IKVM.Clang.Vsix.Clangd;

/// <summary>
/// Arguments of <see cref="ClangCompileDatabase.Changed"/>.
/// </summary>
internal sealed class ClangCompileDatabaseChangedEventArgs : EventArgs
{

    /// <summary>
    /// Creates the arguments.
    /// </summary>
    public ClangCompileDatabaseChangedEventArgs(IReadOnlyCollection<string> files)
    {
        Files = files;
    }

    /// <summary>
    /// Full paths of the files whose commands were added, changed or removed.
    /// </summary>
    public IReadOnlyCollection<string> Files { get; }

}
