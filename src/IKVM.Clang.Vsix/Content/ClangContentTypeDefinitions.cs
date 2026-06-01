using System.ComponentModel.Composition;

using Microsoft.VisualStudio.Utilities;

namespace IKVM.Clang.Vsix.Content
{

    internal static class ClangContentTypeDefinitions
    {

        // ── Content type definitions ──────────────────────────────────────

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

        // ── File extension mappings ───────────────────────────────────────

        // C source
        [Export]
        [FileExtension(".c")]
        [ContentType(ContentTypeNames.CCode)]
        internal static FileExtensionToContentTypeDefinition? CFileExtension = null;

        // C++ source
        [Export]
        [FileExtension(".cpp")]
        [ContentType(ContentTypeNames.CppCode)]
        internal static FileExtensionToContentTypeDefinition? CppFileExtension = null;

        [Export]
        [FileExtension(".cc")]
        [ContentType(ContentTypeNames.CppCode)]
        internal static FileExtensionToContentTypeDefinition? CcFileExtension = null;

        [Export]
        [FileExtension(".cxx")]
        [ContentType(ContentTypeNames.CppCode)]
        internal static FileExtensionToContentTypeDefinition? CxxFileExtension = null;

        [Export]
        [FileExtension(".c++")]
        [ContentType(ContentTypeNames.CppCode)]
        internal static FileExtensionToContentTypeDefinition? CppAltFileExtension = null;

        // C++ module interface
        [Export]
        [FileExtension(".ixx")]
        [ContentType(ContentTypeNames.CppCode)]
        internal static FileExtensionToContentTypeDefinition? IxxFileExtension = null;

        [Export]
        [FileExtension(".cppm")]
        [ContentType(ContentTypeNames.CppCode)]
        internal static FileExtensionToContentTypeDefinition? CppmFileExtension = null;

        // C/C++ headers
        [Export]
        [FileExtension(".h")]
        [ContentType(ContentTypeNames.CppHeader)]
        internal static FileExtensionToContentTypeDefinition? HFileExtension = null;

        [Export]
        [FileExtension(".hpp")]
        [ContentType(ContentTypeNames.CppHeader)]
        internal static FileExtensionToContentTypeDefinition? HppFileExtension = null;

        [Export]
        [FileExtension(".hh")]
        [ContentType(ContentTypeNames.CppHeader)]
        internal static FileExtensionToContentTypeDefinition? HhFileExtension = null;

        [Export]
        [FileExtension(".hxx")]
        [ContentType(ContentTypeNames.CppHeader)]
        internal static FileExtensionToContentTypeDefinition? HxxFileExtension = null;

        [Export]
        [FileExtension(".h++")]
        [ContentType(ContentTypeNames.CppHeader)]
        internal static FileExtensionToContentTypeDefinition? HppAltFileExtension = null;

        [Export]
        [FileExtension(".ipp")]
        [ContentType(ContentTypeNames.CppHeader)]
        internal static FileExtensionToContentTypeDefinition? IppFileExtension = null;

        // Objective-C
        [Export]
        [FileExtension(".m")]
        [ContentType(ContentTypeNames.ObjCCode)]
        internal static FileExtensionToContentTypeDefinition? MFileExtension = null;

        // Objective-C++
        [Export]
        [FileExtension(".mm")]
        [ContentType(ContentTypeNames.ObjCppCode)]
        internal static FileExtensionToContentTypeDefinition? MmFileExtension = null;

        // Assembly
        [Export]
        [FileExtension(".s")]
        [ContentType(ContentTypeNames.AsmCode)]
        internal static FileExtensionToContentTypeDefinition? SFileExtension = null;

        [Export]
        [FileExtension(".S")]
        [ContentType(ContentTypeNames.AsmCode)]
        internal static FileExtensionToContentTypeDefinition? SUpperFileExtension = null;

        [Export]
        [FileExtension(".asm")]
        [ContentType(ContentTypeNames.AsmCode)]
        internal static FileExtensionToContentTypeDefinition? AsmFileExtension = null;

    }

}
