using System;

using Microsoft.VisualStudio.Imaging.Interop;

namespace IKVM.Clang.Vsix.Images
{

    /// <summary>
    /// Image monikers for custom Clang images registered via <c>ClangMonikers.imagemanifest</c>.
    /// The GUID must match <c>ClangMonikersGuid</c> in the manifest.
    /// </summary>
    internal static class ClangImageMonikers
    {

        /// <summary>
        /// Icon representing a Clang SDK project node.
        /// </summary>
        public static ImageMoniker ClangProjectIcon => new ImageMoniker { Guid = new Guid("{7A3F2B1C-4E8D-4F6A-9B2C-1D5E3F7A8B9C}"), Id = 0 };

    }

}
