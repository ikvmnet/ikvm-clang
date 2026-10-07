using System;

using Microsoft.VisualStudio.Imaging.Interop;

namespace IKVM.Clang.Vsix.Imaging;

/// <summary>
/// Monikers for the images in <c>IKVM.Clang.imagemanifest</c>. The GUID must match <c>MonikerGuid</c> in the
/// manifest.
/// </summary>
internal static class ClangMonikers
{

    static readonly Guid ManifestGuid = new Guid("7A3F2B1C-4E8D-4F6A-9B2C-1D5E3F7A8B9C");

    /// <summary>
    /// Returns the moniker for the image with the given id in the Clang image manifest.
    /// </summary>
    static ImageMoniker Get(int id) => new ImageMoniker { Guid = ManifestGuid, Id = id };

    /// <summary>
    /// Icon shown on the root node of a Clang project.
    /// </summary>
    public static ImageMoniker ProjectIcon => Get(1);

}
