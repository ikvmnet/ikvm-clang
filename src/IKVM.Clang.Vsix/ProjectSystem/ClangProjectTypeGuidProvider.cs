using System;
using System.ComponentModel.Composition;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.ProjectSystem
{

    [Export(typeof(IItemTypeGuidProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    internal class ClangProjectTypeGuidProvider : IItemTypeGuidProvider
    {

        [ImportingConstructor]
        public ClangProjectTypeGuidProvider()
        {

        }

        public Guid ProjectTypeGuid
        {
            get { return ProjectType.ClangGuid; }
        }

    }

}
