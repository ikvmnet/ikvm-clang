using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem;

/// <summary>
/// Per-configuration state of a Clang project, exported so other components can reach the configured project and
/// its properties.
/// </summary>
[Export]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
internal class ClangConfiguredProject
{

    /// <summary>
    /// The CPS configured project this instance belongs to.
    /// </summary>
    [Import]
    internal ConfiguredProject ConfiguredProject { get; private set; } = null!;

    /// <summary>
    /// Strongly typed access to the properties of this configuration.
    /// </summary>
    [Import]
    internal ProjectProperties Properties { get; private set; } = null!;

}
