using System;
using System.Collections.Generic;
using System.IO;

namespace IKVM.Clang.Vsix.Clangd
{

    /// <summary>
    /// Builds a command for a file that is not compiled itself, such as a header, from that of a source file of the
    /// same project, so that it is parsed with the project's include directories, definitions and target.
    /// </summary>
    internal static class HeaderCommand
    {

        /// <summary>
        /// Returns <paramref name="source"/>'s command with <paramref name="path"/> as the input, parsed as the header
        /// form of the source's language, and without the output file.
        /// </summary>
        public static ClangCompileCommandEntry Derive(ClangCompileCommandEntry source, string path)
        {
            var arguments = new List<string>(source.Arguments.Count);

            for (int i = 0; i < source.Arguments.Count; i++)
            {
                var argument = source.Arguments[i];

                // the compiler itself
                if (i == 0)
                {
                    arguments.Add(argument);
                    continue;
                }

                // drop the output
                if (argument == "-o" && i + 1 < source.Arguments.Count)
                {
                    i++;
                    continue;
                }

                // parse as the header form of the language
                if (argument == "-x" && i + 1 < source.Arguments.Count)
                {
                    arguments.Add(argument);
                    arguments.Add(ToHeaderLanguage(source.Arguments[++i]));
                    continue;
                }

                // replace the input
                if (IsInput(source, argument))
                {
                    arguments.Add(path);
                    continue;
                }

                arguments.Add(argument);
            }

            return new ClangCompileCommandEntry(path, source.Context, source.WorkingDirectory, arguments);
        }

        static bool IsInput(ClangCompileCommandEntry source, string argument)
        {
            if (argument.StartsWith("-", StringComparison.Ordinal))
                return false;

            try
            {
                return string.Equals(Path.GetFullPath(Path.Combine(source.WorkingDirectory, argument)), source.File, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        static string ToHeaderLanguage(string language)
        {
            return language switch
            {
                "c" => "c-header",
                "c++" => "c++-header",
                "objective-c" => "objective-c-header",
                "objective-c++" => "objective-c++-header",
                _ => language,
            };
        }

    }

}
