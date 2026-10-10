using IKVM.Clang.Vsix.Imaging;
using IKVM.Clang.Vsix.ProjectSystem;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.ProjectSystem;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;

namespace IKVM.Clang.Vsix.Icons;

/// <summary>
/// Provides icons in Solution Explorer for the Clang project root node and for the source and header file types
/// that IKVM.Clang.Sdk includes by default.
/// </summary>
[Export(typeof(IProjectTreePropertiesProvider))]
[AppliesTo(ClangProjectCapabilities.AppliesTo)]
[Order(1000)]
internal sealed class ClangFileIconProvider : IProjectTreePropertiesProvider
{

    static readonly ProjectImageMoniker ProjectIcon = ClangMonikers.ProjectIcon.ToProjectSystemType();

    /// <summary>
    /// Icon for each file extension. Keep in step with the default items in <c>IKVM.Clang.Sdk.DefaultItems.props</c>.
    /// </summary>
    static readonly Dictionary<string, ProjectImageMoniker> FileIcons = Build(new[]
    {
        (KnownMonikers.CFile, new[] { ".c", ".m" }),
        (KnownMonikers.CPPSourceFile, new[] { ".cpp", ".cc", ".cxx", ".c++", ".cppm", ".ixx", ".mm" }),
        (KnownMonikers.CPPHeaderFile, new[] { ".h", ".hpp", ".hh", ".hxx", ".h++", ".ipp" }),
        (KnownMonikers.ASMFile, new[] { ".s", ".asm" }),
    });

    static Dictionary<string, ProjectImageMoniker> Build((Microsoft.VisualStudio.Imaging.Interop.ImageMoniker Moniker, string[] Extensions)[] entries)
    {
        var map = new Dictionary<string, ProjectImageMoniker>(StringComparer.OrdinalIgnoreCase);
        foreach (var (moniker, extensions) in entries)
            foreach (var extension in extensions)
                map[extension] = moniker.ToProjectSystemType();

        return map;
    }

    /// <inheritdoc />
    public void CalculatePropertyValues(IProjectTreeCustomizablePropertyContext propertyContext, IProjectTreeCustomizablePropertyValues propertyValues)
    {
        if (propertyValues.Flags.Contains(ProjectTreeFlags.ProjectRoot))
        {
            propertyValues.Icon = ProjectIcon;
            propertyValues.ExpandedIcon = ProjectIcon;
            return;
        }

        if (propertyContext.IsFolder || string.IsNullOrEmpty(propertyContext.ItemName))
            return;

        if (FileIcons.TryGetValue(Path.GetExtension(propertyContext.ItemName), out var icon))
        {
            propertyValues.Icon = icon;
            propertyValues.ExpandedIcon = icon;
        }
    }

}
