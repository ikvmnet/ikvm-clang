using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem
{

    [Export]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    internal class ClangConfiguredProject
    {

        [Import]
        internal ConfiguredProject ConfiguredProject { get; private set; }

        [Import]
        internal ProjectProperties Properties { get; private set; }
    }

}
