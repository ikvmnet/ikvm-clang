using System.ComponentModel.Composition;
using System.IO;

using IKVM.Clang.Vsix.Images;

using Microsoft.VisualStudio.ProjectSystem;

namespace IKVM.Clang.Vsix.Icons
{

    /// <summary>
    /// Provides file and project icons in Solution Explorer for Clang source file types
    /// and for the Clang project root node itself.
    /// </summary>
    [Export(typeof(IProjectTreePropertiesProvider))]
    [AppliesTo(ClangUnconfiguredProject.UniqueCapability)]
    [Order(100)]
    internal sealed class ClangFileIconProvider : IProjectTreePropertiesProvider
    {

        public void CalculatePropertyValues(IProjectTreeCustomizablePropertyContext context, IProjectTreeCustomizablePropertyValues propertyValues)
        {
            // Project root node: no item type (not an MSBuild item) and no parent (empty parent flags)
            if (context.ItemType == null && context.ParentNodeFlags == ProjectTreeFlags.Empty)
            {
                var projectMoniker = ClangImageMonikers.ClangProject.ToProjectSystemType();
                propertyValues.Icon = projectMoniker;
                propertyValues.ExpandedIcon = projectMoniker;
                return;
            }

            if (context.IsFolder || string.IsNullOrEmpty(context.ItemName))
                return;

            var ext = Path.GetExtension(context.ItemName);
            if (string.IsNullOrEmpty(ext))
                return;

            var moniker = GetMonikerForExtension(ext.ToLowerInvariant());
            if (moniker != null)
            {
                propertyValues.Icon = moniker;
                propertyValues.ExpandedIcon = moniker;
            }
        }

        static ProjectImageMoniker GetMonikerForExtension(string ext)
        {
            switch (ext)
            {
                // C source files
                case ".c":
                    return Microsoft.VisualStudio.Imaging.KnownMonikers.CPPSourceFile.ToProjectSystemType();

                // C++ source files
                case ".cpp":
                case ".cc":
                case ".cxx":
                case ".c++":
                case ".cppm":
                case ".ixx":
                    return Microsoft.VisualStudio.Imaging.KnownMonikers.CPPSourceFile.ToProjectSystemType();

                // C/C++ header files
                case ".h":
                case ".hpp":
                case ".hh":
                case ".hxx":
                case ".h++":
                case ".ipp":
                    return Microsoft.VisualStudio.Imaging.KnownMonikers.CPPHeaderFile.ToProjectSystemType();

                // Objective-C / Objective-C++
                case ".m":
                case ".mm":
                    return Microsoft.VisualStudio.Imaging.KnownMonikers.CPPSourceFile.ToProjectSystemType();

                // Assembly
                case ".s":
                case ".asm":
                    return Microsoft.VisualStudio.Imaging.KnownMonikers.BinaryFile.ToProjectSystemType();

                default:
                    return null;
            }
        }

    }

}
