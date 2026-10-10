using System.ComponentModel.Composition;

using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

namespace IKVM.Clang.Vsix.Content;

/// <summary>
/// Content types for the source files of Clang projects. These are deliberately not mapped to file extensions:
/// such a mapping would apply to every file with that extension in Visual Studio, taking C and C++ files away
/// from other project types. Instead <see cref="ClangEditorFactory"/> applies them to files opened from Clang
/// projects. They derive from the language server base content type, which Visual Studio's language client
/// requires of the content types clangd serves (see <c>ClangdLanguageClient</c>).
/// </summary>
internal static class ClangContentTypeDefinitions
{

    [Export]
    [Name(ContentTypeNames.CCode)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition? CCodeContentType = null;

    [Export]
    [Name(ContentTypeNames.CppCode)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition? CppCodeContentType = null;

    [Export]
    [Name(ContentTypeNames.ObjCCode)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition? ObjCContentType = null;

    [Export]
    [Name(ContentTypeNames.ObjCppCode)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition? ObjCppContentType = null;

    [Export]
    [Name(ContentTypeNames.CppHeader)]
    [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
    internal static ContentTypeDefinition? CppHeaderContentType = null;

    // assembly is not served by clangd
    [Export]
    [Name(ContentTypeNames.AsmCode)]
    [BaseDefinition("code")]
    internal static ContentTypeDefinition? AsmContentType = null;

}
