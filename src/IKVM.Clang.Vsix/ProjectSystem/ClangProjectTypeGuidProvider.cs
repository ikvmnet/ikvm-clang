using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem
{

    /// <summary>
    /// Reports the Clang project type GUID, which Visual Studio writes into solution files for Clang projects.
    /// </summary>
    [Export(typeof(IItemTypeGuidProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    internal class ClangProjectTypeGuidProvider : IItemTypeGuidProvider
    {

        /// <inheritdoc />
        public Guid ProjectTypeGuid => ProjectType.ClangGuid;

    }

}
