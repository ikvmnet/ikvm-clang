using IKVM.Clang.Vsix.Imaging;
using IKVM.Clang.Vsix.ProjectSystem;

using Microsoft.VisualStudio.ProjectSystem;

using System;
using System.ComponentModel.Composition;

namespace IKVM.Clang.Vsix.Icons
{

    /// <summary>
    /// Provides file and project icons in Solution Explorer for Clang source file types
    /// and for the Clang project root node itself.
    /// </summary>
    [Export(typeof(IProjectTreePropertiesProvider))]
    [AppliesTo(ClangProjectCapabilities.AppliesTo)]
    [Order(1000)]
    internal sealed class ClangFileIconProvider : IProjectTreePropertiesProvider
    {

        static readonly ProjectImageMoniker s_cppSourceFileMoniker = Microsoft.VisualStudio.Imaging.KnownMonikers.CPPSourceFile.ToProjectSystemType();
        static readonly ProjectImageMoniker s_cppHeaderFileMoniker = Microsoft.VisualStudio.Imaging.KnownMonikers.CPPHeaderFile.ToProjectSystemType();
        static readonly ProjectImageMoniker s_binaryFileMoniker = Microsoft.VisualStudio.Imaging.KnownMonikers.BinaryFile.ToProjectSystemType();
        static readonly ProjectImageMoniker s_projectIconMoniker = ClangMonikers.ProjectIcon.ToProjectSystemType();

        static ProjectImageMoniker? GetMonikerForExtension(string itemName)
        {
            if (itemName.EndsWith(".c", StringComparison.OrdinalIgnoreCase))
                return s_cppSourceFileMoniker;

            if (itemName.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".cc", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".cxx", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".c++", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".cppm", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".ixx", StringComparison.OrdinalIgnoreCase))
                return s_cppSourceFileMoniker;

            if (itemName.EndsWith(".h", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".hh", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".hxx", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".h++", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".ipp", StringComparison.OrdinalIgnoreCase))
                return s_cppHeaderFileMoniker;

            if (itemName.EndsWith(".m", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".mm", StringComparison.OrdinalIgnoreCase))
                return s_cppSourceFileMoniker;

            if (itemName.EndsWith(".s", StringComparison.OrdinalIgnoreCase) ||
                itemName.EndsWith(".asm", StringComparison.OrdinalIgnoreCase))
                return s_binaryFileMoniker;

            return null;
        }

        public void CalculatePropertyValues(IProjectTreeCustomizablePropertyContext context, IProjectTreeCustomizablePropertyValues propertyValues)
        {
            if (propertyValues.Flags.Contains(ProjectTreeFlags.ProjectRoot))
            {
                propertyValues.Icon = s_projectIconMoniker;
                propertyValues.ExpandedIcon = s_projectIconMoniker;
                return;
            }

            if (context.IsFolder || string.IsNullOrEmpty(context.ItemName))
                return;

            var moniker = GetMonikerForExtension(context.ItemName);
            if (moniker != null)
            {
                propertyValues.Icon = moniker;
                propertyValues.ExpandedIcon = moniker;
            }
        }

    }

}
