using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Utilities;

namespace IKVM.Clang.Vsix.Content
{

    /// <summary>
    /// Content types for the source files of Clang projects. These are deliberately not mapped to file extensions:
    /// such a mapping would apply to every file with that extension in Visual Studio, taking C and C++ files away
    /// from other project types. Instead <see cref="ClangEditorFactory"/> applies them to files opened from Clang
    /// projects.
    /// </summary>
    internal static class ClangContentTypeDefinitions
    {

        [Export]
        [Name(ContentTypeNames.CCode)]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition? CCodeContentType = null;

        [Export]
        [Name(ContentTypeNames.CppCode)]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition? CppCodeContentType = null;

        [Export]
        [Name(ContentTypeNames.ObjCCode)]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition? ObjCContentType = null;

        [Export]
        [Name(ContentTypeNames.ObjCppCode)]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition? ObjCppContentType = null;

        [Export]
        [Name(ContentTypeNames.CppHeader)]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition? CppHeaderContentType = null;

        [Export]
        [Name(ContentTypeNames.AsmCode)]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition? AsmContentType = null;

    }

}
