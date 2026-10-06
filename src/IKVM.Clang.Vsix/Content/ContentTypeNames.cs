using System;
using System.Collections.Generic;
using System.IO;

namespace IKVM.Clang.Vsix.Content
{

    /// <summary>
    /// Names of the Clang content types, and which file extensions take them inside a Clang project.
    /// </summary>
    internal static class ContentTypeNames
    {

        internal const string CCode = "ClangCCode";
        internal const string CppCode = "ClangCppCode";
        internal const string ObjCCode = "ClangObjCCode";
        internal const string ObjCppCode = "ClangObjCppCode";
        internal const string CppHeader = "ClangCppHeader";
        internal const string AsmCode = "ClangAsmCode";

        /// <summary>
        /// Content type for each file extension. Keep in step with the default items in
        /// <c>IKVM.Clang.Sdk.DefaultItems.props</c>.
        /// </summary>
        static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".c"] = CCode,
            [".cpp"] = CppCode,
            [".cc"] = CppCode,
            [".cxx"] = CppCode,
            [".c++"] = CppCode,
            [".cppm"] = CppCode,
            [".ixx"] = CppCode,
            [".h"] = CppHeader,
            [".hpp"] = CppHeader,
            [".hh"] = CppHeader,
            [".hxx"] = CppHeader,
            [".h++"] = CppHeader,
            [".ipp"] = CppHeader,
            [".m"] = ObjCCode,
            [".mm"] = ObjCppCode,
            [".s"] = AsmCode,
            [".asm"] = AsmCode,
        };

        /// <summary>
        /// Gets the name of the Clang content type for the given file, if it is one a Clang project handles.
        /// </summary>
        internal static bool TryGetForFile(string path, out string contentType)
        {
            return ByExtension.TryGetValue(Path.GetExtension(path), out contentType!);
        }

    }

}
